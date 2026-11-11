using System.Windows;
using Taveloom.Host.Security;

namespace Taveloom.App.Views;

public partial class ApprovalDialog : Window
{
    public bool Approved { get; private set; }

    public ApprovalDialog(PendingPairing p)
    {
        InitializeComponent();
        Services.ThemeService.Hook(this);
        Message.Text = "A phone with a valid pairing code is asking to connect.";
        DeviceName.Text = p.DeviceName;
        Address.Text = $"from {p.RemoteAddress}";
    }

    private void OnAllow(object sender, RoutedEventArgs e) { Approved = true; Close(); }
    private void OnDeny(object sender, RoutedEventArgs e) => Close();
}
