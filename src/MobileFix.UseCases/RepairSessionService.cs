using MobileFix.Domain;
using MobileFix.Ports;

namespace MobileFix.UseCases;

/// <summary>
/// Datos que el Safety Gate evalúa antes de permitir cualquier escritura (PLAN-MAESTRO §2, etapa 9).
/// </summary>
public sealed record SafetyGateInputs(
    bool OwnershipVerified,
    bool ConsentSigned,
    bool LinkHealthy,
    RiskLevel Risk,
    bool SupervisorApproved)
{
    /// <summary>Motivos de bloqueo. Un solo motivo basta para detener la sesión.</summary>
    public IReadOnlyList<string> Blockers()
    {
        var blockers = new List<string>();

        if (!OwnershipVerified)
        {
            blockers.Add("propiedad del equipo no verificada");
        }

        if (!ConsentSigned)
        {
            blockers.Add("consentimiento del cliente no firmado");
        }

        if (!LinkHealthy)
        {
            blockers.Add("enlace no apto para escritura");
        }

        if (Risk == RiskLevel.BrickRisk && !SupervisorApproved)
        {
            blockers.Add("riesgo de brick sin aprobación de supervisor");
        }

        return blockers;
    }

    public string Describe() =>
        $"propiedad={(OwnershipVerified ? "verificada" : "NO")} · " +
        $"consentimiento={(ConsentSigned ? "firmado" : "NO")} · " +
        $"enlace={(LinkHealthy ? "apto" : "NO APTO")} · riesgo={Risk}";
}

/// <summary>
/// Orquestador de las 13 etapas.
///
/// Reglas que aplica y que no se pueden eludir desde la interfaz:
/// 1. Cada etapa escribe en el journal antes y después de ejecutarse.
/// 2. Una etapa que falla se marca como fallida: nunca como éxito.
/// 3. Sin respaldo verificado y sin Safety Gate aprobado, no hay escritura.
/// </summary>
public sealed class RepairSessionService(IJournal journal, IClock clock)
{
    private readonly IJournal _journal = journal;
    private readonly IClock _clock = clock;

    public RepairSession Begin()
    {
        var session = new RepairSession(_clock.UtcNow);
        _journal.Append(session.Id, "session.begin", "session", $"Inicio de sesión {_clock.UtcNow:O}");
        return session;
    }

    public void Connect(RepairSession session, string transportDescription) =>
        Run(session, RepairStage.Connect, transportDescription, _ => { });

    public void Identify(RepairSession session, DeviceFingerprint fingerprint) =>
        Run(session, RepairStage.Identify, fingerprint.Describe(), s => s.MarkIdentified(fingerprint));

    public void Classify(RepairSession session, CoverageAssessment coverage) =>
        Run(session, RepairStage.Classify, coverage.Describe(), s => s.MarkClassified(coverage));

    public void Diagnose(RepairSession session, string summary) =>
        Run(session, RepairStage.Diagnose, summary, _ => { });

    public void Correlate(RepairSession session, string summary) =>
        Run(session, RepairStage.Correlate, summary, _ => { });

    public void CheckConstraints(RepairSession session, string summary, bool hardBlocker) =>
        Run(session, RepairStage.CheckConstraints, summary, _ =>
        {
            if (hardBlocker)
            {
                throw new InvalidOperationException(
                    "Restricción dura: no hay ruta legítima para esta operación con el material disponible.");
            }
        });

    public void Backup(RepairSession session, string scope, bool verifiedIntegrity) =>
        Run(session, RepairStage.Backup, $"{scope} · integridad={(verifiedIntegrity ? "verificada" : "NO")}", s =>
        {
            if (!verifiedIntegrity)
            {
                throw new InvalidOperationException(
                    "El respaldo no pasó la verificación de integridad: la sesión no podrá escribir.");
            }

            s.MarkBackupVerified();
        });

    public void PlanRepair(RepairSession session, string planSummary) =>
        Run(session, RepairStage.PlanRepair, planSummary, _ => { });

    public void SafetyGate(RepairSession session, SafetyGateInputs inputs) =>
        Run(session, RepairStage.SafetyGate, inputs.Describe(), s =>
        {
            var blockers = inputs.Blockers();
            if (blockers.Count > 0)
            {
                throw new InvalidOperationException("Safety Gate bloqueado: " + string.Join("; ", blockers) + ".");
            }

            s.MarkSafetyGateApproved();
        });

    public void Repair(RepairSession session, string operation) =>
        Run(session, RepairStage.Repair, operation, _ => { });

    public void Verify(RepairSession session, string result) =>
        Run(session, RepairStage.Verify, result, _ => { });

    public void CompareBeforeAfter(RepairSession session, string delta) =>
        Run(session, RepairStage.CompareBeforeAfter, delta, _ => { });

    public void Report(RepairSession session, string summary) =>
        Run(session, RepairStage.Report, summary, _ => { });

    /// <summary>
    /// Ejecuta una etapa con registro antes y después. Si la etapa falla, queda marcada como
    /// fallida y la excepción se propaga: el fallo se registra, no se silencia.
    /// </summary>
    private void Run(RepairSession session, RepairStage stage, string payload, Action<RepairSession> effect)
    {
        session.SetStage(stage, StageStatus.Active);
        _journal.Append(session.Id, "stage.start", stage.ToString(), payload);

        try
        {
            effect(session);
        }
        catch (Exception ex)
        {
            session.SetStage(stage, StageStatus.Failed);
            _journal.Append(session.Id, "stage.failed", stage.ToString(), ex.Message);
            throw;
        }

        session.SetStage(stage, StageStatus.Done);
        _journal.Append(session.Id, "stage.done", stage.ToString(), "OK");
    }
}
