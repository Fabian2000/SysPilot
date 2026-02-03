using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Task manager page for viewing and managing processes
/// </summary>
public partial class TaskManagerPage : Page
{
    // P/Invoke for window picker
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private const uint GA_ROOTOWNER = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private List<ProcessHelper.ProcessInfo> _allProcesses = [];
    private readonly ObservableCollection<ProcessHelper.ListItemBase> _displayItems = [];
    private readonly DispatcherTimer _refreshTimer;
    private Storyboard? _spinnerStoryboard;
    private bool _isFirstLoad = true;
    private bool _isPickingWindow;

    // Group headers (persistent across refreshes)
    private readonly ProcessHelper.GroupHeader _appsHeader = new(ProcessHelper.ProcessCategory.App, 0);
    private readonly ProcessHelper.GroupHeader _backgroundHeader = new(ProcessHelper.ProcessCategory.Background, 0) { IsCollapsed = true };
    private readonly ProcessHelper.GroupHeader _windowsHeader = new(ProcessHelper.ProcessCategory.Windows, 0) { IsCollapsed = true };

    // Sorting
    private string _sortColumn = "Memory";
    private bool _sortDescending = true;

    public TaskManagerPage()
    {
        InitializeComponent();
        CreateSpinnerAnimation();

        ProcessListBox.ItemsSource = _displayItems;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshProcessListAsync(showLoading: false);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
        await RefreshProcessListAsync(showLoading: true);
        _refreshTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async Task RefreshProcessListAsync(bool showLoading = false)
    {
        // Remember selected IDs
        var selectedIds = ProcessListBox.SelectedItems
            .OfType<ProcessHelper.ProcessInfo>()
            .Select(p => p.Id)
            .ToHashSet();

        if (showLoading || _isFirstLoad)
        {
            ShowLoading();
            _isFirstLoad = false;
        }

        _allProcesses = await Task.Run(() => ProcessHelper.GetProcesses(fullRefresh: showLoading));

        BuildDisplayList();

        // Restore selection
        ProcessListBox.SelectionChanged -= ProcessListBox_SelectionChanged;
        foreach (var item in _displayItems.OfType<ProcessHelper.ProcessInfo>().Where(p => selectedIds.Contains(p.Id)))
        {
            ProcessListBox.SelectedItems.Add(item);
        }
        ProcessListBox.SelectionChanged += ProcessListBox_SelectionChanged;
        UpdateEndTaskButton();

        HideLoading();
    }

    private void BuildDisplayList()
    {
        var filter = SearchBox.Text.Trim().ToLowerInvariant();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Id.ToString().Contains(filter)).ToList();

        // Group and sort
        var apps = SortProcesses(filtered.Where(p => p.Category == ProcessHelper.ProcessCategory.App));
        var background = SortProcesses(filtered.Where(p => p.Category == ProcessHelper.ProcessCategory.Background));
        var windows = SortProcesses(filtered.Where(p => p.Category == ProcessHelper.ProcessCategory.Windows));

        // Update counts
        _appsHeader.Count = apps.Count;
        _backgroundHeader.Count = background.Count;
        _windowsHeader.Count = windows.Count;

        // Build flat list
        _displayItems.Clear();

        // Apps
        _displayItems.Add(_appsHeader);
        if (!_appsHeader.IsCollapsed)
        {
            foreach (var p in apps)
            {
                _displayItems.Add(p);
            }
        }

        // Background
        _displayItems.Add(_backgroundHeader);
        if (!_backgroundHeader.IsCollapsed)
        {
            foreach (var p in background)
            {
                _displayItems.Add(p);
            }
        }

        // Windows
        _displayItems.Add(_windowsHeader);
        if (!_windowsHeader.IsCollapsed)
        {
            foreach (var p in windows)
            {
                _displayItems.Add(p);
            }
        }

        ProcessCountText.Text = $"{filtered.Count} of {_allProcesses.Count} processes";
        UpdateEndTaskButton();
    }

    private void UpdateEndTaskButton()
    {
        var count = ProcessListBox.SelectedItems.OfType<ProcessHelper.ProcessInfo>().Count();
        EndTaskButton.IsEnabled = count > 0;

        var icon = new PackIconMaterial { Kind = PackIconMaterialKind.Close, Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock { Text = count > 1 ? string.Format(Lang.EndTasks, count) : Lang.EndTask, Margin = new Thickness(8, 0, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(icon);
        panel.Children.Add(text);
        EndTaskButton.Content = panel;
    }

    private void GroupHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ProcessHelper.GroupHeader header)
        {
            return;
        }

        header.IsCollapsed = !header.IsCollapsed;
        BuildDisplayList();
    }

    private void ContentClipBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        var rect = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 12, 12);
        border.Clip = rect;
    }

    private void SortHeader_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string column)
        {
            return;
        }

        if (_sortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column;
            _sortDescending = column == "Memory"; // Default descending for Memory
        }

        UpdateSortIcons();
        BuildDisplayList();
    }

    private void UpdateSortIcons()
    {
        // Hide all
        SortIconName.Visibility = Visibility.Collapsed;
        SortIconCPU.Visibility = Visibility.Collapsed;
        SortIconMemory.Visibility = Visibility.Collapsed;
        SortIconDisk.Visibility = Visibility.Collapsed;

        // Show active
        var activeIcon = _sortColumn switch
        {
            "Name" => SortIconName,
            "CPU" => SortIconCPU,
            "Memory" => SortIconMemory,
            "Disk" => SortIconDisk,
            _ => null
        };

        if (activeIcon is not null)
        {
            activeIcon.Visibility = Visibility.Visible;
            activeIcon.Kind = _sortDescending
                ? MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronDown
                : MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronUp;
        }
    }

    private List<ProcessHelper.ProcessInfo> SortProcesses(IEnumerable<ProcessHelper.ProcessInfo> processes)
    {
        var sorted = _sortColumn switch
        {
            "Name" => _sortDescending
                ? processes.OrderByDescending(p => p.Name)
                : processes.OrderBy(p => p.Name),
            "CPU" => _sortDescending
                ? processes.OrderByDescending(p => p.CpuPercent)
                : processes.OrderBy(p => p.CpuPercent),
            "Memory" => _sortDescending
                ? processes.OrderByDescending(p => p.MemoryBytes)
                : processes.OrderBy(p => p.MemoryBytes),
            "Disk" => _sortDescending
                ? processes.OrderByDescending(p => p.DiskBytesPerSec)
                : processes.OrderBy(p => p.DiskBytesPerSec),
            _ => processes.OrderByDescending(p => p.MemoryBytes)
        };
        return [.. sorted];
    }

    private void ProcessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Deselect any headers that got selected
        foreach (var item in e.AddedItems.OfType<ProcessHelper.GroupHeader>().ToList())
        {
            ProcessListBox.SelectedItems.Remove(item);
        }

        UpdateEndTaskButton();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        BuildDisplayList();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshProcessListAsync(showLoading: true);
    }

    private async void EndTask_Click(object sender, RoutedEventArgs e)
    {
        var selectedProcesses = ProcessListBox.SelectedItems.OfType<ProcessHelper.ProcessInfo>().ToList();
        if (selectedProcesses.Count == 0)
        {
            return;
        }

        string message = selectedProcesses.Count == 1
            ? $"{string.Format(Lang.EndTaskConfirm, selectedProcesses[0].Name, selectedProcesses[0].Id)}\n\n{Lang.UnsavedDataWarning}"
            : $"{string.Format(Lang.EndTasksConfirm, selectedProcesses.Count)}\n\n{Lang.UnsavedDataWarning}";

        string title = selectedProcesses.Count == 1 ? Lang.EndTask : string.Format(Lang.EndTasks, selectedProcesses.Count);

        var result = CustomDialog.Show(
            title,
            message,
            CustomDialog.DialogType.Warning,
            title,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            var failed = new List<string>();
            foreach (var process in selectedProcesses)
            {
                if (!ProcessHelper.KillProcess(process.Id))
                {
                    failed.Add(process.Name);
                }
            }

            if (failed.Count > 0)
            {
                CustomDialog.Show(
                    Lang.Error,
                    $"{string.Format(Lang.EndTasksError, failed.Count)}\n{string.Join(", ", failed.Take(5))}{(failed.Count > 5 ? "..." : "")}\n\n{Lang.ProcessEndedOrElevated}",
                    CustomDialog.DialogType.Error);
            }

            await RefreshProcessListAsync();
        }
    }

    private ProcessHelper.ProcessInfo? GetSelectedProcess()
    {
        return ProcessListBox.SelectedItem as ProcessHelper.ProcessInfo;
    }

    private void ShowDetails_Click(object sender, RoutedEventArgs e)
    {
        var process = GetSelectedProcess();
        if (process is null)
        {
            return;
        }

        string fileSize = "Unknown";
        string fileVersion = "Unknown";
        string company = "Unknown";

        try
        {
            if (!string.IsNullOrEmpty(process.FilePath) && File.Exists(process.FilePath))
            {
                var fileInfo = new FileInfo(process.FilePath);
                fileSize = SystemHelper.FormatBytes(fileInfo.Length);

                var versionInfo = FileVersionInfo.GetVersionInfo(process.FilePath);
                fileVersion = versionInfo.FileVersion ?? "Unknown";
                company = versionInfo.CompanyName ?? "Unknown";
            }
        }
        catch { }

        // Build rich content with bold labels
        var content = new StackPanel { MaxWidth = 380 };

        void AddRow(string label, string value, bool addMargin = false)
        {
            var row = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xB0, 0xB0)),
                FontSize = 13,
                Margin = addMargin ? new Thickness(0, 8, 0, 0) : new Thickness(0)
            };
            row.Inlines.Add(new System.Windows.Documents.Run(label) { FontWeight = FontWeights.SemiBold });
            row.Inlines.Add(new System.Windows.Documents.Run(" " + value));
            content.Children.Add(row);
        }

        AddRow("Name:", process.Name);
        AddRow("PID:", process.Id.ToString());
        AddRow("Status:", process.Status);

        AddRow("Description:", string.IsNullOrEmpty(process.Description) ? "N/A" : process.Description, addMargin: true);
        AddRow("Company:", company);
        AddRow("Version:", fileVersion);

        AddRow("Path:", string.IsNullOrEmpty(process.FilePath) ? "N/A" : process.FilePath, addMargin: true);
        AddRow("File Size:", fileSize);

        AddRow("Memory:", process.MemoryFormatted, addMargin: true);
        AddRow("CPU:", process.CpuFormatted);
        AddRow("Disk:", process.DiskFormatted);

        CustomDialog.ShowRichContent(Lang.ProcessDetails, content);
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        var process = GetSelectedProcess();
        if (process == null || string.IsNullOrEmpty(process.FilePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{process.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            CustomDialog.Show(Lang.Error, Lang.CouldNotOpenFileLocation, CustomDialog.DialogType.Error);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var process = GetSelectedProcess();
        if (process == null || string.IsNullOrEmpty(process.FilePath))
        {
            return;
        }

        try
        {
            Clipboard.SetText(process.FilePath);
        }
        catch { }
    }

    private async void ContextEndTask_Click(object sender, RoutedEventArgs e)
    {
        var process = GetSelectedProcess();
        if (process is null)
        {
            return;
        }

        var result = CustomDialog.Show(
            Lang.EndTask,
            $"{string.Format(Lang.EndTaskConfirm, process.Name, process.Id)}\n\n{Lang.UnsavedDataWarning}",
            CustomDialog.DialogType.Warning,
            Lang.EndTask,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (ProcessHelper.KillProcess(process.Id))
            {
                await RefreshProcessListAsync();
            }
            else
            {
                CustomDialog.Show(
                    Lang.Error,
                    $"{string.Format(Lang.EndTaskError, process.Name)}\n{Lang.ProcessEndedOrElevated}",
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        RunDialog.Show();
    }

    #region Window Picker

    private void WindowPicker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isPickingWindow = true;
        Mouse.Capture(WindowPickerButton);
        Mouse.OverrideCursor = Cursors.Cross;
        WindowPickerIcon.Kind = PackIconMaterialKind.CrosshairsGps;
        e.Handled = true;
    }

    private void WindowPicker_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPickingWindow)
        {
            return;
        }
        // Cursor stays as crosshair via OverrideCursor
    }

    private void WindowPicker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPickingWindow)
        {
            return;
        }

        _isPickingWindow = false;
        Mouse.Capture(null);
        Mouse.OverrideCursor = null;
        WindowPickerIcon.Kind = PackIconMaterialKind.Crosshairs;

        // Get window under cursor
        if (GetCursorPos(out POINT pt))
        {
            var hwnd = WindowFromPoint(pt);
            if (hwnd != IntPtr.Zero)
            {
                // Get root owner window (in case we hit a child window)
                var rootHwnd = GetAncestor(hwnd, GA_ROOTOWNER);
                if (rootHwnd != IntPtr.Zero)
                    hwnd = rootHwnd;

                // Get process ID from window
                GetWindowThreadProcessId(hwnd, out uint processId);

                if (processId > 0)
                {
                    SelectProcessById((int)processId);
                }
            }
        }

        e.Handled = true;
    }

    private void SelectProcessById(int processId)
    {
        // Find the process in the list
        var process = _displayItems.OfType<ProcessHelper.ProcessInfo>()
            .FirstOrDefault(p => p.Id == processId);

        if (process is not null)
        {
            // Expand the group if collapsed
            var header = process.Category switch
            {
                ProcessHelper.ProcessCategory.App => _appsHeader,
                ProcessHelper.ProcessCategory.Background => _backgroundHeader,
                ProcessHelper.ProcessCategory.Windows => _windowsHeader,
                _ => null
            };

            if (header is not null && header.IsCollapsed)
            {
                header.IsCollapsed = false;
                BuildDisplayList();
            }

            // Select and scroll to the process
            ProcessListBox.SelectedItem = process;
            ProcessListBox.ScrollIntoView(process);
        }
        else
        {
            // Process might not be in the list yet, try refreshing
            CustomDialog.Show(
                Lang.ProcessNotFound,
                string.Format(Lang.ProcessNotFoundMessage, processId),
                CustomDialog.DialogType.Info);
        }
    }

    #endregion
}
