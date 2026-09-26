using MobileFix.Domain.Identification;
using MobileFix.Domain.Inventory;

namespace MobileFix.Ports;

/// <summary>
/// Calidad del enlace (DESIGN-TOKENS §3.2). Es el dato ambiental más importante de la aplicación:
/// la mayoría de los equipos que se convierten en ladrillo se caen a mitad de escritura por un cable
/// o un hub malos, no por un protocolo mal implementado.
/// </summary>
public enum LinkQuality
{
    Excellent,
    Good,
    Unstable,
    Unusable,
    Disconnected,
}

public sealed record LinkReport(LinkQuality Quality, int ThroughputKbps, int RetryCount, int ErrorCount)
{
    public static LinkReport Disconnected { get; } = new(LinkQuality.Disconnected, 0, 0, 0);

    /// <summary>Solo se escribe si el enlace es excelente o bueno. Ni «inestable» ni «inservible».</summary>
    public bool IsSafeToWrite => Quality is LinkQuality.Excellent or LinkQuality.Good;

    public string Describe() => Quality switch
    {
        LinkQuality.Excellent => $"excelente · {ThroughputKbps} kbps · 0 errores",
        LinkQuality.Good => $"buena · {ThroughputKbps} kbps · {RetryCount} reintentos",
        LinkQuality.Unstable => $"INESTABLE · {RetryCount} reintentos y {ErrorCount} errores: no apta para escribir",
        LinkQuality.Unusable => $"no apta para escribir · {ErrorCount} errores de enlace",
        _ => "desconectado",
    };
}

/// <summary>
/// Transporte hacia el dispositivo. Es el único contrato que conoce el mundo exterior.
///
/// Decisión de diseño (v0.1): <b>un puerto por sesión de dispositivo, no un puerto por protocolo</b>.
/// Los protocolos concretos (Sahara y firehose en Qualcomm, BROM y DAA en MediaTek, FDL en Unisoc,
/// EUB en Exynos, DFU en Apple) son implementaciones de este contrato, no contratos distintos:
/// el resto de la plataforma no necesita saber por dónde se habla, solo qué se obtuvo.
///
/// Todos los métodos de esta versión son de <b>solo lectura</b>. Escribir es la etapa 10 y llega con
/// los gates completos: si algún día aparece aquí un método de escritura, tendrá que pasar por el
/// pipeline de seguridad y no podrá invocarse desde la identificación.
/// </summary>
public interface IDeviceTransport
{
    /// <summary>Descripción legible del enlace, para el journal y la barra de estado.</summary>
    string Describe();

    bool IsConnected { get; }

    /// <summary>Mide el enlace antes de permitir cualquier escritura.</summary>
    LinkReport MeasureLink();

    /// <summary>L0/L1 — descriptor USB y modo detectado. No habla ningún protocolo.</summary>
    ValueTask<TransportDescriptor> ReadDescriptorAsync(CancellationToken cancellationToken);

    /// <summary>L2 — handshake del bootrom. Da el SoC exacto y las banderas de seguridad.</summary>
    ValueTask<HandshakeResult?> HandshakeAsync(AccessMechanism mechanism, CancellationToken cancellationToken);

    /// <summary>L3 — tabla de particiones, leída sin escribir nada.</summary>
    ValueTask<IReadOnlyList<GptPartition>> ReadPartitionTableAsync(CancellationToken cancellationToken);

    /// <summary>L4 — propiedades del sistema: modelo, build, parche de seguridad.</summary>
    ValueTask<BuildProperties?> ReadBuildPropertiesAsync(CancellationToken cancellationToken);

    /// <summary>L5 — identidad de placa: revela placas cambiadas y reparaciones previas.</summary>
    ValueTask<BoardIdentity?> ReadBoardIdentityAsync(CancellationToken cancellationToken);

    /// <summary>L6 — baseband e IMEI, solo para verificar identidad y propiedad.</summary>
    ValueTask<BasebandIdentity?> ReadBasebandAsync(CancellationToken cancellationToken);
}
