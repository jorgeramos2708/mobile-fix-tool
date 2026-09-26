using MobileFix.Domain;
using MobileFix.Infrastructure;

namespace MobileFix.Domain.Tests;

/// <summary>Utilidades compartidas por las pruebas. Reloj y rutas siempre deterministas.</summary>
internal static class TestSupport
{
    /// <summary>Instante fijo de referencia: 26 de septiembre de 2026, 10:00 UTC.</summary>
    public static readonly DateTimeOffset T0 = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    public static ManualClock NewClock() => new(T0);

    /// <summary>Ruta única de journal en un directorio temporal propio de la prueba.</summary>
    public static string NewJournalPath([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "mobilefix-tests",
            $"{testName}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "journal.jsonl");
    }

    /// <summary>Marca como completadas todas las etapas desde <paramref name="from"/> hasta <paramref name="to"/>, inclusive.</summary>
    public static void CompleteStages(this RepairSession session, RepairStage from, RepairStage to)
    {
        foreach (var stage in RepairStages.Ordered)
        {
            if (stage < from)
            {
                continue;
            }

            if (stage > to)
            {
                break;
            }

            session.SetStage(stage, StageStatus.Done);
        }
    }

    /// <summary>Deja la sesión autorizada para escribir, pasando por los mecanismos legítimos.</summary>
    public static RepairSession AuthorizedSession()
    {
        var session = new RepairSession(T0);
        session.CompleteStages(RepairStage.Connect, RepairStage.SafetyGate);
        session.MarkBackupVerified();
        session.MarkSafetyGateApproved();
        return session;
    }

    public static DeviceFingerprint Fingerprint(
        int confidencePercent = 98,
        params string[] conflicts) => new()
    {
        SocVendor = "MediaTek",
        SocModel = "MT6769 Helio G80",
        Oem = "Samsung",
        Model = "Galaxy A13",
        RegionVariant = "SM-A135M / LATAM",
        BuildId = "TP1A.220624.014",
        ConfidencePercent = confidencePercent,
        HighestSource = FingerprintSource.BuildProperties,
        Conflicts = conflicts,
    };
}
