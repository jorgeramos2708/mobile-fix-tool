using System.Windows;
using MobileFix.Desktop.ViewModels;

namespace MobileFix.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new SessionViewModel();
    }
}
