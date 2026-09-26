using MobileFix.Domain;
using MobileFix.Infrastructure;

namespace MobileFix.Domain.Tests;

/// <summary>
/// Journal encadenado por SHA-256. Si estas pruebas pasan, el taller puede demostrar
/// qué se hizo, cuándo y en qué orden; y puede demostrar que nadie lo alteró después.
/// </summary>
public sealed class JournalChainTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Limpieza best-effort: no debe hacer fallar una prueba.
            }
        }
    }

    [Fact]
    public void La_cadena_es_integra_tras_varias_inserciones()
    {
        var journal = NewJournal(out _);
        var sessionId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            journal.Append(sessionId, "stage.done", RepairStage.Connect.ToString(), $"paso {i}");
        }

        var verification = journal.VerifyChain();

        Assert.True(verification.IsValid, verification.Message);
        Assert.Equal(10, verification.Entries);
        Assert.Null(verification.FirstBrokenIndex);
    }

    [Fact]
    public void El_primer_hash_del_journal_encadena_con_el_genesis()
    {
        var journal = NewJournal(out _);
        journal.Append(Guid.NewGuid(), "session.begin", "session", "inicio");

        Assert.Equal(JournalEntry.GenesisHash, journal.ReadAll()[0].PrevHash);
    }

    [Fact]
    public void Manipular_una_entrada_rompe_la_cadena()
    {
        var journal = NewJournal(out var path);
        var sessionId = Guid.NewGuid();

        for (var i = 0; i < 8; i++)
        {
            journal.Append(sessionId, "stage.done", RepairStage.Connect.ToString(), $"paso {i}");
        }

        Assert.True(journal.VerifyChain().IsValid);

        // Simula a alguien editando el journal con un editor de texto.
        var lines = File.ReadAllLines(path);
        var target = Array.FindIndex(lines, line => line.Contains("paso 4", StringComparison.Ordinal));
        Assert.True(target >= 0, "La prueba esperaba encontrar la entrada 'paso 4'.");

        lines[target] = lines[target].Replace("paso 4", "paso 9", StringComparison.Ordinal);
        File.WriteAllLines(path, lines);

        var tampered = new FileJournal(path, TestSupport.NewClock()).VerifyChain();

        Assert.False(tampered.IsValid);
        Assert.Equal(4, tampered.FirstBrokenIndex);
        Assert.Contains("alterada", tampered.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_salto_de_indice_se_detecta_aunque_los_hashes_sean_correctos()
    {
        var path = TestSupport.NewJournalPath();
        _directories.Add(Path.GetDirectoryName(path)!);

        // Entrada legítima nº 0, y después una entrada con índice 5: falta el tramo intermedio.
        var first = JournalEntry.Create(0, TestSupport.T0, Guid.NewGuid(), "stage.done", "Connect", "uno", JournalEntry.GenesisHash);
        var gap = JournalEntry.Create(5, TestSupport.T0, first.SessionId, "stage.done", "Connect", "cinco", first.Hash);

        File.WriteAllLines(path, [Serialise(first), Serialise(gap)]);

        var verification = new FileJournal(path, TestSupport.NewClock()).VerifyChain();

        Assert.False(verification.IsValid);
        Assert.Contains("Índice discontinuo", verification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dos_journals_con_el_mismo_reloj_producen_hashes_identicos()
    {
        var first = NewJournal(out _);
        var second = NewJournal(out _);
        var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        for (var i = 0; i < 5; i++)
        {
            first.Append(sessionId, "stage.done", RepairStage.Connect.ToString(), $"paso {i}");
            second.Append(sessionId, "stage.done", RepairStage.Connect.ToString(), $"paso {i}");
        }

        // Mismo reloj + mismas operaciones = misma cadena de hashes.
        // Sin esto no se puede reproducir una sesión para auditar un caso.
        Assert.Equal(
            first.ReadAll().Select(entry => entry.Hash),
            second.ReadAll().Select(entry => entry.Hash));
    }

    [Fact]
    public void Un_journal_existente_continua_la_cadena_en_lugar_de_reiniciarla()
    {
        var path = TestSupport.NewJournalPath();
        _directories.Add(Path.GetDirectoryName(path)!);

        var first = new FileJournal(path, TestSupport.NewClock());
        first.Append(Guid.NewGuid(), "stage.done", "Connect", "uno");
        first.Append(Guid.NewGuid(), "stage.done", "Identify", "dos");

        // Segunda apertura del mismo archivo, como haría la aplicación al reabrir una sesión.
        var second = new FileJournal(path, TestSupport.NewClock());
        second.Append(Guid.NewGuid(), "stage.done", "Classify", "tres");

        var entries = second.ReadAll();
        var verification = second.VerifyChain();

        Assert.Equal(3, entries.Count);
        Assert.Equal(new[] { 0L, 1L, 2L }, entries.Select(entry => entry.Index));
        Assert.Equal(entries[1].Hash, entries[2].PrevHash);
        Assert.True(verification.IsValid, verification.Message);
    }

    [Fact]
    public void El_journal_sobrevive_a_la_reapertura_sin_perder_la_verificacion()
    {
        var journal = NewJournal(out var path);
        journal.Append(Guid.NewGuid(), "stage.done", "Connect", "contenido con acentos: áéíóú·ñ");

        var reopened = new FileJournal(path, TestSupport.NewClock());

        Assert.True(reopened.VerifyChain().IsValid);
        Assert.Contains("acentos", reopened.ReadAll()[0].Payload, StringComparison.Ordinal);
    }

    private FileJournal NewJournal(out string path)
    {
        path = TestSupport.NewJournalPath();
        _directories.Add(Path.GetDirectoryName(path)!);
        return new FileJournal(path, TestSupport.NewClock());
    }

    private static string Serialise(JournalEntry entry) =>
        System.Text.Json.JsonSerializer.Serialize(entry);
}
