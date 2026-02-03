using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Fast installed programs list using registry (not WMI)
/// </summary>
public partial class InstalledProgramsPage : Page
{
    private List<ProgramsHelper.InstalledProgram> _allPrograms = [];
    private string? _selectedProgramName;
    private Storyboard? _spinnerStoryboard;

    public InstalledProgramsPage()
    {
        InitializeComponent();
        CreateSpinnerAnimation();
        Loaded += OnLoaded;
    }

    private void CreateSpinnerAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(1),
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(animation, LoadingIcon);
        Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));

        _spinnerStoryboard = new Storyboard();
        _spinnerStoryboard.Children.Add(animation);
    }

    private void ShowLoading()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        _spinnerStoryboard?.Begin();
    }

    private void HideLoading()
    {
        _spinnerStoryboard?.Stop();
        LoadingPanel.Visibility = Visibility.Collapsed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshListAsync(showLoading: true);
    }

    private async Task RefreshListAsync(bool showLoading = false)
    {
        _selectedProgramName = (ProgramsList.SelectedItem as ProgramsHelper.InstalledProgram)?.Name;

        if (showLoading)
        {
            ShowLoading();
        }

        var sw = Stopwatch.StartNew();
        var includeSystem = ShowSystemCheckbox.IsChecked == true;
        _allPrograms = await Task.Run(() => ProgramsHelper.GetInstalledPrograms(includeSystem));
        sw.Stop();

        LoadTimeText.Text = $"(loaded in {sw.ElapsedMilliseconds}ms)";

        ApplyFilter();
        HideLoading();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text.Trim().ToLowerInvariant();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allPrograms
            : _allPrograms.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        ProgramsList.ItemsSource = filtered;
        ProgramCountText.Text = $"{filtered.Count} programs";

        var totalSize = ProgramsHelper.GetTotalInstalledSize(filtered);
        TotalSizeText.Text = SystemHelper.FormatBytes(totalSize);

        // Restore selection
        if (_selectedProgramName is not null)
        {
            var previouslySelected = filtered.FirstOrDefault(p => p.Name == _selectedProgramName);
            if (previouslySelected is not null)
            {
                ProgramsList.SelectedItem = previouslySelected;
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

    private void ShowSystem_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }
        _ = RefreshListAsync(showLoading: true);
    }

    private void ProgramsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UninstallButton.IsEnabled = ProgramsList.SelectedItem is not null;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshListAsync(showLoading: true);
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (ProgramsList.SelectedItem is not ProgramsHelper.InstalledProgram program)
        {
            return;
        }

        var result = CustomDialog.Show(
            Lang.UninstallProgram,
            string.Format(Lang.UninstallConfirm, program.Name),
            CustomDialog.DialogType.Warning,
            Lang.Uninstall,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            var success = await ProgramsHelper.UninstallProgramAsync(program);
            if (!success)
            {
                CustomDialog.Show(
                    Lang.Error,
                    string.Format(Lang.CouldNotStartUninstaller, program.Name),
                    CustomDialog.DialogType.Error);
            }
            // Don't refresh immediately - uninstaller is running in background
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
