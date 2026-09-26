using System.Text;
using MobileFix.Domain;
using MobileFix.Infrastructure;
using MobileFix.Ports;
using MobileFix.UseCases;

namespace MobileFix.Cli;

/// <summary>
/// Autocomprobación de la plataforma. Dos modos:
///
///   mobilefix-check                     valida las invariantes del dominio y del journal
///   mobilefix-check --coverage [ruta]   calcula la matriz de cobertura desde el inventario
///
/// No sustituye a las pruebas unitarias, pero demuestra en un ejecutable que las invariantes que
/// sostienen la seguridad del producto se cumplen de verdad.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Salida redirigida sin consola: irrelevante para la comprobación.
        }

        if (args.Length > 0 && args[0] is "--coverage" or "-c")
        {
            return RunCoverage(args.Length > 1 ? args[1] : FindDefaultInventory());
        }

        return RunInvariants();
    }

    // ------------------------------------------------------------------ modo cobertura

    private static int RunCoverage(string? path)
    {
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("MobileFix Tool - matriz de cobertura del banco");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();

        if (path is null || !File.Exists(path))
        {
            Console.WriteLine("No se encontró el inventario.");
            Console.WriteLine("Uso: mobilefix-check --coverage <ruta al csv>");
            Console.WriteLine("Plantilla y datos de ejemplo: docs/inventario-demo.csv");
            return 2;
        }

        var coverage = new BenchCoverageService(new InventoryCsvReader()).Load(path);
        var statistics = coverage.Statistics;

        Console.WriteLine($"Archivo:     {coverage.SourcePath}");
        Console.WriteLine($"Procedencia: {statistics.Provenance}");

        if (coverage.IsProvisional)
        {
            Console.WriteLine();
            Console.WriteLine("  ################################################################");
            Console.WriteLine("  #  PROVISIONAL: calculado con datos de ejemplo, no medidos.      #");
            Console.WriteLine("  #  No usar para decidir alcance ni para afirmar cobertura.      #");
            Console.WriteLine("  ################################################################");
        }

        if (coverage.Dataset.RejectedRows.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Filas descartadas: {coverage.Dataset.RejectedRows.Count}");
            foreach (var rejected in coverage.Dataset.RejectedRows)
            {
                Console.WriteLine($"  - {rejected}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Equipos por mecanismo de acceso");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"  {"Mecanismo",-24} {"Equipos",8} {"Con credencial",16} {"% del banco",12}");

        foreach (var mechanism in statistics.ByMechanism.Where(stats => stats.HasEvidence))
        {
            var percent = statistics.Total == 0 ? 0d : (double)mechanism.Devices / statistics.Total;
            Console.WriteLine($"  {mechanism.Mechanism,-24} {mechanism.Devices,8} {mechanism.RequiringCredentials,16} {percent,11:P0}");
        }

        Console.WriteLine();
        Console.WriteLine("Nivel más alto alcanzado (escalera L0-L6)");
        Console.WriteLine(new string('-', 78));
        foreach (var level in statistics.ByLevel.Where(stats => stats.Devices > 0))
        {
            Console.WriteLine($"  {level.Level,-6} {level.Devices,4} equipos");
        }

        Console.WriteLine();
        Console.WriteLine("Cobertura por nivel de intervención");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"  {"Nivel",-18} {"Sí",5} {"Parcial",8} {"No",5} {"N/A",5} {"Alcanzable",12}");

        foreach (var intervention in statistics.ByIntervention)
        {
            Console.WriteLine(
                $"  {intervention.Name,-18} {intervention.Yes,5} {intervention.Partial,8} " +
                $"{intervention.No,5} {intervention.NotApplicable,5} {intervention.ReachablePercent,11:P0}");
        }

        Console.WriteLine();
        Console.WriteLine("KPI del hito M0");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"  Equipos fichados ......................... {statistics.Total,5}   (objetivo: 150-300)");
        Console.WriteLine($"  Mecanismos con al menos 1 equipo ......... {statistics.MechanismsWithEvidence,5}   (objetivo M0: 4)");
        Console.WriteLine($"  Identificados a nivel de modelo o más .... {statistics.IdentifiedAtModelLevelPercent,10:P0}   (objetivo M2: 85%)");

        var t2 = statistics.Intervention("T2 arranque");
        var t3 = statistics.Intervention("T3 particiones");
        Console.WriteLine($"  Reparación de arranque alcanzable ........ {(t2?.ReachablePercent ?? 0d),10:P0}   (objetivo M5: 50% en «sí»)");
        Console.WriteLine($"  Reparación de particiones alcanzable ..... {(t3?.ReachablePercent ?? 0d),10:P0}   (objetivo M7: 70% en «sí»)");

        Console.WriteLine();
        Console.WriteLine("Resolución de veredictos (¿qué puedo hacer con este equipo?)");
        Console.WriteLine(new string('-', 78));

        foreach (var fingerprint in SampleFingerprints())
        {
            var assessment = coverage.Resolve(fingerprint);
            var provisional = assessment.IsProvisional ? "  [PROVISIONAL]" : string.Empty;
            Console.WriteLine(
                $"  {fingerprint.SocVendor,-22} {fingerprint.Model,-22} -> {assessment.Verdict,-20} " +
                $"escritura: {(assessment.AllowsWriting ? "sí" : "no"),-3}{provisional}");

            foreach (var reason in assessment.Reasons.Take(2))
            {
                Console.WriteLine($"      · {reason}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(coverage.Describe());
        return 0;
    }

    private static IEnumerable<DeviceFingerprint> SampleFingerprints()
    {
        yield return Fingerprint("MediaTek", "Galaxy A13");
        yield return Fingerprint("Qualcomm", "Moto G52");
        yield return Fingerprint("Unisoc", "Spark 10");
        yield return Fingerprint("Exynos", "Galaxy A53 5G");
        yield return Fingerprint("Apple", "iPhone 12");
        yield return Fingerprint("Ficticio Semiconductor", "Modelo Z");
    }

    private static DeviceFingerprint Fingerprint(string socVendor, string model) => new()
    {
        SocVendor = socVendor,
        SocModel = "leído en el handshake",
        Oem = "fabricante",
        Model = model,
        ConfidencePercent = 96,
        HighestSource = FingerprintSource.BuildProperties,
    };

    private static string? FindDefaultInventory()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "inventario-demo.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "inventario-demo.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "inventario-demo.csv"),
        };

        // En desarrollo, subir desde bin/Debug hasta la raíz del repositorio.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "docs", "inventario-demo.csv"));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    // ------------------------------------------------------------------ modo invariantes

    private static int RunInvariants()
    {
        var results = new List<(string Name, bool Ok, string Detail)>();

        Console.WriteLine(new string('=', 78));
        Console.WriteLine("MobileFix Tool - autocomprobacion de la plataforma");
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
            service.Identify(blockedSession, Fingerprint("MediaTek", "Galaxy A13"));
            service.Classify(blockedSession, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial, CoverageEvidence.SeededDemo, "mecanismo accesible en modo solo lectura"));
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

        if (target < 0)
        {
            results.Add(("Manipulacion detectada", false, "no se encontro una entrada adecuada para manipular"));
        }
        else
        {
            lines[target] = lines[target].Replace("stage.done", "stage.alterado", StringComparison.Ordinal);
            File.WriteAllLines(tamperPath, lines);

            var tamperedVerification = new FileJournal(tamperPath, clock).VerifyChain();
            results.Add(("Manipulacion detectada", !tamperedVerification.IsValid, tamperedVerification.Message));
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
        Console.WriteLine();
        Console.WriteLine("Para la matriz de cobertura:  mobilefix-check --coverage docs/inventario-demo.csv");

        return failed == 0 ? 0 : 1;
    }

    private static string? RunFullSession(RepairSessionService service, RepairSession session)
    {
        try
        {
            service.Connect(session, "USB 2.0 - puerto 3 del hub 1 (simulado)");
            Print(RepairStage.Connect, session);

            service.Identify(session, Fingerprint("MediaTek", "Galaxy A13"));
            Print(RepairStage.Identify, session);

            service.Classify(session, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial,
                CoverageEvidence.SeededDemo,
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
