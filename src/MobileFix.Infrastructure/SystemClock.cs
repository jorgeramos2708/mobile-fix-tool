using MobileFix.Ports;

namespace MobileFix.Infrastructure;

/// <summary>Reloj real. En pruebas se sustituye por un reloj fijo para obtener journals deterministas.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
