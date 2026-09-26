using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MobileFix.Domain;
using MobileFix.Domain.Inventory;
using MobileFix.Infrastructure;
using MobileFix.UseCases;

namespace MobileFix.Desktop.ViewModels;

/// <summary>Fila de la matriz de cobertura por mecanismo de acceso.</summary>
public sealed record MechanismRow(string Mecanismo, int Equipos, int ConCredencial, string Porcentaje);

/// <summary>Fila de la cobertura por nivel de intervención.</summary>
public sealed record InterventionRow(string Nivel, int Si, int Parcial, int No, int NoAplica, string Alcanzable);

/// <summary>Veredicto resuelto para un dispositivo de ejemplo.</summary>
public sealed record VerdictRow(string Dispositivo, string Veredicto, string Escritura, string Nota);

/// <summary>
/// Vista de la sesión y de la cobertura del banco.
///
/// El trabajo se ejecuta en un hilo de fondo y las actualizaciones se marshalean al hilo de UI:
/// cuando llegue el I/O real (USB, EDL, BROM), la interfaz no se puede bloquear nunca.
/// </summary>
public sealed class SessionViewModel : INotifyPropertyChanged
{
    private static readonly Brush ProvisionalBrush = Freeze(0xF5, 0x9E, 0x0B);

    private readonly string _journalPath;
    private readonly FileJournal _journal;
    private readonly RepairSessionService _service;
    private readonly BenchCoverageService _coverageService;

    private string _log = string.Empty;
    private string _linkStatus = "ENLACE: sin hardware · modo simulación";
    private string _journalStatus = "journal: —";
    private string _coverageSummary = "Cargando inventario del banco…";
    private string _coverageSource = string.Empty;
    private string _coverageBanner = string.Empty;
    private Visibility _provisionalVisibility = Visibility.Collapsed;
    private bool _busy;

    public SessionViewModel()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MobileFixDemo");

        _journalPath = Path.Combine(root, "journal-desktop.jsonl");
        _journal = new FileJournal(_journalPath, new SystemClock());
        _service = new RepairSessionService(_journal, new SystemClock());
        _coverageService = new BenchCoverageService(new InventoryCsvReader());

        foreach (var stage in RepairStages.Ordered)
        {
            Stages.Add(new StageViewModel(stage));
        }

        RunSessionCommand = new RelayCommand(_ => RunSession(), _ => !_busy);
        RunBlockedCommand = new RelayCommand(_ => RunBlockedSession(), _ => !_busy);
        VerifyCommand = new RelayCommand(_ => VerifyChain(), _ => !_busy);
        LoadInventoryCommand = new RelayCommand(_ => PickInventory(), _ => !_busy);

        WriteLine($"Plataforma MobileFix · pipeline de {RepairStages.Total} etapas");
        WriteLine($"Journal: {_journalPath}");
        WriteLine("Sin hardware conectado. Los datos de esta sesión son simulados.");
        WriteLine("Pulsa «Ejecutar sesión simulada» para recorrer las 13 etapas.");
        UpdateJournalStatus();

        LoadDefaultInventory();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StageViewModel> Stages { get; } = [];

    public ObservableCollection<MechanismRow> MechanismRows { get; } = [];

    public ObservableCollection<InterventionRow> InterventionRows { get; } = [];

    public ObservableCollection<VerdictRow> VerdictRows { get; } = [];

    public ICommand RunSessionCommand { get; }

    public ICommand RunBlockedCommand { get; }

    public ICommand VerifyCommand { get; }

    public ICommand LoadInventoryCommand { get; }

    public string Log
    {
        get => _log;
        private set
        {
            _log = value;
            OnPropertyChanged();
        }
    }

    public string LinkStatus
    {
        get => _linkStatus;
        private set
        {
            _linkStatus = value;
            OnPropertyChanged();
        }
    }

    public string JournalStatus
    {
        get => _journalStatus;
        private set
        {
            _journalStatus = value;
            OnPropertyChanged();
        }
    }

    public string CoverageSummary
    {
        get => _coverageSummary;
        private set
        {
            _coverageSummary = value;
            OnPropertyChanged();
        }
    }

    public string CoverageSource
    {
        get => _coverageSource;
        private set
        {
            _coverageSource = value;
            OnPropertyChanged();
        }
    }

    public string CoverageBanner
    {
        get => _coverageBanner;
        private set
        {
            _coverageBanner = value;
            OnPropertyChanged();
        }
    }

    public Visibility ProvisionalVisibility
    {
        get => _provisionalVisibility;
        private set
        {
            _provisionalVisibility = value;
            OnPropertyChanged();
        }
    }

    // ------------------------------------------------------------------ cobertura

    private void LoadDefaultInventory()
    {
        var path = FindDefaultInventory();

        if (path is null)
        {
            CoverageSummary = "No se encontró ningún inventario.";
            CoverageSource = "Usa «Abrir inventario…» para cargar un CSV del banco.";
            return;
        }

        LoadInventoryFrom(path);
    }

    private void PickInventory()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccionar inventario del banco (CSV)",
            Filter = "Inventario CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            LoadInventoryFrom(dialog.FileName);
        }
    }

    private void LoadInventoryFrom(string path)
    {
        try
        {
            ApplyCoverage(_coverageService.Load(path));
        }
        catch (Exception ex)
        {
            CoverageSummary = "No se pudo cargar el inventario.";
            CoverageSource = ex.Message;
        }
    }

    private void ApplyCoverage(BenchCoverage coverage)
    {
        var statistics = coverage.Statistics;

        MechanismRows.Clear();
        foreach (var mechanism in statistics.ByMechanism.Where(stats => stats.HasEvidence))
        {
            var percent = statistics.Total == 0 ? 0d : (double)mechanism.Devices / statistics.Total;
            MechanismRows.Add(new MechanismRow(mechanism.Mechanism.ToString(), mechanism.Devices, mechanism.RequiringCredentials, $"{percent:P0}"));
        }

        InterventionRows.Clear();
        foreach (var intervention in statistics.ByIntervention)
        {
            InterventionRows.Add(new InterventionRow(
                intervention.Name,
                intervention.Yes,
                intervention.Partial,
                intervention.No,
                intervention.NotApplicable,
                $"{intervention.ReachablePercent:P0}"));
        }

        VerdictRows.Clear();
        foreach (var fingerprint in SampleFingerprints())
        {
            var assessment = coverage.Resolve(fingerprint);
            VerdictRows.Add(new VerdictRow(
                $"{fingerprint.SocVendor} · {fingerprint.Model}",
                assessment.Verdict.ToString(),
                assessment.AllowsWriting ? "sí" : "no",
                assessment.Reasons.FirstOrDefault() ?? string.Empty));
        }

        CoverageSummary =
            $"{statistics.Total} equipos · {statistics.MechanismsWithEvidence} mecanismos con evidencia · " +
            $"identificados a nivel de modelo o superior: {statistics.IdentifiedAtModelLevelPercent:P0}";
        CoverageSource = coverage.SourcePath ?? "(sin ruta)";

        if (coverage.IsProvisional)
        {
            ProvisionalVisibility = Visibility.Visible;
            CoverageBanner =
                "COBERTURA PROVISIONAL — este inventario no está declarado como medido en el banco " +
                $"(procedencia: {statistics.Provenance}). Los veredictos sirven para construir y probar el motor, " +
                "no para afirmar cobertura ante un cliente.";
        }
        else
        {
            ProvisionalVisibility = Visibility.Collapsed;
            CoverageBanner = string.Empty;
        }

        WriteLine($"Cobertura cargada desde {coverage.SourcePath}: {statistics.Total} equipos ({statistics.Provenance}).");
    }

    private static IEnumerable<DeviceFingerprint> SampleFingerprints()
    {
        yield return Fingerprint("MediaTek", "Galaxy A13");
        yield return Fingerprint("Qualcomm", "Moto G52");
        yield return Fingerprint("Unisoc", "Spark 10");
        yield return Fingerprint("Exynos", "Galaxy A53 5G");
        yield return Fingerprint("Apple", "iPhone 12");
        yield return Fingerprint("Ficticio Semiconductor", "Modelo Z");
    }

    private static DeviceFingerprint Fingerprint(string socVendor, string model) => new()
    {
        SocVendor = socVendor,
        SocModel = "leído en el handshake",
        Oem = "fabricante",
        Model = model,
        ConfidencePercent = 96,
        HighestSource = FingerprintSource.BuildProperties,
    };

    private static string? FindDefaultInventory()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "inventario-demo.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "inventario-demo.csv"),
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "docs", "inventario-demo.csv"));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    // ------------------------------------------------------------------ sesión

    private void RunSession()
    {
        RunOnBackground(() =>
        {
            WriteLine(string.Empty);
            WriteLine("=== Sesión simulada: 13 etapas ===");
            ResetStages();

            var session = _service.Begin();

            Step(RepairStage.Connect, () => _service.Connect(session, "USB simulado · puerto 3 del hub 1"));

            Step(RepairStage.Identify, () => _service.Identify(session, Fingerprint("MediaTek", "Galaxy A13")));

            Step(RepairStage.Classify, () => _service.Classify(session, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial,
                CoverageEvidence.SeededDemo,
                "BROM accesible sin autenticación DAA/SLA en este modelo")));

            Step(RepairStage.Diagnose, () =>
                _service.Diagnose(session, "batería 78% (4120 mV) · eMMC saludable · 0 bloques ilegibles"));

            Step(RepairStage.Correlate, () =>
                _service.Correlate(session, "síntoma bootloop + partición boot corrupta → 87% resueltos con reflasheo"));

            Step(RepairStage.CheckConstraints, () =>
                _service.CheckConstraints(session, "secure boot off · AVB deshabilitado · sin ARB · bootloader desbloqueado", hardBlocker: false));

            Step(RepairStage.Backup, () =>
                _service.Backup(session, "GPT + boot + persist + modem (12 MB)", verifiedIntegrity: true));

            Step(RepairStage.PlanRepair, () =>
                _service.PlanRepair(session, "reflasheo de 'boot' desde firmware oficial firmado, hash verificado"));

            Step(RepairStage.SafetyGate, () =>
                _service.SafetyGate(session, new SafetyGateInputs(
                    OwnershipVerified: true,
                    ConsentSigned: true,
                    LinkHealthy: true,
                    Risk: RiskLevel.Destructive,
                    SupervisorApproved: true)));

            Step(RepairStage.Repair, () =>
                _service.Repair(session, "escritura de 'boot' (8 MB) — punto único de escritura de la plataforma"));

            Step(RepairStage.Verify, () =>
                _service.Verify(session, "hash leído == hash esperado · arranque completado en 22 s"));

            Step(RepairStage.CompareBeforeAfter, () =>
                _service.CompareBeforeAfter(session, "boot corrupto → íntegro · userdata intacta · IMEI sin cambios"));

            Step(RepairStage.Report, () =>
                _service.Report(session, "informe técnico + informe de cliente emitidos con evidencia encadenada"));

            WriteLine(string.Empty);
            WriteLine(session.Describe());
            LinkStatus = "ENLACE: simulado · apto para escritura";
            UpdateJournalStatus();
        });
    }

    private void RunBlockedSession()
    {
        RunOnBackground(() =>
        {
            WriteLine(string.Empty);
            WriteLine("=== Sesión con Safety Gate bloqueado (el caso que protege al taller) ===");
            ResetStages();

            var session = _service.Begin();

            Step(RepairStage.Connect, () => _service.Connect(session, "USB simulado · hub 2"));
            Step(RepairStage.Identify, () => _service.Identify(session, Fingerprint("MediaTek", "Redmi 9A")));
            Step(RepairStage.Classify, () => _service.Classify(session, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial,
                CoverageEvidence.SeededDemo,
                "requiere auth file legítimo no disponible")));
            Step(RepairStage.Diagnose, () => _service.Diagnose(session, "batería 12% (3550 mV) — por debajo del umbral de escritura"));
            Step(RepairStage.Correlate, () => _service.Correlate(session, "unbrick por BROM"));
            Step(RepairStage.CheckConstraints, () => _service.CheckConstraints(session, "DAA/SLA presente", hardBlocker: false));
            Step(RepairStage.Backup, () => _service.Backup(session, "GPT", verifiedIntegrity: true));
            Step(RepairStage.PlanRepair, () => _service.PlanRepair(session, "reflasheo completo"));

            WriteLine("  Safety Gate con requisitos incompletos: propiedad NO verificada, consentimiento NO firmado, enlace inestable.");
            Step(RepairStage.SafetyGate, () => _service.SafetyGate(session, new SafetyGateInputs(
                OwnershipVerified: false,
                ConsentSigned: false,
                LinkHealthy: false,
                Risk: RiskLevel.BrickRisk,
                SupervisorApproved: false)));

            try
            {
                _service.Repair(session, "esto no debe ejecutarse jamás");
                WriteLine("  FALLO DE INVARIANTE: la etapa Reparar se ejecutó sin autorización.");
            }
            catch (InvalidOperationException ex)
            {
                var repair = Stages.First(stage => stage.Stage == RepairStage.Repair);
                repair.Status = StageStatus.Blocked;
                repair.Detail = ex.Message;
                WriteLine($"  La etapa Reparar fue rechazada: {ex.Message}");
                WriteLine("  Correcto: el equipo del cliente está protegido.");
            }

            WriteLine(string.Empty);
            WriteLine(session.Describe());
            UpdateJournalStatus();
        });
    }

    private void VerifyChain()
    {
        RunOnBackground(() =>
        {
            var verification = _journal.VerifyChain();
            WriteLine(string.Empty);
            WriteLine($"=== Verificación de la cadena · {verification.Message} ===");
            WriteLine($"  Entradas: {verification.Entries} · válida: {(verification.IsValid ? "SÍ" : "NO")}" +
                      (verification.FirstBrokenIndex is null ? string.Empty : $" · primera entrada rota: {verification.FirstBrokenIndex}"));
            UpdateJournalStatus();
        });
    }

    private void Step(RepairStage stage, Action action)
    {
        var viewModel = Stages.First(item => item.Stage == stage);
        viewModel.Status = StageStatus.Active;
        viewModel.Detail = "en curso…";
        WriteLine($"  [{(int)stage:00}] {stage.Label()}");

        try
        {
            action();
            viewModel.Status = StageStatus.Done;
            viewModel.Detail = "completada · evidencia registrada";
        }
        catch (InvalidOperationException ex)
        {
            viewModel.Status = StageStatus.Failed;
            viewModel.Detail = ex.Message;
            WriteLine($"       FALLÓ: {ex.Message}");
        }

        // Pausa deliberada: permite ver el avance etapa a etapa en la interfaz.
        Thread.Sleep(180);
    }

    private void ResetStages()
    {
        foreach (var stage in Stages)
        {
            stage.Status = StageStatus.Pending;
            stage.Detail = "pendiente";
        }
    }

    private void UpdateJournalStatus()
    {
        var verification = _journal.VerifyChain();
        JournalStatus = $"journal: {verification.Entries} entradas · cadena {(verification.IsValid ? "íntegra" : "ROTA")}";
    }

    private void RunOnBackground(Action work)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        Task.Run(() =>
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                WriteLine($"ERROR: {ex.Message}");
            }
            finally
            {
                _busy = false;
            }
        });
    }

    private void WriteLine(string text) => Ui(() => Log += text + Environment.NewLine);

    private static void Ui(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Comando mínimo para el shell. Sin dependencias externas por decisión de diseño.</summary>
internal sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
