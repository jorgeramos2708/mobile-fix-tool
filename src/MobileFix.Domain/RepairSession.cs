namespace MobileFix.Domain;

/// <summary>
/// Agregado raíz de la plataforma (PLAN-MAESTRO §2).
///
/// Implementa la invariante del <b>Punto Único de Escritura</b>: solo la etapa Reparar escribe,
/// y no puede iniciarse si las nueve etapas anteriores no están completadas, el respaldo no está
/// verificado y el Safety Gate no está aprobado. La restricción no es una convención de código:
/// se aplica aquí y no hay forma de saltarla desde capas superiores.
/// </summary>
public sealed class RepairSession
{
    /// <summary>Bucle acotado: máximo de intentos de reparación por sesión (PLAN-MAESTRO §2).</summary>
    public const int MaxRepairAttempts = 3;

    private readonly Dictionary<RepairStage, StageStatus> _stages =
        RepairStages.Ordered.ToDictionary(stage => stage, _ => StageStatus.Pending);

    public RepairSession(DateTimeOffset startedUtc)
    {
        StartedUtc = startedUtc;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DateTimeOffset StartedUtc { get; }

    public DeviceFingerprint? Fingerprint { get; private set; }

    public CoverageAssessment? Coverage { get; private set; }

    public bool BackupVerified { get; private set; }

    public bool SafetyGateApproved { get; private set; }

    public int RepairAttempts { get; private set; }

    public IReadOnlyDictionary<RepairStage, StageStatus> Stages => _stages;

    public StageStatus StatusOf(RepairStage stage) => _stages[stage];

    /// <summary>Primera etapa activa o pendiente. Es la única que puede avanzar.</summary>
    public RepairStage? CurrentStage
    {
        get
        {
            foreach (var stage in RepairStages.Ordered)
            {
                if (_stages[stage] is StageStatus.Active or StageStatus.Pending)
                {
                    return stage;
                }
            }

            return null;
        }
    }

    /// <summary>¿Está autorizada la escritura? Esto es lo que protege al equipo del cliente.</summary>
    public bool IsWriteAuthorized =>
        BackupVerified &&
        SafetyGateApproved &&
        RepairStages.Ordered
            .Where(stage => stage < RepairStage.Repair)
            .All(stage => _stages[stage] == StageStatus.Done);

    public void MarkIdentified(DeviceFingerprint fingerprint)
    {
        Fingerprint = fingerprint;
        _stages[RepairStage.Identify] = StageStatus.Done;
    }

    public void MarkClassified(CoverageAssessment coverage)
    {
        Coverage = coverage;
        _stages[RepairStage.Classify] = StageStatus.Done;
    }

    public void MarkBackupVerified() => BackupVerified = true;

    public void MarkSafetyGateApproved() => SafetyGateApproved = true;

    /// <summary>
    /// Cambia el estado de una etapa validando el orden del pipeline y, en el caso de
    /// la etapa Reparar, la autorización de escritura.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si se intenta saltar una etapa o escribir sin autorización.</exception>
    public void SetStage(RepairStage stage, StageStatus status)
    {
        if (status is StageStatus.Active or StageStatus.Done or StageStatus.Skipped)
        {
            EnsurePrecedence(stage);
        }

        if (stage == RepairStage.Repair && status == StageStatus.Active)
        {
            if (!IsWriteAuthorized)
            {
                throw new InvalidOperationException(
                    "Punto Único de Escritura: la etapa Reparar no puede iniciarse sin respaldo verificado, " +
                    "Safety Gate aprobado y las nueve etapas anteriores completadas.");
            }

            if (RepairAttempts >= MaxRepairAttempts)
            {
                throw new InvalidOperationException(
                    $"Bucle acotado: máximo {MaxRepairAttempts} intentos de reparación por sesión.");
            }
        }

        if (stage == RepairStage.Repair && status == StageStatus.Done)
        {
            RepairAttempts++;
        }

        _stages[stage] = status;
    }

    /// <summary>Resumen legible de la sesión, para la interfaz y el informe.</summary>
    public string Describe()
    {
        var done = _stages.Count(pair => pair.Value == StageStatus.Done);
        var current = CurrentStage;
        return $"Sesión {Id:D} · {done}/{RepairStages.Total} etapas completadas · " +
               $"etapa actual: {(current is null ? "fin" : current.Value.Label())} · " +
               $"escritura {(IsWriteAuthorized ? "AUTORIZADA" : "bloqueada")}";
    }

    private void EnsurePrecedence(RepairStage stage)
    {
        foreach (var prior in RepairStages.Ordered)
        {
            if (prior >= stage)
            {
                break;
            }

            if (_stages[prior] != StageStatus.Done)
            {
                throw new InvalidOperationException(
                    $"Orden del pipeline: la etapa {stage.Label()} requiere que {prior.Label()} esté completada.");
            }
        }
    }
}
