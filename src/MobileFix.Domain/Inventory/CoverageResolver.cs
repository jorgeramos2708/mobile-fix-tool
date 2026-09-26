namespace MobileFix.Domain.Inventory;

/// <summary>
/// Resuelve qué puede hacer la plataforma con un dispositivo concreto, a partir de la evidencia
/// <b>medida</b> en el banco (PLAN-MAESTRO §5).
///
/// Regla de honestidad: si no hay ningún equipo de ese tipo en el banco, el veredicto es
/// «no soportado» con la razón explícita, no una promesa. La matriz de cobertura existe para
/// que el técnico sepa <b>antes</b> de tocar nada qué puede y qué no puede hacer.
/// </summary>
public sealed class CoverageResolver(InventoryDataset dataset)
{
    private readonly InventoryDataset _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));

    public InventoryDataset Dataset => _dataset;

    /// <summary>Mecanismo que corresponde al fabricante del SoC leído en el handshake.</summary>
    public static AccessMechanism MechanismFor(string? socVendor) => Normalise(socVendor) switch
    {
        "mediatek" or "mtk" => AccessMechanism.BromMediaTek,
        "qualcomm" or "qc" or "snapdragon" => AccessMechanism.EdlQualcomm,
        "unisoc" or "spreadtrum" => AccessMechanism.FdlUnisoc,
        "exynos" or "samsung" => AccessMechanism.EubExynos,
        "apple" => AccessMechanism.DfuApple,
        _ => AccessMechanism.None,
    };

    public CoverageAssessment Resolve(DeviceFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        var evidence = _dataset.IsProvisional ? CoverageEvidence.SeededDemo : CoverageEvidence.BenchMeasured;
        var vendor = Normalise(fingerprint.SocVendor);
        var matching = _dataset.Devices.Where(device => Normalise(device.SocVendor) == vendor).ToArray();

        if (matching.Length == 0)
        {
            return CoverageAssessment.Evaluate(
                CoverageVerdict.Unsupported,
                CoverageEvidence.None,
                $"sin evidencia en el banco para {fingerprint.SocVendor}: la cobertura no está demostrada",
                $"mecanismo esperado: {MechanismFor(fingerprint.SocVendor)}");
        }

        var verdict = Decide(matching);
        var reasons = new List<string>
        {
            $"evidencia sobre {matching.Length} equipo(s) del banco con SoC {fingerprint.SocVendor}",
            $"mecanismo esperado: {MechanismFor(fingerprint.SocVendor)}",
        };

        var repairEvidence = matching.Count(device => device.DemonstratesRepair);
        reasons.Add(repairEvidence == 0
            ? "ningún equipo del grupo demuestra reparación alcanzable"
            : $"{repairEvidence} de {matching.Length} equipos demuestran reparación alcanzable");

        var locked = matching.Where(device => device.CarrierLock == CarrierLockState.LockedToCarrier).ToArray();
        if (locked.Length > 0)
        {
            var carriers = locked
                .Select(device => string.IsNullOrWhiteSpace(device.Carrier) ? "operador no declarado" : device.Carrier!)
                .Distinct()
                .Take(3);

            reasons.Add(
                $"{locked.Length} de {matching.Length} equipos están bloqueados a operador ({string.Join(", ", carriers)}): " +
                "la liberación es solo por el operador, nunca por bypass");
        }

        var esimOnly = matching.Count(device => device.Sim == SimType.EsimOnly);
        if (esimOnly > 0)
        {
            reasons.Add($"{esimOnly} equipo(s) del grupo son solo eSIM: no aceptan SIM física mexicana");
        }

        if (_dataset.IsProvisional)
        {
            reasons.Add("PROVISIONAL: calculado con datos de ejemplo, no con mediciones del banco");
        }

        return CoverageAssessment.Evaluate(verdict, evidence, reasons.ToArray());
    }

    private static CoverageVerdict Decide(DeviceRecord[] matching)
    {
        var verdict = matching.Any(device => device.T3 == SupportLevel.Yes) ? CoverageVerdict.Full
            : matching.Any(device => device.T2 == SupportLevel.Yes) ? CoverageVerdict.Partial
            : matching.Any(device => device.T1 == SupportLevel.Yes) ? CoverageVerdict.ReadOnly
            : CoverageVerdict.Unsupported;

        // Escritura sin ruta legítima no es escritura: si todo el grupo exige material de terceros,
        // la plataforma baja el veredicto en lugar de prometer algo que no puede cumplir.
        var noLegitimatePath = matching.All(device =>
            device.RequiresAuth is AuthRequirement.ThirdPartyLegitimate or AuthRequirement.Unknown);

        if (verdict == CoverageVerdict.Full && noLegitimatePath)
        {
            return CoverageVerdict.Partial;
        }

        return verdict;
    }

    private static string Normalise(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "samsung exynos" or "exynos" => "exynos",
            "mediatek" or "mtk" => "mediatek",
            "qualcomm" or "qcom" => "qualcomm",
            "spreadtrum" or "unisoc" => "unisoc",
            var other => other,
        };
}
