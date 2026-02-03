using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SysPilot.Controls;

/// <summary>
/// Circular gauge control for displaying percentage values
/// </summary>
public partial class GaugeControl : UserControl
{
    private const double StartAngle = 135;
    private const double EndAngle = 405;
    private const double Radius = 52;
    private const double CenterX = 60;
    private const double CenterY = 60;

    public GaugeControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawBackgroundArc();
        UpdateValueArc();
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeControl),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(GaugeControl),
            new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty GaugeColorProperty =
        DependencyProperty.Register(nameof(GaugeColor), typeof(Brush), typeof(GaugeControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), OnColorChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(GaugeControl),
            new PropertyMetadata(null, OnDisplayTextChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Brush GaugeColor
    {
        get => (Brush)GetValue(GaugeColorProperty);
        set => SetValue(GaugeColorProperty, value);
    }

    public string? DisplayText
    {
        get => (string?)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl gauge)
        {
            gauge.UpdateValueArc();
            if (string.IsNullOrEmpty(gauge.DisplayText))
            {
                gauge.ValueText.Text = $"{gauge.Value:0}%";
            }
        }
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl gauge)
        {
            gauge.LabelText.Text = (string)e.NewValue;
        }
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl gauge)
        {
            gauge.UpdateValueArc();
        }
    }

    private static void OnDisplayTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GaugeControl gauge)
        {
            gauge.ValueText.Text = (string?)e.NewValue ?? $"{gauge.Value:0}%";
        }
    }

    private void DrawBackgroundArc()
    {
        var geometry = CreateArcGeometry(StartAngle, EndAngle);
        BackgroundArc.Data = geometry;
    }

    private void UpdateValueArc()
    {
        if (!IsLoaded)
        {
            return;
        }

        var clampedValue = Math.Clamp(Value, 0, 100);
        var sweepAngle = (EndAngle - StartAngle) * (clampedValue / 100.0);
        var endAngle = StartAngle + sweepAngle;

        if (sweepAngle < 1)
        {
            ValueArc.Data = null!;
            return;
        }

        var geometry = CreateArcGeometry(StartAngle, endAngle);
        ValueArc.Data = geometry;
        ValueArc.Stroke = GaugeColor;
    }

    private static PathGeometry CreateArcGeometry(double startAngle, double endAngle)
    {
        var startRad = startAngle * Math.PI / 180;
        var endRad = endAngle * Math.PI / 180;

        var startX = CenterX + Radius * Math.Cos(startRad);
        var startY = CenterY + Radius * Math.Sin(startRad);
        var endX = CenterX + Radius * Math.Cos(endRad);
        var endY = CenterY + Radius * Math.Sin(endRad);

        var isLargeArc = (endAngle - startAngle) > 180;

        var figure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };

        var arc = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(Radius, Radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        };

        figure.Segments.Add(arc);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
