using MobileFix.Domain.Inventory;

namespace MobileFix.Domain.Identification;

/// <summary>
/// Ensambla la identidad del dispositivo a partir de los peldaños alcanzados y decide si el
/// resultado es fiable.
///
/// Dos reglas que valen más que cualquier heurística:
/// 1. <b>El conflicto manda sobre la confianza.</b> Dos peldaños que se contradicen producen una
///    identidad dudosa aunque hayan coincidido en todo lo demás. En LATAM es la firma habitual de
///    un equipo con placa cambiada o reparación previa.
/// 2. <b>Un peldaño que no se alcanzó no se inventa.</b> Si falta la baseband, el informe dice que
///    falta; no se rellena con el valor de otro equipo del mismo modelo.
/// </summary>
public sealed class IdentificationLadder
{
    public DeviceIdentity Assemble(IdentificationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var highest = HighestLevel(inputs);
        var conflicts = DetectConflicts(inputs);
        var confidence = ComputeConfidence(highest, conflicts, inputs);

        var socVendor = inputs.Handshake?.SocVendor
            ?? InferSocVendor(inputs.Descriptor?.SuspectedMechanism)
            ?? "Desconocido";

        var socModel = inputs.Handshake?.SocModel ?? "Desconocido";

        return new DeviceIdentity
        {
            SocVendor = socVendor,
            SocModel = socModel,
            SecureBootEnabled = inputs.Handshake?.SecureBootEnabled,
            AuthenticationRequired = inputs.Handshake?.RequiresAuthentication,
            Oem = inputs.Build?.Oem ?? inputs.Board?.ProductName ?? "Desconocido",
            Model = inputs.Build?.Model ?? inputs.Board?.ProductName ?? "Desconocido",
            RegionVariant = inputs.Build?.RegionVariant,
            BoardCodename = inputs.Board?.BoardCodename,
            BuildId = inputs.Build?.BuildId,
            AndroidVersion = inputs.Build?.AndroidVersion,
            SecurityPatch = inputs.Build?.SecurityPatch,
            BasebandVersion = inputs.Baseband?.BasebandVersion,
            CarrierLock = inputs.Build?.CarrierLock,
            Carrier = inputs.Build?.Carrier,
            ImeiHash = SensitiveData.HashOptional(inputs.Baseband?.Imei),
            SerialHash = SensitiveData.HashOptional(inputs.Handshake?.DeviceSerial),
            HighestLevel = highest,
            ConfidencePercent = confidence,
            Evidence = inputs.Evidence,
            Conflicts = conflicts,
            Partitions = inputs.Partitions,
        };
    }

    private static ProbeLevel HighestLevel(IdentificationInputs inputs)
    {
        if (inputs.Baseband is not null)
        {
            return ProbeLevel.L6_Baseband;
        }

        if (inputs.Board is not null)
        {
            return ProbeLevel.L5_BoardIdentity;
        }

        if (inputs.Build is not null)
        {
            return ProbeLevel.L4_BuildProperties;
        }

        if (inputs.Partitions.Count > 0)
        {
            return ProbeLevel.L3_PartitionTable;
        }

        if (inputs.Handshake is not null)
        {
            return ProbeLevel.L2_Handshake;
        }

        return inputs.Descriptor is not null ? ProbeLevel.L1_Interface : ProbeLevel.L0_UsbDescriptor;
    }

    /// <summary>
    /// Discrepancias que se pueden detectar sin catálogo, cruzando lo que dice cada peldaño.
    /// </summary>
    private static List<string> DetectConflicts(IdentificationInputs inputs)
    {
        var conflicts = new List<string>();

        // 1. El handshake dijo un fabricante de SoC y las propiedades del sistema dicen otro.
        if (inputs.Handshake is not null && inputs.Build is not null && inputs.Build.Oem is not null)
        {
            var expected = InferKnownOem(inputs.Build.Oem);
            if (expected is not null && !string.Equals(expected, inputs.Handshake.SocVendor, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(
                    $"el handshake dice {inputs.Handshake.SocVendor} pero el sistema declara {inputs.Build.Oem}: " +
                    "posible placa cambiada o firmware de otro modelo");
            }
        }

        // 2. La región del build no coincide con la del módem: síntoma de firmware cruzado.
        if (inputs.Build?.RegionVariant is not null && inputs.Baseband?.ModemRegion is not null
            && !string.Equals(inputs.Build.RegionVariant, inputs.Baseband.ModemRegion, StringComparison.OrdinalIgnoreCase))
        {
            conflicts.Add(
                $"región del sistema ({inputs.Build.RegionVariant}) distinta de la del módem ({inputs.Baseband.ModemRegion}): " +
                "firmware de otra variante");
        }

        // El arranque seguro y la autenticación NO se registran aquí a propósito: no son conflictos
        // de identidad, son límites de la operación y se informan aparte (etapa 6, Comprobar
        // restricciones). Mezclarlos haría que un equipo perfectamente identificado pareciera dudoso.
        return conflicts;
    }

    private static int ComputeConfidence(ProbeLevel highest, List<string> conflicts, IdentificationInputs inputs)
    {
        var baseConfidence = highest switch
        {
            ProbeLevel.L6_Baseband => 98,
            ProbeLevel.L5_BoardIdentity => 96,
            ProbeLevel.L4_BuildProperties => 94,
            ProbeLevel.L3_PartitionTable => 85,
            ProbeLevel.L2_Handshake => 75,
            ProbeLevel.L1_Interface => 45,
            _ => 20,
        };

        // Un peldaño que no se alcanzó NO baja la confianza: se informa como campo ausente.
        // La confianza mide la certeza sobre lo que sí se leyó, no la completitud del informe.
        // Restar puntos aquí bajaba un equipo perfectamente identificado a «media» por un artefacto
        // de umbral, y una alerta que salta sin motivo deja de mirarse.
        if (conflicts.Count > 0)
        {
            baseConfidence -= 15;
        }

        return Math.Clamp(baseConfidence, 0, 100);
    }

    private static string? InferSocVendor(AccessMechanism? mechanism) => mechanism switch
    {
        AccessMechanism.BromMediaTek => "MediaTek",
        AccessMechanism.EdlQualcomm => "Qualcomm",
        AccessMechanism.FdlUnisoc => "Unisoc",
        AccessMechanism.EubExynos => "Exynos",
        AccessMechanism.DfuApple => "Apple",
        _ => null,
    };

    /// <summary>
    /// Fabricante de SoC que se deduce del OEM del sistema, <b>solo cuando ese OEM es exclusivo</b>.
    ///
    /// Apple y Google fabrican su propio silicio, así que un Apple con handshake MediaTek es una
    /// incoherencia real. Huawei, Samsung, Xiaomi y Motorola montan silicio de varios fabricantes:
    /// un Huawei Nova 9 con Snapdragon es legítimo, y marcarlo como conflicto sería un falso positivo
    /// que erosiona la confianza en el propio aviso.
    /// </summary>
    private static string? InferKnownOem(string oem) => oem.Trim().ToLowerInvariant() switch
    {
        "apple" => "Apple",
        "google" => "Google Tensor",
        _ => null,
    };
}
