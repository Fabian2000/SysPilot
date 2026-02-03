using System.Windows;
using System.Windows.Controls;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Services manager page for viewing and controlling Windows services
/// </summary>
public partial class ServicesPage : Page
{
    private List<ServiceHelper.ServiceInfo> _allServices = [];
    private string? _selectedServiceName;

    public ServicesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshServicesAsync();
    }

    private async Task RefreshServicesAsync()
    {
        // Remember selection
        _selectedServiceName = (ServicesList.SelectedItem as ServiceHelper.ServiceInfo)?.Name;

        _allServices = await Task.Run(() => ServiceHelper.GetServices());
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text.Trim().ToLowerInvariant();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allServices
            : _allServices.Where(s =>
                s.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        ServicesList.ItemsSource = filtered;
        ServiceCountText.Text = $"{filtered.Count} of {_allServices.Count} services";

        // Restore selection
        if (_selectedServiceName is not null)
        {
            var previouslySelected = filtered.FirstOrDefault(s => s.Name == _selectedServiceName);
            if (previouslySelected is not null)
            {
                ServicesList.SelectedItem = previouslySelected;
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyFilter();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshServicesAsync();
    }

    private void ServicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServicesList.SelectedItem is ServiceHelper.ServiceInfo service)
        {
            StartButton.IsEnabled = service.CanStart;
            StopButton.IsEnabled = service.CanStop;
            RestartButton.IsEnabled = service.Status == "Running";
        }
        else
        {
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            RestartButton.IsEnabled = false;
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesList.SelectedItem is not ServiceHelper.ServiceInfo service)
        {
            return;
        }

        if (!AdminHelper.RequireAdmin())
        {
            return;
        }

        SetButtonsEnabled(false);

        var success = await ServiceHelper.StartServiceAsync(service.Name);
        if (!success)
        {
            CustomDialog.Show(
                Lang.Error,
                string.Format(Lang.CouldNotStartService, service.DisplayName),
                CustomDialog.DialogType.Error);
        }

        await RefreshServicesAsync();
        SetButtonsEnabled(true);
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesList.SelectedItem is not ServiceHelper.ServiceInfo service)
        {
            return;
        }

        if (!AdminHelper.RequireAdmin())
        {
            return;
        }

        var result = CustomDialog.Show(
            Lang.StopService,
            string.Format(Lang.StopServiceConfirm, service.DisplayName),
            CustomDialog.DialogType.Warning,
            Lang.Stop,
            Lang.Cancel);

        if (result != CustomDialog.ButtonResult.Primary)
        {
            return;
        }

        SetButtonsEnabled(false);

        var success = await ServiceHelper.StopServiceAsync(service.Name);
        if (!success)
        {
            CustomDialog.Show(
                Lang.Error,
                string.Format(Lang.CouldNotStopService, service.DisplayName),
                CustomDialog.DialogType.Error);
        }

        await RefreshServicesAsync();
        SetButtonsEnabled(true);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesList.SelectedItem is not ServiceHelper.ServiceInfo service)
        {
            return;
        }

        if (!AdminHelper.RequireAdmin())
        {
            return;
        }

        SetButtonsEnabled(false);

        var success = await ServiceHelper.RestartServiceAsync(service.Name);
        if (!success)
        {
            CustomDialog.Show(
                Lang.Error,
                string.Format(Lang.CouldNotRestartService, service.DisplayName),
                CustomDialog.DialogType.Error);
        }

        await RefreshServicesAsync();
        SetButtonsEnabled(true);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        StartButton.IsEnabled = enabled;
        StopButton.IsEnabled = enabled;
        RestartButton.IsEnabled = enabled;
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
