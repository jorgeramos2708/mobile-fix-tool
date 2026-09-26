using MobileFix.Domain.Inventory;

namespace MobileFix.Domain.Identification;

/// <summary>
/// Nivel de un peldaño de la escalera de identificación (PLAN-MAESTRO §4).
/// </summary>
public enum ProbeLevel
{
    L0_UsbDescriptor = 0,
    L1_Interface = 1,
    L2_Handshake = 2,
    L3_PartitionTable = 3,
    L4_BuildProperties = 4,
    L5_BoardIdentity = 5,
    L6_Baseband = 6,
}

/// <summary>Descriptor USB del dispositivo: nivel L0 y L1. No requiere hablar ningún protocolo.</summary>
public sealed record TransportDescriptor
{
    public required int VendorId { get; init; }
    public required int ProductId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Product { get; init; }
    public string? SerialHint { get; init; }

    /// <summary>Qué se deduce del descriptor: en qué modo está el equipo.</summary>
    public required AccessMechanism SuspectedMechanism { get; init; }

    public string Describe() =>
        $"VID:PID {VendorId:X4}:{ProductId:X4} · {Manufacturer ?? "?"} {Product ?? "?"} · {SuspectedMechanism}";
}

/// <summary>
/// Resultado del handshake de nivel L2 (Sahara, BROM, FDL, EUB, DFU).
/// Es el peldaño que más información da con menos intrusión: aquí ya se conoce el SoC exacto.
/// </summary>
public sealed record HandshakeResult
{
    public required string SocVendor { get; init; }
    public required string SocModel { get; init; }
    public string? SocRevision { get; init; }
    public string? OemId { get; init; }

    /// <summary>Arranque seguro activo. Si es verdadero, no hay ruta de escritura sin material firmado.</summary>
    public required bool SecureBootEnabled { get; init; }

    /// <summary>El mecanismo exige autenticación (DAA/SLA en MediaTek, firehose firmado en Qualcomm).</summary>
    public required bool RequiresAuthentication { get; init; }

    public string? DeviceSerial { get; init; }

    public string Describe() =>
        $"{SocVendor} {SocModel}{(SocRevision is null ? string.Empty : $" rev {SocRevision}")} · " +
        $"secure boot {(SecureBootEnabled ? "ON" : "off")} · auth {(RequiresAuthentication ? "SÍ" : "no")}";
}

/// <summary>
/// Propiedades leídas del sistema (nivel L4). <b>Medidas en el equipo, no declaradas por el catálogo.</b>
/// El build exacto y el parche de seguridad son lo que decide si una operación aplica o no.
/// </summary>
public sealed record BuildProperties
{
    public required string Model { get; init; }
    public string? Oem { get; init; }
    public string? RegionVariant { get; init; }
    public required string BuildId { get; init; }
    public string? AndroidVersion { get; init; }
    public string? SecurityPatch { get; init; }
    public string? FirmwareVersion { get; init; }

    /// <summary>Estado del bloqueo de operador <b>leído del equipo</b>, no supuesto. En México es crítico.</summary>
    public CarrierLockState? CarrierLock { get; init; }

    /// <summary>Operador al que está bloqueado, si el equipo lo declara.</summary>
    public string? Carrier { get; init; }

    public string Describe() =>
        $"{Oem} {Model}{(RegionVariant is null ? string.Empty : $" ({RegionVariant})")} · build {BuildId}" +
        $"{(AndroidVersion is null ? string.Empty : $" · Android {AndroidVersion}")}" +
        $"{(SecurityPatch is null ? string.Empty : $" · parche {SecurityPatch}")}";
}

/// <summary>Identidad de placa (nivel L5): la que revela placas cambiadas y reparaciones previas.</summary>
public sealed record BoardIdentity
{
    public required string BoardCodename { get; init; }
    public string? HardwareRevision { get; init; }
    public string? BoardSerial { get; init; }
    public string? ProductName { get; init; }

    public string Describe() =>
        $"{BoardCodename}{(HardwareRevision is null ? string.Empty : $" rev {HardwareRevision}")}" +
        $"{(ProductName is null ? string.Empty : $" · {ProductName}")}";
}

/// <summary>
/// Baseband (nivel L6). El IMEI se lee solo para verificar identidad y propiedad:
/// se almacena su hash y se enmascara en los informes.
/// </summary>
public sealed record BasebandIdentity
{
    public required string BasebandVersion { get; init; }
    public string? ModemRegion { get; init; }
    public string? Imei { get; init; }
    public string? Imei2 { get; init; }

    public string Describe() =>
        $"baseband {BasebandVersion}{(ModemRegion is null ? string.Empty : $" · región módem {ModemRegion}")}";
}

/// <summary>Una partición de la tabla GPT leída del equipo (nivel L3).</summary>
public sealed record GptPartition
{
    public required string Name { get; init; }
    public required long FirstSector { get; init; }
    public required long LastSector { get; init; }
    public required long SizeBytes { get; init; }

    /// <summary>Particiones de datos del usuario: nunca se leen sin autorización explícita.</summary>
    public bool IsUserData => Name is "userdata" or "data" or "sdcard" or "media";

    /// <summary>Particiones críticas cuya pérdida deja el equipo sin red o sin identidad.</summary>
    public bool IsCritical => Name is "persist" or "modem" or "modemst1" or "modemst2" or "fsg" or "nvdata" or "nvram" or "efs" or "param";
}
