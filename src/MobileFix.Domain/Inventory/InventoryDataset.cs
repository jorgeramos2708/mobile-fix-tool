namespace MobileFix.Domain.Inventory;

/// <summary>
/// El conjunto de equipos fichados del banco, con la procedencia del dato.
///
/// Si la procedencia no es <see cref="DataProvenance.Bench"/>, toda cobertura calculada desde
/// este conjunto es <b>provisional</b> y no puede usarse para decidir alcance. Esto existe porque
/// un banco con datos sembrados es la forma más fácil de mentirse a uno mismo.
/// </summary>
public sealed record InventoryDataset
{
    public required DataProvenance Provenance { get; init; }

    public required IReadOnlyList<DeviceRecord> Devices { get; init; }

    /// <summary>Filas descartadas durante la carga, con el motivo. Un dato raro no debe romper la carga.</summary>
    public IReadOnlyList<string> RejectedRows { get; init; } = [];

    public string? SourcePath { get; init; }

    /// <summary>¿La cobertura derivada de este conjunto es provisional?</summary>
    public bool IsProvisional => Provenance != DataProvenance.Bench;

    public static InventoryDataset Empty { get; } = new()
    {
        Provenance = DataProvenance.Unknown,
        Devices = [],
    };
}
