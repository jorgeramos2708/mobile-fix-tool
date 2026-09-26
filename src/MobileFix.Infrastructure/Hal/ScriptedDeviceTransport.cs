using MobileFix.Domain.Identification;
using MobileFix.Domain.Inventory;
using MobileFix.Ports;

namespace MobileFix.Infrastructure.Hal;

/// <summary>
/// Perfil de dispositivo simulado. Todo lo que devuelve está marcado como
/// <see cref="IdentificationEvidence.ScriptedSimulation"/>, así que la identidad resultante sale
/// PROVISIONAL: sirve para construir y probar la escalera, nunca para afirmar cobertura.
/// </summary>
public sealed record ScriptedDevice
{
    public required string Name { get; init; }
    public required TransportDescriptor Descriptor { get; init; }
    public HandshakeResult? Handshake { get; init; }
    public IReadOnlyList<GptPartition> Partitions { get; init; } = [];
    public BuildProperties? Build { get; init; }
    public BoardIdentity? Board { get; init; }
    public BasebandIdentity? Baseband { get; init; }
    public LinkReport Link { get; init; } = new(LinkQuality.Excellent, 12400, 0, 0);
}

/// <summary>
/// Transporte simulado. Sustituye al USB real hasta que llegue el banco de pruebas (M0).
///
/// Existe por una razón concreta: cuando llegue el primer MediaTek enchufado, lo único que hay que
/// escribir es la implementación USB de <see cref="IDeviceTransport"/>. La escalera de identificación,
/// el cálculo de confianza, la detección de conflictos, el motor de cobertura y toda la interfaz ya
/// estarán probados contra este simulador.
/// </summary>
public sealed class ScriptedDeviceTransport(ScriptedDevice device) : IDeviceTransport
{
    private readonly ScriptedDevice _device = device ?? throw new ArgumentNullException(nameof(device));

    public string Name => _device.Name;

    public string Describe() => $"SIMULADO · {_device.Name}";

    public bool IsConnected => true;

    public LinkReport MeasureLink() => _device.Link;

    public ValueTask<TransportDescriptor> ReadDescriptorAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_device.Descriptor);

    public ValueTask<HandshakeResult?> HandshakeAsync(AccessMechanism mechanism, CancellationToken cancellationToken)
    {
        // El simulador respeta el mecanismo pedido: si no es el suyo, no hay handshake.
        var result = _device.Descriptor.SuspectedMechanism == mechanism ? _device.Handshake : null;
        return ValueTask.FromResult(result);
    }

    public ValueTask<IReadOnlyList<GptPartition>> ReadPartitionTableAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_device.Partitions);

    public ValueTask<BuildProperties?> ReadBuildPropertiesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_device.Build);

    public ValueTask<BoardIdentity?> ReadBoardIdentityAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_device.Board);

    public ValueTask<BasebandIdentity?> ReadBasebandAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_device.Baseband);
}

/// <summary>Perfiles del simulador, con arquitecturas reales del mercado mexicano.</summary>
public static class ScriptedProfiles
{
    /// <summary>Samsung Galaxy A13 (SM-A135M): Exynos 850, coherente, hasta L6.</summary>
    public static ScriptedDevice GalaxyA13Lte() => new()
    {
        Name = "Samsung Galaxy A13 SM-A135M (Exynos 850)",
        Descriptor = new TransportDescriptor
        {
            VendorId = 0x04E8, ProductId = 0x685D, Manufacturer = "SAMSUNG", Product = "Gadget Serial",
            SuspectedMechanism = AccessMechanism.EubExynos,
        },
        Handshake = new HandshakeResult
        {
            SocVendor = "Exynos", SocModel = "Exynos 850", SocRevision = "1.0",
            SecureBootEnabled = false, RequiresAuthentication = false, DeviceSerial = "R58NC0ABCDE",
        },
        Partitions = SamsungLayout(100_663_298_048),
        Build = new BuildProperties
        {
            Oem = "samsung", Model = "SM-A135M", RegionVariant = "MXO", BuildId = "TP1A.220624.014",
            AndroidVersion = "13", SecurityPatch = "2024-08-01", FirmwareVersion = "A135MUBU6DXA1",
            CarrierLock = CarrierLockState.NotApplicable,
        },
        Board = new BoardIdentity { BoardCodename = "a13", HardwareRevision = "0.6", ProductName = "a13m" },
        Baseband = new BasebandIdentity { BasebandVersion = "A135MUBU6DXA1", ModemRegion = "MXO", Imei = "352999110012345", Imei2 = "352999110012346" },
    };

    /// <summary>Xiaomi Redmi Note 11 en versión Qualcomm (2201117TG): SD680, coherente, hasta L6.</summary>
    public static ScriptedDevice RedmiNote11Qualcomm() => new()
    {
        Name = "Xiaomi Redmi Note 11 2201117TG (Snapdragon 680)",
        Descriptor = new TransportDescriptor
        {
            VendorId = 0x2717, ProductId = 0xFF40, Manufacturer = "Xiaomi", Product = "QDLoader 9008",
            SuspectedMechanism = AccessMechanism.EdlQualcomm,
        },
        Handshake = new HandshakeResult
        {
            SocVendor = "Qualcomm", SocModel = "SM6225 Snapdragon 680", SocRevision = "1.0",
            SecureBootEnabled = true, RequiresAuthentication = true, DeviceSerial = "0x9C3F21A7",
        },
        Partitions = XiaomiLayout(128_849_018_880),
        Build = new BuildProperties
        {
            Oem = "Xiaomi", Model = "2201117TG", RegionVariant = "MI", BuildId = "SKQ1.211103.001",
            AndroidVersion = "13", SecurityPatch = "2024-05-01", FirmwareVersion = "V14.0.4.0.TKGMIXM",
            CarrierLock = CarrierLockState.Unlocked,
        },
        Board = new BoardIdentity { BoardCodename = "spes", HardwareRevision = "1.0", ProductName = "spes_global" },
        Baseband = new BasebandIdentity { BasebandVersion = "MPSS.JO.3.0.c7-00066", ModemRegion = "MI", Imei = "860000000000001" },
    };

    /// <summary>
    /// Equipo con placa cambiada: el handshake dice una cosa y el sistema declara otra.
    /// Este perfil existe para demostrar que la escalera detecta la incoherencia en vez de tragársela.
    /// </summary>
    public static ScriptedDevice PlacaCambiada() => new()
    {
        Name = "Equipo con placa cambiada (incoherente)",
        Descriptor = new TransportDescriptor
        {
            VendorId = 0x2717, ProductId = 0xFF40, Manufacturer = "Xiaomi", Product = "QDLoader 9008",
            SuspectedMechanism = AccessMechanism.EdlQualcomm,
        },
        Handshake = new HandshakeResult
        {
            SocVendor = "MediaTek", SocModel = "MT6769 Helio G85", SocRevision = "1.0",
            SecureBootEnabled = false, RequiresAuthentication = false, DeviceSerial = "ABCDEF123456",
        },
        Partitions = XiaomiLayout(64_424_509_440),
        Build = new BuildProperties
        {
            Oem = "Xiaomi", Model = "2201117TG", RegionVariant = "MI", BuildId = "SKQ1.211103.001",
            AndroidVersion = "13", SecurityPatch = "2023-11-01",
        },
        Board = new BoardIdentity { BoardCodename = "spes", HardwareRevision = "1.0", ProductName = "spes_global" },
        Baseband = new BasebandIdentity { BasebandVersion = "MPSS.JO.3.0.c7-00066", ModemRegion = "EU", Imei = "860000000000099" },
    };

    /// <summary>Enlace inestable: demuestra que la plataforma se niega a escribir con mal cable.</summary>
    public static ScriptedDevice ConEnlaceInestable() => GalaxyA13Lte() with
    {
        Name = "Samsung Galaxy A13 con enlace inestable (cable o hub en mal estado)",
        Link = new LinkReport(LinkQuality.Unstable, 820, 14, 9),
    };

    private static IReadOnlyList<GptPartition> SamsungLayout(long userDataBytes) =>
    [
        Partition("preloader", 2048, 4095),
        Partition("pgpt", 4096, 5119),
        Partition("md1img", 8192, 16383),
        Partition("boot", 16384, 147455),
        Partition("recovery", 147456, 278527),
        Partition("vbmeta", 278528, 278559),
        Partition("super", 524288, 8_912_895),
        Partition("persist", 8_912_896, 8_978_431),
        Partition("modem", 8_978_432, 9_043_967),
        Partition("nvdata", 9_043_968, 9_109_503),
        Partition("nvram", 9_109_504, 9_110_527),
        Partition("efs", 9_110_528, 9_114_623),
        Partition("userdata", 10_485_760, 10_485_760 + (userDataBytes / 512) - 1),
    ];

    private static IReadOnlyList<GptPartition> XiaomiLayout(long userDataBytes) =>
    [
        Partition("gpt", 0, 33),
        Partition("preloader", 2048, 4095),
        Partition("boot", 8192, 139263),
        Partition("recovery", 139264, 270335),
        Partition("vbmeta", 270336, 270367),
        Partition("super", 524288, 4_718_591),
        Partition("persist", 4_718_592, 4_784_127),
        Partition("modem", 4_784_128, 4_849_663),
        Partition("fsg", 4_849_664, 4_850_687),
        Partition("nvdata", 4_850_688, 4_917_247),
        Partition("nvram", 4_917_248, 4_918_271),
        Partition("userdata", 6_291_456, 6_291_456 + (userDataBytes / 512) - 1),
    ];

    private static GptPartition Partition(string name, long firstSector, long lastSector) => new()
    {
        Name = name,
        FirstSector = firstSector,
        LastSector = lastSector,
        SizeBytes = (lastSector - firstSector + 1) * 512,
    };
}
