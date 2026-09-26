using System.Text;
using System.Text.Json;
using MobileFix.Domain;
using MobileFix.Ports;

namespace MobileFix.Infrastructure;

/// <summary>
/// Journal append-only en disco, formato JSON Lines, encadenado por SHA-256.
///
/// Se escribe y se sincroniza a disco (fsync) antes de devolver el control. Es deliberadamente
/// lento y deliberadamente simple: si el proceso muere a mitad de una etapa, la última entrada
/// escrita describe exactamente en qué punto se quedó la sesión.
/// </summary>
public sealed class FileJournal : IJournal
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly string _path;
    private readonly IClock _clock;
    private readonly object _gate = new();

    private long _lastIndex = -1;
    private string _lastHash = JournalEntry.GenesisHash;

    public FileJournal(string path, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _path = Path.GetFullPath(path);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Si ya existe un journal, continuar la cadena en lugar de reiniciarla.
        foreach (var entry in ReadAll())
        {
            _lastIndex = entry.Index;
            _lastHash = entry.Hash;
        }
    }

    public string FilePath => _path;

    public JournalEntry Append(Guid sessionId, string kind, string stage, string payload)
    {
        lock (_gate)
        {
            var entry = JournalEntry.Create(
                _lastIndex + 1,
                _clock.UtcNow,
                sessionId,
                kind ?? string.Empty,
                stage ?? string.Empty,
                payload ?? string.Empty,
                _lastHash);

            var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;

            using (var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(line);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            _lastIndex = entry.Index;
            _lastHash = entry.Hash;
            return entry;
        }
    }

    public IReadOnlyList<JournalEntry> ReadAll()
    {
        var entries = new List<JournalEntry>();

        if (!File.Exists(_path))
        {
            return entries;
        }

        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JournalEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<JournalEntry>(line, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"El journal está corrupto en '{_path}': {ex.Message}", ex);
            }

            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public JournalVerification VerifyChain()
    {
        var entries = ReadAll();
        var expectedIndex = 0L;
        var previousHash = JournalEntry.GenesisHash;

        foreach (var entry in entries)
        {
            if (entry.Index != expectedIndex)
            {
                return new JournalVerification(
                    false, entries.Count, entry.Index,
                    $"Índice discontinuo: se esperaba {expectedIndex} y se encontró {entry.Index}. Falta una entrada.");
            }

            if (!string.Equals(entry.PrevHash, previousHash, StringComparison.Ordinal))
            {
                return new JournalVerification(
                    false, entries.Count, entry.Index,
                    "Encadenamiento roto: PrevHash no coincide con el hash de la entrada anterior.");
            }

            if (!string.Equals(JournalEntry.ComputeHash(entry), entry.Hash, StringComparison.Ordinal))
            {
                return new JournalVerification(
                    false, entries.Count, entry.Index,
                    "Entrada alterada: el hash recalculado no coincide con el almacenado.");
            }

            previousHash = entry.Hash;
            expectedIndex++;
        }

        return new JournalVerification(true, entries.Count, null, $"Cadena verificada: {entries.Count} entradas íntegras.");
    }
}
