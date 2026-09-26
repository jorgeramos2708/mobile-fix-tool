namespace MobileFix.Domain;

/// <summary>
/// De dónde sale el veredicto de cobertura. Es la diferencia entre una afirmación y una promesa:
/// con datos sembrados, el veredicto es provisional y debe mostrarse como tal.
/// </summary>
public enum CoverageEvidence
{
    /// <summary>Medido con equipos reales del banco.</summary>
    BenchMeasured,

    /// <summary>Calculado con datos de ejemplo: la cobertura no está demostrada.</summary>
    SeededDemo,

    /// <summary>Sin evidencia alguna.</summary>
    None,
}

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

    /// <summary>
    /// Procedencia del dato que sostiene el veredicto. Es obligatorio declararla: un veredicto
    /// sin evidencia declarada es un veredicto sin valor.
    /// </summary>
    public required CoverageEvidence Evidence { get; init; }

    public bool AllowsWriting => AllowedRisk != RiskLevel.Safe;

    /// <summary>Verdadero si el veredicto no está medido en el banco. La interfaz debe mostrarlo.</summary>
    public bool IsProvisional => Evidence != CoverageEvidence.BenchMeasured;

    public static CoverageAssessment Evaluate(CoverageVerdict verdict, CoverageEvidence evidence, params string[] reasons)
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
            Evidence = evidence,
        };
    }

    public string Describe()
    {
        var provisional = IsProvisional ? " · PROVISIONAL" : string.Empty;
        var detail = Reasons.Count == 0 ? string.Empty : $" · {string.Join("; ", Reasons)}";
        return $"{Verdict} · riesgo máximo permitido {AllowedRisk}{provisional}{detail}";
    }
}
