namespace MobileFix.Domain;

/// <summary>Peldaño de la escalera de identificación (PLAN-MAESTRO §4).</summary>
public enum FingerprintSource
{
    UsbDescriptor = 0,   // L0
    Interface = 1,       // L1
    Handshake = 2,       // L2  (Sahara / BROM / FDL / EUB / DFU)
    PartitionTable = 3,  // L3  (GPT leída por bootrom)
    BuildProperties = 4, // L4  (build.prop, vendor, bootloader)
    BoardId = 5,         // L5  (otp, persist, board id)
    Baseband = 6,        // L6  (solo lectura, hasheado)
}

/// <summary>Confianza de identificación (DESIGN-TOKENS §3.4).</summary>
public enum IdentityConfidence
{
    Low,
    Medium,
    High,
    Conflict,
}

/// <summary>
/// Huella del dispositivo. Inmutable una vez establecida en la etapa Identificar.
/// </summary>
public sealed record DeviceFingerprint
{
    public required string SocVendor { get; init; }
    public required string SocModel { get; init; }
    public required string Oem { get; init; }
    public required string Model { get; init; }
    public string? RegionVariant { get; init; }
    public string? BuildId { get; init; }
    public required int ConfidencePercent { get; init; }
    public required FingerprintSource HighestSource { get; init; }

    /// <summary>
    /// Discrepancias entre peldaños (típicamente L5 contra L6). En LATAM es la señal
    /// habitual de un equipo con placa cambiada o reparación previa.
    /// </summary>
    public IReadOnlyList<string> Conflicts { get; init; } = [];

    public IdentityConfidence Confidence => Conflicts.Count > 0
        ? IdentityConfidence.Conflict
        : ConfidencePercent switch
        {
            >= 95 => IdentityConfidence.High,
            >= 70 => IdentityConfidence.Medium,
            _ => IdentityConfidence.Low,
        };

    public string Describe()
    {
        var region = RegionVariant is null ? string.Empty : $" ({RegionVariant})";
        return $"{Oem} {Model}{region} · {SocVendor} {SocModel} · fuente {HighestSource} · " +
               $"confianza {ConfidencePercent}% ({Confidence})";
    }
}
