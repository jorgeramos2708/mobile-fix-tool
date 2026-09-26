namespace MobileFix.Domain.Inventory;

/// <summary>
/// Mecanismo de acceso de bajo nivel. La plataforma se organiza por mecanismo, no por fabricante
/// (PLAN-MAESTRO §1): cinco mecanismos cubren la mayor parte del universo Android.
/// </summary>
public enum AccessMechanism
{
    None,
    AdbOnly,
    Fastboot,
    BromMediaTek,
    EdlQualcomm,
    FdlUnisoc,
    EubExynos,
    DfuApple,
}

/// <summary>¿El mecanismo exige autenticación o material de terceros? Determina si hay ruta legítima.</summary>
public enum AuthRequirement
{
    None,
    OwnCredential,
    ThirdPartyLegitimate,
    Unknown,
}

public enum DeviceCondition
{
    Boots,
    Bootloop,
    NoBoot,
    RecoveryOnly,
    LowLevelOnly,
    Bricked,
    Dead,
    Unknown,
}

/// <summary>Peldaño más alto alcanzado en la escalera de identificación (PLAN-MAESTRO §4).</summary>
public enum EscaleraLevel
{
    None,
    L0,
    L1,
    L2,
    L3,
    L4,
    L5,
    L6,
}

/// <summary>Nivel de intervención alcanzado en un equipo concreto (T1 a T4).</summary>
public enum SupportLevel
{
    No,
    Partial,
    Yes,
    NotApplicable,
}

/// <summary>
/// Procedencia del dato. Es la diferencia entre una afirmación y una promesa:
/// la cobertura solo se puede declarar cuando está <b>medida</b> en el banco.
/// </summary>
public enum DataProvenance
{
    Bench,
    SeededDemo,
    Unknown,
}

/// <summary>
/// Un equipo del banco de pruebas. Es la unidad de evidencia: cada fila es una afirmación
/// de la plataforma sobre lo que puede hacer con un dispositivo real.
/// </summary>
public sealed record DeviceRecord
{
    public required string Id { get; init; }
    public required string Brand { get; init; }
    public required string Model { get; init; }
    public string? Region { get; init; }
    public required string SocVendor { get; init; }
    public string? SocModel { get; init; }
    public required string Platform { get; init; }
    public required AccessMechanism Mechanism { get; init; }
    public required AuthRequirement RequiresAuth { get; init; }
    public required DeviceCondition Condition { get; init; }
    public required EscaleraLevel MaxLevel { get; init; }
    public required SupportLevel T1 { get; init; }
    public required SupportLevel T2 { get; init; }
    public required SupportLevel T3 { get; init; }
    public required SupportLevel T4 { get; init; }

    /// <summary>Tipo de SIM del equipo: un iPhone de EEUU desde el 14 no acepta SIM física mexicana.</summary>
    public required SimType Sim { get; init; }

    /// <summary>Operador al que está bloqueado, si lo está (att, tmobile, verizon, metro, cricket, boost).</summary>
    public string? Carrier { get; init; }

    public required CarrierLockState CarrierLock { get; init; }

    public required UnlockPath Unlock { get; init; }

    public string? BenchLocation { get; init; }
    public string? Notes { get; init; }

    /// <summary>¿Este equipo demuestra que se puede reparar (T2 o T3)?</summary>
    public bool DemonstratesRepair => T2 == SupportLevel.Yes || T3 == SupportLevel.Yes;

    /// <summary>¿Se identificó al menos a nivel de modelo o superior (L4+)?</summary>
    public bool IdentifiedAtModelLevel => MaxLevel >= EscaleraLevel.L4;

    public string Describe() =>
        $"{Id} · {Brand} {Model} · {SocVendor}{(SocModel is null ? string.Empty : $" {SocModel}")} · " +
        $"{Mechanism} · {Condition} · nivel {MaxLevel}";
}
