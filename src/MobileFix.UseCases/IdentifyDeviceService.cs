using MobileFix.Domain;
using MobileFix.Domain.Identification;
using MobileFix.Domain.Inventory;
using MobileFix.Ports;

namespace MobileFix.UseCases;

/// <summary>Resultado completo de identificar un equipo: qué es, si el enlace sirve y qué se puede hacer.</summary>
public sealed record IdentificationOutcome(
    DeviceIdentity Identity,
    LinkReport Link,
    CoverageAssessment? Coverage,
    string UnlockGuidance)
{
    /// <summary>¿El enlace permite escribir? Solo con enlace bueno o excelente.</summary>
    public bool LinkAllowsWriting => Link.IsSafeToWrite;

    /// <summary>
    /// ¿Se puede escribir? Hacen falta <b>las tres</b> cosas a la vez: enlace apto para escritura,
    /// identidad sin conflictos y cobertura que permita escribir.
    ///
    /// Sin evidencia de banco devuelve falso: la cobertura tiene que estar demostrada, no supuesta.
    /// </summary>
    public bool CanWrite =>
        LinkAllowsWriting &&
        Identity.Conflicts.Count == 0 &&
        Coverage?.AllowsWriting == true;
}

/// <summary>
/// Recorre la escalera de identificación contra un transporte y, si hay evidencia de banco
/// disponible, resuelve además el veredicto de cobertura.
///
/// Este es el bucle completo del producto: <b>leer del equipo → identificar → resolver cobertura →
/// decidir qué se puede hacer</b>, con el enlace medido antes de prometer nada.
/// </summary>
public sealed class IdentifyDeviceService
{
    private readonly IDeviceTransport _transport;
    private readonly IdentificationEvidence _evidence;
    private readonly IdentificationLadder _ladder = new();

    public IdentifyDeviceService(IDeviceTransport transport, IdentificationEvidence evidence)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _evidence = evidence;
    }

    public async Task<IdentificationOutcome> RunAsync(BenchCoverage? coverage = null, CancellationToken cancellationToken = default)
    {
        var descriptor = await _transport.ReadDescriptorAsync(cancellationToken);
        var handshake = await _transport.HandshakeAsync(descriptor.SuspectedMechanism, cancellationToken);
        var partitions = await _transport.ReadPartitionTableAsync(cancellationToken);
        var build = await _transport.ReadBuildPropertiesAsync(cancellationToken);
        var board = await _transport.ReadBoardIdentityAsync(cancellationToken);
        var baseband = await _transport.ReadBasebandAsync(cancellationToken);

        var identity = _ladder.Assemble(new IdentificationInputs
        {
            Descriptor = descriptor,
            Handshake = handshake,
            Partitions = partitions,
            Build = build,
            Board = board,
            Baseband = baseband,
            Evidence = _evidence,
        });

        var link = _transport.MeasureLink();

        var assessment = coverage?.Resolve(identity.ToFingerprint());

        var unlock = identity.CarrierLock is null
            ? "el equipo no declara estado de bloqueo de operador"
            : UnlockGuidance.DescribeFromDevice(identity.CarrierLock.Value, identity.Carrier);

        return new IdentificationOutcome(identity, link, assessment, unlock);
    }
}
