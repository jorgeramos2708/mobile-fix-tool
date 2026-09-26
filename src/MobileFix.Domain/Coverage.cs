namespace MobileFix.Domain;

/// <summary>Veredicto de cobertura (PLAN-MAESTRO §5, DESIGN-TOKENS §3.5).</summary>
public enum CoverageVerdict
{
    Full,
    Partial,
    ReadOnly,
    BlockedByAuthorization,
    Unsupported,
}

/// <summary>
/// Nivel de riesgo de una operación (DESIGN-TOKENS §3.1). La escala es ordenada:
/// Safe &lt; Caution &lt; Destructive &lt; BrickRisk.
/// </summary>
public enum RiskLevel
{
    Safe = 0,
    Caution = 1,
    Destructive = 2,
    BrickRisk = 3,
}

/// <summary>
/// Matriz de cobertura aplicada a un dispositivo concreto. Decide qué se puede hacer
/// antes de que el técnico toque nada, y degrada la sesión a solo-lectura si no hay ruta.
/// </summary>
public sealed record CoverageAssessment
{
    public required CoverageVerdict Verdict { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required RiskLevel AllowedRisk { get; init; }

    public bool AllowsWriting => AllowedRisk != RiskLevel.Safe;

    public static CoverageAssessment Evaluate(CoverageVerdict verdict, params string[] reasons)
    {
        var allowed = verdict switch
        {
            CoverageVerdict.Full => RiskLevel.Destructive,
            CoverageVerdict.Partial => RiskLevel.Destructive,
            _ => RiskLevel.Safe,
        };

        return new CoverageAssessment
        {
            Verdict = verdict,
            Reasons = reasons,
            AllowedRisk = allowed,
        };
    }

    public string Describe() => Reasons.Count == 0
        ? $"{Verdict} · riesgo máximo permitido {AllowedRisk}"
        : $"{Verdict} · riesgo máximo permitido {AllowedRisk} · {string.Join("; ", Reasons)}";
}
