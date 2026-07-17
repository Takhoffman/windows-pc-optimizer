using System.Windows;
using System.Windows.Controls;

namespace Velocity.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
        StateChanged += (_, _) =>
            MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (DashboardPanel is null) return; // fired during InitializeComponent

        DashboardPanel.Visibility = ReferenceEquals(sender, NavDashboard) ? Visibility.Visible : Visibility.Collapsed;
        TweaksPanel.Visibility = ReferenceEquals(sender, NavTweaks) ? Visibility.Visible : Visibility.Collapsed;
        ProfilesPanel.Visibility = ReferenceEquals(sender, NavProfiles) ? Visibility.Visible : Visibility.Collapsed;
        RestorePanel.Visibility = ReferenceEquals(sender, NavRestore) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
