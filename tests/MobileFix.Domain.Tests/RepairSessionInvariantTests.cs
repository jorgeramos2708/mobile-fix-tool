using MobileFix.Domain;
using MobileFix.Infrastructure;
using MobileFix.Ports;
using MobileFix.UseCases;

namespace MobileFix.Domain.Tests;

/// <summary>
/// Invariantes del Punto Único de Escritura y del orden del pipeline.
///
/// Estas pruebas son la garantía técnica de que un técnico no puede dejar inservible el equipo
/// de un cliente por saltarse un paso. La restricción vive en el dominio, no en la interfaz:
/// si alguien la quita de aquí, las pruebas se ponen en rojo.
/// </summary>
public sealed class RepairSessionInvariantTests
{
    [Fact]
    public void Solo_la_etapa_Reparar_puede_escribir()
    {
        var writing = RepairStages.Ordered.Where(stage => stage.IsWriteStage()).ToArray();

        Assert.Single(writing);
        Assert.Equal(RepairStage.Repair, writing[0]);

        // Ninguna de las nueve etapas previas escribe: son no destructivas por construcción.
        Assert.All(
            RepairStages.Ordered.Where(stage => stage < RepairStage.Repair),
            stage => Assert.False(stage.IsWriteStage(), $"{stage.Label()} no debería escribir."));
    }

    [Fact]
    public void Las_etapas_se_clasifican_en_lectura_escritura_y_sin_acceso_al_dispositivo()
    {
        // Cómputo puro: no tocan el dispositivo. Planificar, autorizar y reportar.
        Assert.Equal(
            new[] { RepairStage.PlanRepair, RepairStage.SafetyGate, RepairStage.Report },
            RepairStages.Ordered.Where(stage => stage.IsOffDevice()));

        // Leen del dispositivo y nunca escriben.
        Assert.Equal(
            new[]
            {
                RepairStage.Connect, RepairStage.Identify, RepairStage.Classify, RepairStage.Diagnose,
                RepairStage.Correlate, RepairStage.CheckConstraints, RepairStage.Backup,
                RepairStage.Verify, RepairStage.CompareBeforeAfter,
            },
            RepairStages.Ordered.Where(stage => stage.IsReadOnly()));

        // Escribe: una sola.
        Assert.Equal(
            new[] { RepairStage.Repair },
            RepairStages.Ordered.Where(stage => stage.IsWriteStage()));

        // Las tres categorías cubren el pipeline completo, una sola vez cada etapa.
        Assert.All(RepairStages.Ordered, stage => Assert.Equal(
            1,
            (stage.IsOffDevice() ? 1 : 0) + (stage.IsReadOnly() ? 1 : 0) + (stage.IsWriteStage() ? 1 : 0)));
    }

    [Fact]
    public void El_pipeline_tiene_13_etapas_en_orden_sin_huecos()
    {
        Assert.Equal(13, RepairStages.Total);
        Assert.Equal(RepairStages.Total, RepairStages.Ordered.Count);
        Assert.Equal(Enumerable.Range(1, 13), RepairStages.Ordered.Select(stage => (int)stage));
    }

    [Fact]
    public void No_se_puede_reparar_sin_respaldo_verificado_ni_safety_gate()
    {
        var session = new RepairSession(TestSupport.T0);

        // Todas las etapas 1..9 marcadas como completadas, pero sin los dos flags que importan.
        session.CompleteStages(RepairStage.Connect, RepairStage.SafetyGate);

        Assert.False(session.IsWriteAuthorized);

        var exception = Assert.Throws<InvalidOperationException>(
            () => session.SetStage(RepairStage.Repair, StageStatus.Active));

        Assert.Contains("Punto Único de Escritura", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_respaldo_verificado_y_gate_aprobado_la_escritura_se_autoriza()
    {
        var session = TestSupport.AuthorizedSession();

        Assert.True(session.IsWriteAuthorized);

        session.SetStage(RepairStage.Repair, StageStatus.Active);
        Assert.Equal(StageStatus.Active, session.StatusOf(RepairStage.Repair));

        session.SetStage(RepairStage.Repair, StageStatus.Done);
        Assert.Equal(1, session.RepairAttempts);
    }

    [Theory]
    [InlineData(RepairStage.Backup)]
    [InlineData(RepairStage.Repair)]
    [InlineData(RepairStage.Report)]
    public void No_se_puede_completar_una_etapa_saltandose_las_anteriores(RepairStage stage)
    {
        var session = new RepairSession(TestSupport.T0);

        var exception = Assert.Throws<InvalidOperationException>(
            () => session.SetStage(stage, StageStatus.Done));

        Assert.Contains("Orden del pipeline", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void El_bucle_de_reparacion_es_acotado()
    {
        var session = TestSupport.AuthorizedSession();

        for (var attempt = 0; attempt < RepairSession.MaxRepairAttempts; attempt++)
        {
            session.SetStage(RepairStage.Repair, StageStatus.Active);
            session.SetStage(RepairStage.Repair, StageStatus.Done);
        }

        Assert.Equal(RepairSession.MaxRepairAttempts, session.RepairAttempts);

        var exception = Assert.Throws<InvalidOperationException>(
            () => session.SetStage(RepairStage.Repair, StageStatus.Active));

        Assert.Contains("Bucle acotado", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_etapa_que_falla_se_marca_como_fallida_y_no_autoriza_la_escritura()
    {
        var journal = new FileJournal(TestSupport.NewJournalPath(), TestSupport.NewClock());
        var service = new RepairSessionService(journal, TestSupport.NewClock());
        var session = service.Begin();

        service.Connect(session, "USB simulado");
        service.Identify(session, TestSupport.Fingerprint());
        service.Classify(session, CoverageAssessment.Evaluate(
            CoverageVerdict.Partial, CoverageEvidence.SeededDemo, "parcial"));
        service.Diagnose(session, "batería OK");
        service.Correlate(session, "sin correlación relevante");
        service.CheckConstraints(session, "sin bloqueos duros", hardBlocker: false);
        service.Backup(session, "GPT + persist", verifiedIntegrity: true);
        service.PlanRepair(session, "reflasheo de boot");

        // Propiedad no verificada y consentimiento no firmado: el gate debe bloquear.
        Assert.Throws<InvalidOperationException>(() => service.SafetyGate(
            session,
            new SafetyGateInputs(
                OwnershipVerified: false,
                ConsentSigned: false,
                LinkHealthy: true,
                Risk: RiskLevel.Safe,
                SupervisorApproved: true)));

        Assert.Equal(StageStatus.Failed, session.StatusOf(RepairStage.SafetyGate));
        Assert.False(session.IsWriteAuthorized);

        // El fallo queda registrado: no se silencia.
        Assert.Contains(journal.ReadAll(), entry => entry.Kind == "stage.failed");
    }

    [Fact]
    public void El_safety_gate_exige_supervisor_cuando_el_riesgo_es_de_brick()
    {
        var inputs = new SafetyGateInputs(
            OwnershipVerified: true,
            ConsentSigned: true,
            LinkHealthy: true,
            Risk: RiskLevel.BrickRisk,
            SupervisorApproved: false);

        var blockers = inputs.Blockers();

        Assert.Single(blockers);
        Assert.Contains("supervisor", blockers[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Un_respaldo_sin_integridad_verificada_no_habilita_la_escritura()
    {
        var journal = new FileJournal(TestSupport.NewJournalPath(), TestSupport.NewClock());
        var service = new RepairSessionService(journal, TestSupport.NewClock());
        var session = service.Begin();

        service.Connect(session, "USB simulado");
        service.Identify(session, TestSupport.Fingerprint());
        service.Classify(session, CoverageAssessment.Evaluate(
            CoverageVerdict.Full, CoverageEvidence.SeededDemo));
        service.Diagnose(session, "OK");
        service.Correlate(session, "OK");
        service.CheckConstraints(session, "OK", hardBlocker: false);

        Assert.Throws<InvalidOperationException>(
            () => service.Backup(session, "GPT", verifiedIntegrity: false));

        Assert.False(session.BackupVerified);
        Assert.False(session.IsWriteAuthorized);
    }

    [Fact]
    public void Cada_etapa_registra_inicio_y_fin_en_el_journal()
    {
        var journal = new FileJournal(TestSupport.NewJournalPath(), TestSupport.NewClock());
        var service = new RepairSessionService(journal, TestSupport.NewClock());
        var session = service.Begin();

        service.Connect(session, "USB simulado");

        var entries = journal.ReadAll();
        var kinds = entries.Select(entry => entry.Kind).ToArray();

        Assert.Equal(new[] { "session.begin", "stage.start", "stage.done" }, kinds);
        Assert.All(entries, entry => Assert.Equal(session.Id, entry.SessionId));
        Assert.All(entries, entry => Assert.Equal(TestSupport.T0, entry.TimestampUtc));
    }
}
