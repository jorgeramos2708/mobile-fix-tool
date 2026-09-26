using System.Security.Cryptography;
using System.Text;
using MobileFix.Domain.Inventory;

namespace MobileFix.Domain.Identification;

/// <summary>
/// Procedencia de la identidad. Mismo principio que en la cobertura: un dato sin procedencia
/// declarada no vale nada, y una simulación nunca puede pasar por una medición.
/// </summary>
public enum IdentificationEvidence
{
    /// <summary>Leída de un dispositivo físico conectado.</summary>
    MeasuredFromDevice,

    /// <summary>Producida por el transporte simulado: sirve para construir, no para afirmar.</summary>
    ScriptedSimulation,

    /// <summary>Declarada a mano por el técnico.</summary>
    DeclaredByOperator,

    Unknown,
}

/// <summary>
/// Identidad del dispositivo construida a partir de los peldaños de la escalera.
///
/// Todo lo que aquí aparece está <b>leído del equipo</b> —placa, build, baseband, parche de
/// seguridad, tabla de particiones—, no tomado de un catálogo. Esa es la diferencia entre saber
/// qué es este teléfono y suponer qué modelo debería ser.
/// </summary>
public sealed record DeviceIdentity
{
    public required string SocVendor { get; init; }
    public required string SocModel { get; init; }
    public required string Oem { get; init; }
    public required string Model { get; init; }
    public string? RegionVariant { get; init; }
    public string? BoardCodename { get; init; }

    /// <summary>Build exacto leído del equipo. Es lo que decide si una operación aplica.</summary>
    public string? BuildId { get; init; }

    public string? AndroidVersion { get; init; }

    /// <summary>Parche de seguridad. Un equipo más nuevo que el que soporta el plan es un rechazo, no un aviso.</summary>
    public string? SecurityPatch { get; init; }

    public string? BasebandVersion { get; init; }

    /// <summary>
    /// Arranque seguro activo. Es un límite de la operación, no un conflicto de identidad:
    /// se informa en la etapa de restricciones, no en la de identificación.
    /// </summary>
    public bool? SecureBootEnabled { get; init; }

    /// <summary>El mecanismo exige material autenticado (DAA/SLA en MediaTek, firehose firmado en Qualcomm).</summary>
    public bool? AuthenticationRequired { get; init; }

    /// <summary>Bloqueo de operador leído del equipo. Determina qué se le puede prometer al cliente.</summary>
    public CarrierLockState? CarrierLock { get; init; }

    public string? Carrier { get; init; }

    /// <summary>Hash del IMEI. El valor en claro no se persiste: se usa y se descarta.</summary>
    public string? ImeiHash { get; init; }

    public string? SerialHash { get; init; }

    public required ProbeLevel HighestLevel { get; init; }

    public required int ConfidencePercent { get; init; }

    public required IdentificationEvidence Evidence { get; init; }

    /// <summary>Discrepancias entre peldaños. Si hay alguna, la identidad es dudosa y la interfaz lo dice.</summary>
    public IReadOnlyList<string> Conflicts { get; init; } = [];

    public IReadOnlyList<GptPartition> Partitions { get; init; } = [];

    public IdentityConfidence Confidence => Conflicts.Count > 0
        ? IdentityConfidence.Conflict
        : ConfidencePercent switch
        {
            >= 95 => IdentityConfidence.High,
            >= 70 => IdentityConfidence.Medium,
            _ => IdentityConfidence.Low,
        };

    public bool IsProvisional => Evidence != IdentificationEvidence.MeasuredFromDevice;

    public int CriticalPartitionCount => Partitions.Count(partition => partition.IsCritical);

    /// <summary>
    /// Puente con el motor de cobertura: la identidad medida es lo que se resuelve contra la
    /// evidencia del banco para decidir qué se puede hacer con este equipo.
    /// </summary>
    public DeviceFingerprint ToFingerprint() => new()
    {
        SocVendor = SocVendor,
        SocModel = SocModel,
        Oem = Oem,
        Model = Model,
        RegionVariant = RegionVariant,
        BuildId = BuildId,
        ConfidencePercent = ConfidencePercent,
        HighestSource = HighestLevel switch
        {
            ProbeLevel.L2_Handshake => FingerprintSource.Handshake,
            ProbeLevel.L3_PartitionTable => FingerprintSource.PartitionTable,
            ProbeLevel.L4_BuildProperties => FingerprintSource.BuildProperties,
            ProbeLevel.L5_BoardIdentity => FingerprintSource.BoardId,
            ProbeLevel.L6_Baseband => FingerprintSource.Baseband,
            _ => FingerprintSource.UsbDescriptor,
        },
        Conflicts = Conflicts,
    };

    public string Describe()
    {
        var provisional = IsProvisional ? " · PROVISIONAL (simulación)" : string.Empty;
        var board = BoardCodename is null ? string.Empty : $" · placa {BoardCodename}";

        return $"{Oem} {Model}{(RegionVariant is null ? string.Empty : $" ({RegionVariant})")} · " +
               $"{SocVendor} {SocModel}{board} · nivel {HighestLevel} · confianza {ConfidencePercent}% " +
               $"({Confidence}){provisional}";
    }
}

/// <summary>
/// Datos recogidos por cada peldaño antes de ensamblar la identidad. Los campos nulos significan
/// «ese peldaño no se alcanzó», que es información, no un hueco que rellenar a la ligera.
/// </summary>
public sealed record IdentificationInputs
{
    public TransportDescriptor? Descriptor { get; init; }
    public HandshakeResult? Handshake { get; init; }
    public IReadOnlyList<GptPartition> Partitions { get; init; } = [];
    public BuildProperties? Build { get; init; }
    public BoardIdentity? Board { get; init; }
    public BasebandIdentity? Baseband { get; init; }
    public required IdentificationEvidence Evidence { get; init; }
}

/// <summary>Tratamiento de datos sensibles: se guarda el hash, no el valor.</summary>
public static class SensitiveData
{
    /// <summary>Hash estable y corto de un identificador sensible (IMEI, número de serie).</summary>
    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())))[..16].ToLowerInvariant();

    public static string? HashOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Hash(value);

    /// <summary>Enmascara un IMEI para informes y pantallas: se ven los cuatro últimos dígitos.</summary>
    public static string Mask(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length < 4
            ? "****"
            : new string('*', value.Length - 4) + value[^4..];
}
