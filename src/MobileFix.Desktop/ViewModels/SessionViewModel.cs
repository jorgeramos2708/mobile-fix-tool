using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MobileFix.UseCases;
using MobileFix.Domain;
using MobileFix.Infrastructure;

namespace MobileFix.Desktop.ViewModels;

/// <summary>
/// Vista de la sesión. En este primer corte ejecuta una sesión simulada para que el flujo de
/// 13 etapas y las invariantes sean visibles y verificables sin hardware conectado.
///
/// El trabajo se ejecuta en un hilo de fondo y las actualizaciones se marshalean al hilo de UI:
/// cuando llegue el I/O real (USB, EDL, BROM), la interfaz no se puede bloquear nunca.
/// </summary>
public sealed class SessionViewModel : INotifyPropertyChanged
{
    private readonly string _journalPath;
    private readonly FileJournal _journal;
    private readonly RepairSessionService _service;

    private string _log = string.Empty;
    private string _linkStatus = "ENLACE: sin hardware · modo simulación";
    private string _journalStatus = "journal: —";
    private bool _busy;

    public SessionViewModel()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MobileFixDemo");

        _journalPath = Path.Combine(root, "journal-desktop.jsonl");
        _journal = new FileJournal(_journalPath, new SystemClock());
        _service = new RepairSessionService(_journal, new SystemClock());

        foreach (var stage in RepairStages.Ordered)
        {
            Stages.Add(new StageViewModel(stage));
        }

        RunSessionCommand = new RelayCommand(_ => RunSession(), _ => !_busy);
        RunBlockedCommand = new RelayCommand(_ => RunBlockedSession(), _ => !_busy);
        VerifyCommand = new RelayCommand(_ => VerifyChain(), _ => !_busy);

        WriteLine($"Plataforma MobileFix · pipeline de {RepairStages.Total} etapas");
        WriteLine($"Journal: {_journalPath}");
        WriteLine("Sin hardware conectado. Los datos de esta sesión son simulados.");
        WriteLine("Pulsa «Ejecutar sesión simulada» para recorrer las 13 etapas.");
        UpdateJournalStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StageViewModel> Stages { get; } = [];

    public ICommand RunSessionCommand { get; }

    public ICommand RunBlockedCommand { get; }

    public ICommand VerifyCommand { get; }

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

    private void RunSession()
    {
        RunOnBackground(() =>
        {
            WriteLine(string.Empty);
            WriteLine("=== Sesión simulada: 13 etapas ===");
            ResetStages();

            var session = _service.Begin();

            Step(RepairStage.Connect, () =>
                _service.Connect(session, "USB simulado · puerto 3 del hub 1"));

            Step(RepairStage.Identify, () =>
                _service.Identify(session, new DeviceFingerprint
                {
                    SocVendor = "MediaTek",
                    SocModel = "MT6769 Helio G80",
                    Oem = "Samsung",
                    Model = "Galaxy A13",
                    RegionVariant = "SM-A135M / LATAM",
                    BuildId = "TP1A.220624.014",
                    ConfidencePercent = 98,
                    HighestSource = FingerprintSource.BuildProperties,
                }));

            Step(RepairStage.Classify, () =>
                _service.Classify(session, CoverageAssessment.Evaluate(
                    CoverageVerdict.Partial,
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
            Step(RepairStage.Identify, () => _service.Identify(session, new DeviceFingerprint
            {
                SocVendor = "MediaTek",
                SocModel = "MT6762 Helio G25",
                Oem = "Xiaomi / Redmi / POCO",
                Model = "Redmi 9A",
                ConfidencePercent = 91,
                HighestSource = FingerprintSource.PartitionTable,
                Conflicts = ["board ID no coincide con la variante declarada"],
            }));
            Step(RepairStage.Classify, () => _service.Classify(session, CoverageAssessment.Evaluate(
                CoverageVerdict.Partial, "requiere auth file legítimo no disponible")));
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
                Stages.First(s => s.Stage == RepairStage.Repair).Status = StageStatus.Blocked;
                Stages.First(s => s.Stage == RepairStage.Repair).Detail = ex.Message;
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
        var vm = Stages.First(s => s.Stage == stage);
        vm.Status = StageStatus.Active;
        vm.Detail = "en curso…";
        WriteLine($"  [{(int)stage:00}] {stage.Label()}");

        try
        {
            action();
            vm.Status = StageStatus.Done;
            vm.Detail = "completada · evidencia registrada";
        }
        catch (InvalidOperationException ex)
        {
            vm.Status = StageStatus.Failed;
            vm.Detail = ex.Message;
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
