using MobileFix.Ports;

namespace MobileFix.Infrastructure;

/// <summary>
/// Reloj controlable por el llamante. No es una utilidad solo para pruebas: es lo que permite
/// <b>reproducir una sesión</b> (modo simulador, PLAN-MAESTRO §21.9) y <b>reconstruir el journal
/// byte a byte</b> para auditar un caso. Un sistema que no puede reproducir el tiempo no puede auditar nada.
/// </summary>
public sealed class ManualClock : IClock
{
    private DateTimeOffset _now;

    public ManualClock(DateTimeOffset start) => _now = start;

    public DateTimeOffset UtcNow => _now;

    /// <summary>Avanza el reloj. Útil para simular operaciones que tardan (flasheos, timeouts).</summary>
    public void Advance(TimeSpan delta) => _now += delta;

    /// <summary>Fija una hora concreta.</summary>
    public void Set(DateTimeOffset value) => _now = value;
}
