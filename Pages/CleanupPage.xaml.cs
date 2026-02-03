using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Disk cleanup page for removing temporary files and caches
/// </summary>
public partial class CleanupPage : Page
{
    private List<CleanupHelper.CleanupTarget> _targets = [];
    private CancellationTokenSource? _cleanupCts;
    private Storyboard? _spinnerStoryboard;

    public CleanupPage()
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

    private void StartSpinner()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        _spinnerStoryboard?.Begin();
    }

    private void StopSpinner()
    {
        _spinnerStoryboard?.Stop();
        LoadingPanel.Visibility = Visibility.Collapsed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ScanTargetsAsync();
    }

    private async Task ScanTargetsAsync()
    {
        TargetsList.Visibility = Visibility.Collapsed;
        StartSpinner();
        LoadingText.Text = "Scanning...";

        _targets = await CleanupHelper.GetCleanupTargetsAsync();

        TargetsList.ItemsSource = _targets;
        TargetsList.Visibility = Visibility.Visible;
        StopSpinner();

        UpdateTotalSize();
    }

    private void UpdateTotalSize()
    {
        var selectedSize = _targets.Where(t => t.IsSelected).Sum(t => t.SizeBytes);
        TotalSizeText.Text = $"Selected: {SystemHelper.FormatBytes(selectedSize)}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ScanTargetsAsync();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = true;
        }
        TargetsList.Items.Refresh();
        UpdateTotalSize();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = false;
        }
        TargetsList.Items.Refresh();
        UpdateTotalSize();
    }

    private void TargetItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.DataContext is CleanupHelper.CleanupTarget target)
        {
            target.IsSelected = !target.IsSelected;
            TargetsList.Items.Refresh();
            UpdateTotalSize();
        }
    }

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0)
        {
            CustomDialog.Show(
                Lang.NoSelection,
                Lang.PleaseSelectItem,
                CustomDialog.DialogType.Info);
            return;
        }

        // Check if any selected target requires admin (Windows folders)
        var needsAdmin = selectedTargets.Any(t =>
            t.Path.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase) ||
            t.Path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), StringComparison.OrdinalIgnoreCase));

        if (needsAdmin && !AdminHelper.RequireAdmin())
            return;

        var totalSize = selectedTargets.Sum(t => t.SizeBytes);
        var result = CustomDialog.Show(
            Lang.ConfirmCleanup,
            string.Format(Lang.CleanupConfirmMessage, SystemHelper.FormatBytes(totalSize)),
            CustomDialog.DialogType.Warning,
            Lang.Clean,
            Lang.Cancel);

        if (result != CustomDialog.ButtonResult.Primary)
        {
            return;
        }

        CleanButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        ProgressBar.Maximum = 100;

        _cleanupCts = new CancellationTokenSource();

        var progress = new Progress<CleanupHelper.CleanupProgress>(p =>
        {
            ProgressBar.IsIndeterminate = p.IsIndeterminate;

            if (!p.IsIndeterminate && p.TotalBytes > 0)
            {
                ProgressBar.Value = (double)p.BytesFreed / p.TotalBytes * 100;
            }

            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                ProgressText.Text = p.CurrentFile;
            }
            else
            {
                ProgressText.Text = $"Cleaning... {p.FilesDeleted} files deleted ({SystemHelper.FormatBytes(p.BytesFreed)} freed)";
            }
        });

        try
        {
            await CleanupHelper.CleanupAsync(selectedTargets, progress, _cleanupCts.Token);

            CustomDialog.Show(
                Lang.CleanupComplete,
                Lang.CleanupCompleteMessage,
                CustomDialog.DialogType.Info);
        }
        catch (OperationCanceledException)
        {
            CustomDialog.Show(
                Lang.Cancelled,
                Lang.CleanupCancelled,
                CustomDialog.DialogType.Info);
        }
        finally
        {
            CleanButton.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
            _cleanupCts?.Dispose();
            _cleanupCts = null;

            await ScanTargetsAsync();
        }
    }
}
