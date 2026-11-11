using System.Windows;
using System.Windows.Controls;
using Taveloom.App.ViewModels;

namespace Taveloom.App.Views;

public partial class DevicesView : UserControl
{
    public DevicesView() => InitializeComponent();

    private void OnNameCommitted(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeviceItem d } && DataContext is DevicesViewModel vm) vm.Rename(d);
    }
}
