using MobileFix.Domain;
using MobileFix.Domain.Inventory;
using MobileFix.Ports;

namespace MobileFix.UseCases;

/// <summary>
/// Cobertura calculada desde el banco de pruebas: la matriz y la resolución por dispositivo.
/// Es la respuesta a la pregunta del técnico: «¿qué puedo hacer con este equipo?», antes de tocarlo.
/// </summary>
public sealed record BenchCoverage
{
    public required InventoryDataset Dataset { get; init; }

    public required InventoryStatistics Statistics { get; init; }

    public required CoverageResolver Resolver { get; init; }

    public bool IsProvisional => Dataset.IsProvisional;

    public string? SourcePath => Dataset.SourcePath;

    public CoverageAssessment Resolve(DeviceFingerprint fingerprint) => Resolver.Resolve(fingerprint);

    public string Describe()
    {
        var provisional = IsProvisional ? " · PROVISIONAL (datos de ejemplo, no mediciones)" : string.Empty;

        return $"Banco: {Statistics.Total} equipos · {Statistics.MechanismsWithEvidence} mecanismos con evidencia · " +
               $"identificados a nivel de modelo o superior: {Statistics.IdentifiedAtModelLevelPercent:P0}{provisional}";
    }
}

/// <summary>Carga el inventario y calcula la matriz de cobertura.</summary>
public sealed class BenchCoverageService(IInventorySource source)
{
    private readonly IInventorySource _source = source ?? throw new ArgumentNullException(nameof(source));

    public BenchCoverage Load(string path) => From(_source.LoadFile(path));

    public static BenchCoverage From(InventoryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        return new BenchCoverage
        {
            Dataset = dataset,
            Statistics = InventoryStatistics.From(dataset),
            Resolver = new CoverageResolver(dataset),
        };
    }
}
