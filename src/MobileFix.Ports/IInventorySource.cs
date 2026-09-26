using MobileFix.Domain.Inventory;

namespace MobileFix.Ports;

/// <summary>
/// Fuente del inventario del banco de pruebas. El CSV es el formato de intercambio porque es
/// diffeable, versionable y no arrastra dependencias; SQLite tomará el relevo en M1-04.
/// </summary>
public interface IInventorySource
{
    /// <summary>Carga el inventario desde un archivo. No lanza por filas malformadas: las reporta.</summary>
    InventoryDataset LoadFile(string path);
}
