namespace MobileFix.Ports;

/// <summary>
/// Reloj inyectable. Existe para que el dominio y el journal sean deterministas en pruebas:
/// un sistema que no puede reproducir el tiempo no puede auditar nada.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
