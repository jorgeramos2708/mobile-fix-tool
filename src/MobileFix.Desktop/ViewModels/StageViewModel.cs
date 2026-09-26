using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MobileFix.Domain;

namespace MobileFix.Desktop.ViewModels;

/// <summary>
/// Una etapa del pipeline en la interfaz. El color y la forma siguen DESIGN-TOKENS §3.3:
/// ningún estado depende solo del color, siempre hay texto de estado.
/// </summary>
public sealed class StageViewModel : INotifyPropertyChanged
{
    private static readonly Brush PendingBrush = Freeze(0x6B, 0x72, 0x80);
    private static readonly Brush ActiveBrush = Freeze(0x25, 0x63, 0xEB);
    private static readonly Brush DoneBrush = Freeze(0x05, 0x96, 0x69);
    private static readonly Brush FailedBrush = Freeze(0xDC, 0x26, 0x26);
    private static readonly Brush BlockedBrush = Freeze(0xD9, 0x77, 0x06);
    private static readonly Brush SkippedBrush = Freeze(0x9A, 0xA3, 0xB2);

    private StageStatus _status = StageStatus.Pending;
    private string _detail = "pendiente";

    public StageViewModel(RepairStage stage)
    {
        Stage = stage;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RepairStage Stage { get; }

    public string Title => $"{(int)Stage:00} · {Stage.Label()}";

    public string Kind => Stage.IsWriteStage()
        ? "ESCRIBE"
        : Stage.IsReadOnly()
            ? "solo lectura"
            : "sin acceso";

    public StageStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail == value)
            {
                return;
            }

            _detail = value;
            OnPropertyChanged();
        }
    }

    public string StatusText => Status switch
    {
        StageStatus.Pending => "pendiente",
        StageStatus.Active => "en curso",
        StageStatus.Done => "completada",
        StageStatus.Failed => "FALLÓ",
        StageStatus.Blocked => "BLOQUEADA",
        StageStatus.Skipped => "omitida",
        _ => Status.ToString(),
    };

    public Brush StatusBrush => Status switch
    {
        StageStatus.Active => ActiveBrush,
        StageStatus.Done => DoneBrush,
        StageStatus.Failed => FailedBrush,
        StageStatus.Blocked => BlockedBrush,
        StageStatus.Skipped => SkippedBrush,
        _ => PendingBrush,
    };

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
