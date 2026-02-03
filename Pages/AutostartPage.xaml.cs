using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Autostart manager page for managing startup programs
/// </summary>
public partial class AutostartPage : Page
{
    private List<RegistryHelper.AutostartEntry> _allEntries = [];
    private string? _selectedEntryName;
    private Storyboard? _spinnerStoryboard;

    public AutostartPage()
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
        // Remember selection
        _selectedEntryName = (AutostartList.SelectedItem as RegistryHelper.AutostartEntry)?.Name;

        if (showLoading)
        {
            ShowLoading();
        }

        _allEntries = await Task.Run(() => RegistryHelper.GetAutostartEntries());
        ApplyFilter();

        HideLoading();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text.Trim().ToLowerInvariant();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allEntries
            : _allEntries.Where(e =>
                e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                e.Command.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        AutostartList.ItemsSource = filtered;
        EntryCountText.Text = $"{filtered.Count} of {_allEntries.Count} entries";

        // Restore selection
        if (_selectedEntryName is not null)
        {
            var previouslySelected = filtered.FirstOrDefault(e => e.Name == _selectedEntryName);
            if (previouslySelected is not null)
            {
                AutostartList.SelectedItem = previouslySelected;
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

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkbox &&
            checkbox.DataContext is RegistryHelper.AutostartEntry entry)
        {
            bool enable = checkbox.IsChecked == true;

            var success = await Task.Run(() =>
                RegistryHelper.SetAutostartEnabled(entry, enable));

            if (!success)
            {
                var action = enable ? Lang.Enable.ToLower() : Lang.Disable.ToLower();
                var message = entry.Source == RegistryHelper.AutostartSource.TaskScheduler
                    ? string.Format(Lang.CouldNotToggleAutostartTask, action, entry.Name)
                    : string.Format(Lang.CouldNotToggleAutostart, action, entry.Name);

                CustomDialog.Show(Lang.Error, message, CustomDialog.DialogType.Error);
                checkbox.IsChecked = !enable;
            }
            else
            {
                await RefreshListAsync();
            }
        }
    }

    private void AutostartList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AutostartList.SelectedItem is RegistryHelper.AutostartEntry entry)
        {
            // Task Scheduler entries can't be deleted from here
            DeleteButton.IsEnabled = entry.Source != RegistryHelper.AutostartSource.TaskScheduler;
        }
        else
        {
            DeleteButton.IsEnabled = false;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshListAsync(showLoading: true);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (AutostartList.SelectedItem is not RegistryHelper.AutostartEntry entry)
        {
            return;
        }

        if (entry.Source == RegistryHelper.AutostartSource.TaskScheduler)
        {
            CustomDialog.Show(
                Lang.CannotRemove,
                Lang.CannotRemoveTaskScheduler,
                CustomDialog.DialogType.Info);
            return;
        }

        var sourceDescription = entry.Source == RegistryHelper.AutostartSource.StartupFolder
            ? Lang.RemoveStartupFolderNote
            : Lang.RemoveRegistryNote;

        var result = CustomDialog.Show(
            Lang.RemoveAutostartEntry,
            $"{string.Format(Lang.RemoveAutostartConfirm, entry.Name)}\n\n{sourceDescription}\n\n{Lang.ActionCannotBeUndone}",
            CustomDialog.DialogType.Warning,
            Lang.Remove,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            var success = await Task.Run(() =>
                RegistryHelper.RemoveAutostartEntry(entry));

            if (success)
            {
                await RefreshListAsync(showLoading: true);
            }
            else
            {
                CustomDialog.Show(
                    Lang.Error,
                    string.Format(Lang.CouldNotRemoveAutostart, entry.Name),
                    CustomDialog.DialogType.Error);
            }
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
