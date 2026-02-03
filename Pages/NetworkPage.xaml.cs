using System.Windows;
using System.Windows.Controls;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Network information and tools page
/// </summary>
public partial class NetworkPage : Page
{
    public NetworkPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        LocalIpText.Text = "Loading...";
        PublicIpText.Text = "Loading...";
        ConnectionCountText.Text = "...";

        var data = await Task.Run(() =>
        {
            var localIp = NetworkHelper.GetLocalIpAddress();
            var adapters = NetworkHelper.GetNetworkAdapters();
            var connections = NetworkHelper.GetTcpConnections();
            return (localIp, adapters, connections);
        });

        LocalIpText.Text = data.localIp;
        AdaptersList.ItemsSource = data.adapters;
        ConnectionsList.ItemsSource = data.connections;
        ConnectionCountText.Text = data.connections.Count.ToString();

        // Public IP is a network call, do it separately
        var publicIp = await NetworkHelper.GetPublicIpAsync();
        PublicIpText.Text = publicIp;
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (AdaptersTab?.IsChecked == true)
        {
            if (AdaptersPanel != null)
            {
                AdaptersPanel.Visibility = Visibility.Visible;
            }
            if (ConnectionsPanel != null)
            {
                ConnectionsPanel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            if (AdaptersPanel != null)
            {
                AdaptersPanel.Visibility = Visibility.Collapsed;
            }
            if (ConnectionsPanel != null)
            {
                ConnectionsPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        var success = await CleanupHelper.FlushDnsAsync();
        if (success)
        {
            CustomDialog.Show(
                Lang.DnsFlushed,
                Lang.DnsFlushedMessage,
                CustomDialog.DialogType.Info);
        }
        else
        {
            CustomDialog.Show(
                Lang.Error,
                Lang.CouldNotFlushDns,
                CustomDialog.DialogType.Error);
        }
    }

    private void Border_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }
        border.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 12, 12);
    }
}
