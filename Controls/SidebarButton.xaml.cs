using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SysPilot.Controls;

/// <summary>
/// Sidebar navigation button control
/// </summary>
public partial class SidebarButton : UserControl
{
    public SidebarButton()
    {
        InitializeComponent();
        MouseLeftButtonUp += OnClick;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SidebarButton),
            new PropertyMetadata(""));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(object), typeof(SidebarButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(SidebarButton),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty PageTypeProperty =
        DependencyProperty.Register(nameof(PageType), typeof(Type), typeof(SidebarButton),
            new PropertyMetadata(null));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Type? PageType
    {
        get => (Type?)GetValue(PageTypeProperty);
        set => SetValue(PageTypeProperty, value);
    }

    public event EventHandler? Selected;

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SidebarButton button)
        {
            button.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (IsSelected)
        {
            SelectionIndicator.Visibility = Visibility.Visible;
            ButtonBorder.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
            LabelText.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        }
        else
        {
            SelectionIndicator.Visibility = Visibility.Collapsed;
            ButtonBorder.Background = Brushes.Transparent;
            LabelText.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
        }
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        Selected?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        ButtonBorder.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        ButtonBorder.Background = IsSelected
            ? new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25))
            : Brushes.Transparent;
    }
}
