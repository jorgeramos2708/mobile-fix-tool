using MobileFix.Domain;

namespace MobileFix.Ports;

/// <summary>
/// Journal append-only. Toda operación se registra antes y después de ejecutarse:
/// una operación que no está en el journal no ocurrió (PLAN-MAESTRO §2).
/// </summary>
public interface IJournal
{
    /// <summary>Añade una entrada y la sincroniza a disco antes de devolver el control.</summary>
    JournalEntry Append(Guid sessionId, string kind, string stage, string payload);

    /// <summary>Devuelve todas las entradas, en orden.</summary>
    IReadOnlyList<JournalEntry> ReadAll();

    /// <summary>Recalcula la cadena y detecta alteraciones, saltos de índice o rupturas de encadenamiento.</summary>
    JournalVerification VerifyChain();
}

/// <summary>Resultado de verificar la integridad de la cadena.</summary>
public sealed record JournalVerification(
    bool IsValid,
    long Entries,
    long? FirstBrokenIndex,
    string Message);
