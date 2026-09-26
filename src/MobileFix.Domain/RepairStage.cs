namespace MobileFix.Domain;

/// <summary>
/// Las 13 etapas del pipeline operativo (PLAN-MAESTRO §2). El orden es vinculante:
/// ninguna etapa puede iniciarse sin que todas las anteriores estén completadas.
/// </summary>
public enum RepairStage
{
    Connect = 1,
    Identify = 2,
    Classify = 3,
    Diagnose = 4,
    Correlate = 5,
    CheckConstraints = 6,
    Backup = 7,
    PlanRepair = 8,
    SafetyGate = 9,
    Repair = 10,
    Verify = 11,
    CompareBeforeAfter = 12,
    Report = 13,
}

/// <summary>Estados de una etapa (DESIGN-TOKENS §3.3).</summary>
public enum StageStatus
{
    Pending,
    Active,
    Done,
    Failed,
    Blocked,
    Skipped,
}

public static class RepairStages
{
    /// <summary>Las 13 etapas en orden. Es el orden canónico del pipeline.</summary>
    public static readonly IReadOnlyList<RepairStage> Ordered = Enum.GetValues<RepairStage>();

    /// <summary>Número total de etapas. Si alguien añade una, este número deja de coincidir.</summary>
    public const int Total = 13;

    /// <summary>Etiquetas en español para la interfaz y los informes.</summary>
    public static string Label(this RepairStage stage) => stage switch
    {
        RepairStage.Connect => "Conectar",
        RepairStage.Identify => "Identificar",
        RepairStage.Classify => "Clasificar",
        RepairStage.Diagnose => "Diagnosticar",
        RepairStage.Correlate => "Correlacionar",
        RepairStage.CheckConstraints => "Comprobar restricciones",
        RepairStage.Backup => "Respaldar",
        RepairStage.PlanRepair => "Analizar procedimiento",
        RepairStage.SafetyGate => "Safety Gate",
        RepairStage.Repair => "Reparar",
        RepairStage.Verify => "Verificar",
        RepairStage.CompareBeforeAfter => "Comparar antes/después",
        RepairStage.Report => "Reportar",
        _ => stage.ToString(),
    };

    /// <summary>
    /// Etapas que acceden al dispositivo, para leer o para escribir.
    /// Planificar (8), autorizar (9) y reportar (13) no tocan hardware: son cómputo puro
    /// y por eso no se pueden considerar «lectura».
    /// </summary>
    public static bool TouchesDevice(this RepairStage stage) => stage is
        RepairStage.Connect or
        RepairStage.Identify or
        RepairStage.Classify or
        RepairStage.Diagnose or
        RepairStage.Correlate or
        RepairStage.CheckConstraints or
        RepairStage.Backup or
        RepairStage.Repair or
        RepairStage.Verify or
        RepairStage.CompareBeforeAfter;

    /// <summary>Etapas que leen del dispositivo y nunca escriben. Son seguras por construcción.</summary>
    public static bool IsReadOnly(this RepairStage stage) => stage.TouchesDevice() && !stage.IsWriteStage();

    /// <summary>Etapas sin acceso al dispositivo: análisis, autorización y generación de informes.</summary>
    public static bool IsOffDevice(this RepairStage stage) => !stage.TouchesDevice();

    /// <summary>La única etapa que escribe. Toda la arquitectura existe para proteger este hecho.</summary>
    public static bool IsWriteStage(this RepairStage stage) => stage == RepairStage.Repair;
}
