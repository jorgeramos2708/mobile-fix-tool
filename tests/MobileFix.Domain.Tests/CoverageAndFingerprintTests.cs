using MobileFix.Domain;

namespace MobileFix.Domain.Tests;

/// <summary>
/// Semántica de cobertura y de identidad. Determinan si la plataforma puede escribir
/// sobre un equipo antes de que el técnico toque nada (PLAN-MAESTRO §5 y §21.3).
/// </summary>
public sealed class CoverageAndFingerprintTests
{
    [Theory]
    [InlineData(CoverageVerdict.Full, RiskLevel.Destructive, true)]
    [InlineData(CoverageVerdict.Partial, RiskLevel.Destructive, true)]
    [InlineData(CoverageVerdict.ReadOnly, RiskLevel.Safe, false)]
    [InlineData(CoverageVerdict.BlockedByAuthorization, RiskLevel.Safe, false)]
    [InlineData(CoverageVerdict.Unsupported, RiskLevel.Safe, false)]
    public void El_veredicto_de_cobertura_determina_si_se_puede_escribir(
        CoverageVerdict verdict,
        RiskLevel expectedRisk,
        bool expectedWriting)
    {
        var coverage = CoverageAssessment.Evaluate(verdict);

        Assert.Equal(expectedRisk, coverage.AllowedRisk);
        Assert.Equal(expectedWriting, coverage.AllowsWriting);
    }

    [Fact]
    public void Un_veredicto_solo_lectura_nunca_debe_habilitar_la_escritura()
    {
        foreach (var verdict in new[]
                 {
                     CoverageVerdict.ReadOnly,
                     CoverageVerdict.BlockedByAuthorization,
                     CoverageVerdict.Unsupported,
                 })
        {
            var coverage = CoverageAssessment.Evaluate(verdict, "sin ruta legítima disponible");

            Assert.False(coverage.AllowsWriting, $"{verdict} no debería permitir escritura.");
        }
    }

    [Fact]
    public void Las_razones_del_veredicto_se_conservan_para_la_interfaz_y_el_informe()
    {
        var coverage = CoverageAssessment.Evaluate(
            CoverageVerdict.Partial,
            "BROM accesible",
            "requiere auth file legítimo");

        Assert.Equal(2, coverage.Reasons.Count);
        Assert.Contains("BROM accesible", coverage.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Un_conflicto_de_identidad_manda_sobre_el_porcentaje_de_confianza()
    {
        // Caso real y frecuente en LATAM: equipo con placa cambiada o reparación previa.
        var fingerprint = TestSupport.Fingerprint(
            confidencePercent: 99,
            "board ID no coincide con la variante declarada");

        Assert.Equal(IdentityConfidence.Conflict, fingerprint.Confidence);
    }

    [Theory]
    [InlineData(99, IdentityConfidence.High)]
    [InlineData(95, IdentityConfidence.High)]
    [InlineData(94, IdentityConfidence.Medium)]
    [InlineData(70, IdentityConfidence.Medium)]
    [InlineData(69, IdentityConfidence.Low)]
    public void La_confianza_de_identificacion_escala_por_peldanos(int percent, IdentityConfidence expected)
    {
        var fingerprint = TestSupport.Fingerprint(percent);

        Assert.Equal(expected, fingerprint.Confidence);
    }

    [Fact]
    public void La_huella_se_describe_con_modelo_soc_fuente_y_confianza()
    {
        var description = TestSupport.Fingerprint().Describe();

        Assert.Contains("Galaxy A13", description, StringComparison.Ordinal);
        Assert.Contains("SM-A135M", description, StringComparison.Ordinal);
        Assert.Contains("BuildProperties", description, StringComparison.Ordinal);
        Assert.Contains("98%", description, StringComparison.Ordinal);
    }

    [Fact]
    public void La_escalera_de_identificacion_cubre_los_siete_peldanos()
    {
        var peldanos = Enum.GetValues<FingerprintSource>();

        Assert.Equal(7, peldanos.Length);
        Assert.Equal(FingerprintSource.UsbDescriptor, peldanos[0]);
        Assert.Equal(FingerprintSource.Baseband, peldanos[^1]);
    }
}
