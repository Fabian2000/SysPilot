using System.Windows;
using System.Windows.Input;
using MahApps.Metro.IconPacks;
using SysPilot.Controls;
using SysPilot.Helpers;
using SysPilot.Pages;

namespace SysPilot;

public partial class MainWindow : Window
{
    private readonly Dictionary<Type, object> _pageCache = new();

    public MainWindow()
    {
        InitializeComponent();

        // Set version from generated AppInfo (compile-time constant)
        VersionText.Text = $"v{AppInfo.Version}";

        // Official SystemCommands bindings for smooth window operations
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimizeWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow));

        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Show admin shield if running as administrator
        if (AdminHelper.IsAdmin)
        {
            AdminShield.Visibility = Visibility.Visible;
        }

        // Navigate to dashboard by default
        NavigateToPage(typeof(DashboardPage));
    }

    private void NavButton_Selected(object? sender, EventArgs e)
    {
        if (sender is SidebarButton button && button.PageType != null)
        {
            // Update selection state
            DeselectAllNavButtons();
            button.IsSelected = true;

            // Navigate to page
            NavigateToPage(button.PageType);
        }
    }

    private void DeselectAllNavButtons()
    {
        NavDashboard.IsSelected = false;
        NavPerformance.IsSelected = false;
        NavTasks.IsSelected = false;
        NavAutostart.IsSelected = false;
        NavServices.IsSelected = false;
        NavNetwork.IsSelected = false;
        NavCleanup.IsSelected = false;
        NavPrograms.IsSelected = false;
        NavHardware.IsSelected = false;
        NavPower.IsSelected = false;
    }

    private void NavigateToPage(Type pageType)
    {
        // Use cached page if available
        if (!_pageCache.TryGetValue(pageType, out var page))
        {
            page = Activator.CreateInstance(pageType);
            if (page is not null)
            {
                _pageCache[pageType] = page;
            }
        }

        ContentFrame.Navigate(page);
    }

    private void OnMinimizeWindow(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void OnMaximizeWindow(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MaximizeWindow(this);
    }

    private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.RestoreWindow(this);
    }

    private void OnCloseWindow(object sender, ExecutedRoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            BtnMaximize.Visibility = Visibility.Collapsed;
            BtnRestore.Visibility = Visibility.Visible;
        }
        else
        {
            BtnMaximize.Visibility = Visibility.Visible;
            BtnRestore.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;

        if (Topmost)
        {
            PinIcon.Kind = PackIconMaterialKind.Pin;
            BtnPin.ToolTip = Lang.UnpinFromTop;
        }
        else
        {
            PinIcon.Kind = PackIconMaterialKind.PinOutline;
            BtnPin.ToolTip = Lang.PinOnTop;
        }
    }
}
