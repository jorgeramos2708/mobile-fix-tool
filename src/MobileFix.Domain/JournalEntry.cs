using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MobileFix.Domain;

/// <summary>
/// Entrada del journal append-only, encadenada por hash (PLAN-MAESTRO §2, §21.4).
/// El hash de cada entrada incluye el hash de la anterior: alterar una sola entrada
/// invalida toda la cadena posterior. Esto es la base de la cadena de custodia.
/// </summary>
public sealed record JournalEntry
{
    /// <summary>Hash inicial de la cadena. No es un hash de nada: es el ancla.</summary>
    public const string GenesisHash = "GENESIS";

    public long Index { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public Guid SessionId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public string PrevHash { get; init; } = GenesisHash;
    public string Hash { get; init; } = string.Empty;

    /// <summary>Crea una entrada calculando su hash. La única forma legítima de crear una.</summary>
    public static JournalEntry Create(
        long index,
        DateTimeOffset timestampUtc,
        Guid sessionId,
        string kind,
        string stage,
        string payload,
        string prevHash)
    {
        var draft = new JournalEntry
        {
            Index = index,
            TimestampUtc = timestampUtc,
            SessionId = sessionId,
            Kind = kind,
            Stage = stage,
            Payload = payload,
            PrevHash = prevHash,
        };

        return draft with { Hash = ComputeHash(draft) };
    }

    /// <summary>Recalcula el hash a partir de los campos. El campo Hash se excluye.</summary>
    public static string ComputeHash(JournalEntry entry)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{entry.Index}|{entry.TimestampUtc:O}|{entry.SessionId:D}|{entry.Kind}|{entry.Stage}|{entry.Payload}|{entry.PrevHash}");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
