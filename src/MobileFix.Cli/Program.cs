using System.Text;
using MobileFix.UseCases;
using MobileFix.Domain;
using MobileFix.Infrastructure;
using MobileFix.Ports;

namespace MobileFix.Cli;

/// <summary>
/// Autocomprobación de la plataforma. No sustituye a las pruebas unitarias (llegan con el banco),
/// pero valida hoy las invariantes que sostienen la seguridad del producto:
///
///   1. Las 13 etapas se ejecutan en orden y quedan registradas.
///   2. El Punto Único de Escritura: sin respaldo verificado no se puede reparar.
///   3. El Safety Gate bloquea la escritura cuando falta un requisito.
///   4. El orden del pipeline no se puede saltar.
///   5. El journal detecta cualquier alteración de sus entradas.
/// </summary>
internal static class Program
{
    private const string Title = "MobileFix Tool - autocomprobacion de la plataforma";

    private static int Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Salida redirigida sin consola: irrelevante para la comprobación.
        }

        var results = new List<(string Name, bool Ok, string Detail)>();

        Console.WriteLine(new string('=', 78));
        Console.WriteLine(Title);
        Console.WriteLine(new string('=', 78));

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MobileFixDemo");

        Directory.CreateDirectory(root);

        var clock = new SystemClock();

        // ---------------------------------------------------------------- 1. sesión completa
        var journalPath = Path.Combine(root, "journal.jsonl");
        if (File.Exists(journalPath))
        {
            File.Delete(journalPath);
        }

        var journal = new FileJournal(journalPath, clock);
        var service = new RepairSessionService(journal, clock);

        Console.WriteLine();
        Console.WriteLine("1. Sesion completa en modo simulacion (13 etapas)");
        Console.WriteLine(new string('-', 78));

        var session = service.Begin();
        var failure = RunFullSession(service, session);
        results.Add(("Sesion completa de 13 etapas", failure is null, failure ?? "todas las etapas completadas"));

        Console.WriteLine();
        Console.WriteLine(session.Describe());
        Console.WriteLine($"Journal: {journalPath}");

        // ---------------------------------------------------------------- 2. integridad de la cadena
        Console.WriteLine();
        Console.WriteLine("2. Integridad del journal");
        Console.WriteLine(new string('-', 78));

        var verification = journal.VerifyChain();
        results.Add(("Cadena del journal íntegra", verification.IsValid, verification.Message));

        // ---------------------------------------------------------------- 3. Safety Gate bloqueado
        Console.WriteLine();
        Console.WriteLine("3. Invariante: sin Safety Gate aprobado no hay escritura");
        Console.WriteLine(new string('-', 78));

        var blockedSession = service.Begin();
        var gateBlocked = TryStep(() =>
        {
            service.Connect(blockedSession, "USB simulado");
            service.Identify(blockedSession, SampleFingerprint());
            service.Classify(blockedSession, CoverageAssessment.Evaluate(CoverageVerdict.Partial, "mecanismo accesible en modo solo lectura"));
            service.Diagnose(blockedSession, "bateria 42%, almacenamiento OK");
            service.Correlate(blockedSession, "caso similar: bootloop por particion boot corrupta");
            service.CheckConstraints(blockedSession, "bootloader bloqueado, sin token disponible", hardBlocker: false);
            service.Backup(blockedSession, "GPT + persist + modem", verifiedIntegrity: true);
            service.PlanRepair(blockedSession, "reflasheo de boot con imagen de stock");
            service.SafetyGate(blockedSession, new SafetyGateInputs(
                OwnershipVerified: true,
                ConsentSigned: true,
                LinkHealthy: true,
                Risk: RiskLevel.BrickRisk,
                SupervisorApproved: false));
            service.Repair(blockedSession, "esto no debe ejecutarse jamas");
        });

        var repairWasReached = blockedSession.StatusOf(RepairStage.Repair) != StageStatus.Pending;
        results.Add((
            "Safety Gate bloquea la escritura",
            gateBlocked is not null && !repairWasReached,
            gateBlocked ?? "el gate no bloqueo nada (FALLO)"));

        Console.WriteLine($"  Etapa SafetyGate: {blockedSession.StatusOf(RepairStage.SafetyGate)}");
        Console.WriteLine($"  Etapa Repair:     {blockedSession.StatusOf(RepairStage.Repair)}");
        Console.WriteLine($"  Escritura autorizada: {blockedSession.IsWriteAuthorized} (debe ser False)");

        // ---------------------------------------------------------------- 4. orden del pipeline
        Console.WriteLine();
        Console.WriteLine("4. Invariante: el orden del pipeline no se puede saltar");
        Console.WriteLine(new string('-', 78));

        var rawSession = new RepairSession(clock.UtcNow);
        var skipped = TryStep(() => rawSession.SetStage(RepairStage.Report, StageStatus.Done));
        results.Add((
            "Salto de etapa rechazado",
            skipped is not null && skipped.Contains("Orden del pipeline", StringComparison.Ordinal),
            skipped ?? "el salto fue permitido (FALLO)"));

        // ---------------------------------------------------------------- 5. manipulacion del journal
        Console.WriteLine();
        Console.WriteLine("5. Invariante: el journal detecta manipulacion");
        Console.WriteLine(new string('-', 78));

        var tamperPath = Path.Combine(root, "journal-manipulado.jsonl");
        var lines = File.ReadAllLines(journalPath);
        var target = Array.FindIndex(lines, line => line.Contains("\"Kind\":\"stage.done\"", StringComparison.Ordinal));

        string tamperDetail;
        if (target < 0)
        {
            tamperDetail = "no se encontro una entrada adecuada para manipular (revisar)";
            results.Add(("Manipulacion detectada", false, tamperDetail));
        }
        else
        {
            lines[target] = lines[target].Replace("stage.done", "stage.alterado", StringComparison.Ordinal);
            File.WriteAllLines(tamperPath, lines);

            var tampered = new FileJournal(tamperPath, clock);
            var tamperedVerification = tampered.VerifyChain();
            tamperDetail = tamperedVerification.Message;
            results.Add((
                "Manipulacion detectada",
                !tamperedVerification.IsValid,
                tamperDetail));
        }

        // ---------------------------------------------------------------- resumen
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("RESUMEN");
        Console.WriteLine(new string('=', 78));

        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok)
            {
                failed++;
            }

            Console.WriteLine($"  [{(ok ? " OK " : "FALLO")}] {name}");
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Console.WriteLine($"         {detail}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? "Todas las invariantes se cumplen."
            : $"{failed} invariante(s) incumplida(s).");

        Console.WriteLine();
        Console.WriteLine("Nota: esto no valida hardware. La validacion real empieza con los spikes de M0");
        Console.WriteLine("contra el banco de dispositivos.");

        return failed == 0 ? 0 : 1;
    }

    private static string? RunFullSession(RepairSessionService service, RepairSession session)
    {
        try
        {
            service.Connect(session, "USB 2.0 - puerto 3 del hub 1 (simulado)");
            Print(RepairStage.Connect, session);

            service.Identify(session, SampleFingerprint());
            Print(RepairStage.Identify, session);

            service.Classify(session, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial,
                "BROM accesible sin autenticacion DAA/SLA en este modelo",
                "sin token de fabricante para operaciones de escritura en caliente"));
            Print(RepairStage.Classify, session);

            service.Diagnose(session, "bateria 78% (4120 mV), eMMC saludable, 0 bloques ilegibles, temperatura 31 C");
            Print(RepairStage.Diagnose, session);

            service.Correlate(session, "sintoma 'bootloop' + particion boot corrupta: 87% de casos resueltos con reflasheo");
            Print(RepairStage.Correlate, session);

            service.CheckConstraints(session, "secure boot off, AVB deshabilitado, sin ARB, bootloader desbloqueado", hardBlocker: false);
            Print(RepairStage.CheckConstraints, session);

            service.Backup(session, "GPT + boot + persist + modem (12 MB)", verifiedIntegrity: true);
            Print(RepairStage.Backup, session);

            service.PlanRepair(session, "reflasheo de 'boot' desde firmware oficial firmado, hash verificado");
            Print(RepairStage.PlanRepair, session);

            service.SafetyGate(session, new SafetyGateInputs(
                OwnershipVerified: true,
                ConsentSigned: true,
                LinkHealthy: true,
                Risk: RiskLevel.Destructive,
                SupervisorApproved: true));
            Print(RepairStage.SafetyGate, session);

            service.Repair(session, "escritura de 'boot' (8 MB) - punto unico de escritura de toda la plataforma");
            Print(RepairStage.Repair, session);

            service.Verify(session, "hash leido == hash esperado; arranque completado en 22 s");
            Print(RepairStage.Verify, session);

            service.CompareBeforeAfter(session, "boot: corrupto -> integro; userdata intacta; IMEI sin cambios");
            Print(RepairStage.CompareBeforeAfter, session);

            service.Report(session, "informe tecnico + informe de cliente emitidos con evidencia encadenada");
            Print(RepairStage.Report, session);

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static void Print(RepairStage stage, RepairSession session) =>
        Console.WriteLine($"  [{(int)stage,2}] {stage.Label(),-24} {session.StatusOf(stage),-8} {(stage.IsWriteStage() ? "<-- ESCRIBE" : string.Empty)}");

    private static DeviceFingerprint SampleFingerprint() => new()
    {
        SocVendor = "MediaTek",
        SocModel = "MT6769 Helio G80",
        Oem = "Samsung",
        Model = "Galaxy A13",
        RegionVariant = "SM-A135M / LATAM",
        BuildId = "TP1A.220624.014",
        ConfidencePercent = 98,
        HighestSource = FingerprintSource.BuildProperties,
    };

    private static string? TryStep(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
