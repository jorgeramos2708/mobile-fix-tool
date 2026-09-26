namespace MobileFix.Domain.Inventory;

/// <summary>
/// Bloqueo de operador. En México una parte enorme del mercado son equipos importados de
/// Estados Unidos con bloqueo de AT&amp;T, T-Mobile, Verizon, Metro, Cricket o Boost.
/// </summary>
public enum CarrierLockState
{
    NotApplicable,
    Unlocked,
    LockedToCarrier,
    Unknown,
}

/// <summary>
/// Ruta de liberación. <b>Solo hay una legítima: el operador.</b>
///
/// La plataforma lee el estado del bloqueo, comprueba elegibilidad, guía al trámite oficial y
/// documenta la liberación hecha por el operador. Nunca la ejecuta por bypass, exploitation ni
/// servicios de terceros: eso es exactamente el terreno que el producto tiene prohibido (ADR y
/// alcance excluido del PLAN-MAESTRO).
/// </summary>
public enum UnlockPath
{
    /// <summary>No hace falta: el equipo no está bloqueado a ningún operador.</summary>
    NotNeeded,

    /// <summary>El operador libera mediante su portal, cumpliendo requisitos (pago, permanencia).</summary>
    CarrierPortal,

    /// <summary>El operador libera de forma automática tras un plazo (p. ej. Verizon a los 60 días).</summary>
    CarrierAutomatic,

    /// <summary>No hay ruta de liberación conocida.</summary>
    Unsupported,

    Unknown,
}

/// <summary>Tipo de SIM. Relevante porque los iPhone de EEUU desde el 14 son solo eSIM.</summary>
public enum SimType
{
    Unknown,
    PhysicalSingle,
    PhysicalDual,
    Hybrid,
    EsimOnly,
}

/// <summary>
/// Texto de orientación para el técnico. Existe para que la respuesta de la plataforma ante un
/// equipo bloqueado sea siempre la misma y siempre legítima.
/// </summary>
public static class UnlockGuidance
{
    /// <summary>La plataforma nunca libera por bypass. Si algún día alguien cambia esto, que sea a conciencia.</summary>
    public const bool BypassIsEverAllowed = false;

    public static string Describe(UnlockPath path, string? carrier) => path switch
    {
        UnlockPath.NotNeeded =>
            "sin bloqueo de operador",

        UnlockPath.CarrierPortal =>
            $"bloqueado a {Carrier(carrier)}: la liberación la hace el operador en su portal, " +
            "con el equipo pagado y sin contrato vigente. La plataforma guía el trámite y documenta el resultado",

        UnlockPath.CarrierAutomatic =>
            $"bloqueado a {Carrier(carrier)}: el operador libera automáticamente al cumplir su plazo. " +
            "Comprobar fecha de activación antes de cobrar el trámite",

        UnlockPath.Unsupported =>
            $"bloqueado a {Carrier(carrier)}: no se conoce ruta de liberación",

        _ => "estado de bloqueo sin determinar: hay que leerlo del equipo antes de prometer nada",
    };

    /// <summary>¿Se le puede decir al cliente que el trámite es viable con lo que sabemos?</summary>
    public static bool IsActionable(UnlockPath path) =>
        path is UnlockPath.CarrierPortal or UnlockPath.CarrierAutomatic;

    /// <summary>
    /// Orientación a partir de lo que <b>declara el equipo</b>. El equipo dice que está bloqueado;
    /// no dice si cumple los requisitos, así que la elegibilidad se confirma con el operador antes
    /// de cobrar nada.
    /// </summary>
    public static string DescribeFromDevice(CarrierLockState carrierLock, string? carrier) => carrierLock switch
    {
        CarrierLockState.NotApplicable =>
            "el bloqueo de operador no aplica a este equipo",

        CarrierLockState.Unlocked =>
            "el equipo se declara libre: verificar leyendo de nuevo el estado antes de facturar la liberación",

        CarrierLockState.LockedToCarrier =>
            $"bloqueado a {Carrier(carrier)}: la liberación la hace el operador. Comprobar elegibilidad " +
            "(equipo pagado, contrato cumplido, plazo) antes de cobrar el trámite. La plataforma no libera por bypass",

        _ => Describe(UnlockPath.Unknown, carrier),
    };

    private static string Carrier(string? carrier) =>
        string.IsNullOrWhiteSpace(carrier) ? "operador no declarado" : carrier;
}
