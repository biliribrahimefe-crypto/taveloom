using System.Windows;

namespace Taveloom.App;

public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); Services.ThemeService.Hook(this); }
}
