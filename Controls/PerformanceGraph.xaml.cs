using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SysPilot.Controls;

public partial class PerformanceGraph : UserControl
{
    private readonly List<double> _values = [];
    private const int MaxPoints = 60; // 60 seconds of history

    public static readonly DependencyProperty GraphColorProperty =
        DependencyProperty.Register(nameof(GraphColor), typeof(Color), typeof(PerformanceGraph),
            new PropertyMetadata(Color.FromRgb(0, 122, 204), OnGraphColorChanged));

    public static readonly DependencyProperty ValueFormatProperty =
        DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(PerformanceGraph),
            new PropertyMetadata("{0:0}%"));

    public Color GraphColor
    {
        get => (Color)GetValue(GraphColorProperty);
        set => SetValue(GraphColorProperty, value);
    }

    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public PerformanceGraph()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;

        // Initialize with zeros
        for (int i = 0; i < MaxPoints; i++)
        {
            _values.Add(0);
        }
    }

    private static void OnGraphColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceGraph graph)
        {
            var color = (Color)e.NewValue;
            graph.GraphLine.Stroke = new SolidColorBrush(color);
            graph.GraphFill.Fill = new SolidColorBrush(Color.FromArgb(32, color.R, color.G, color.B));
            graph.ValueText.Foreground = new SolidColorBrush(color);
        }
    }

    public void AddValue(double value)
    {
        _values.Add(Math.Clamp(value, 0, 100));
        if (_values.Count > MaxPoints)
        {
            _values.RemoveAt(0);
        }

        ValueText.Text = string.Format(ValueFormat, value);
        UpdateGraph();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGridLines();
        UpdateGraph();
    }

    private void DrawGridLines()
    {
        // Remove old grid lines
        var toRemove = GraphCanvas.Children.OfType<Line>().ToList();
        foreach (var line in toRemove)
        {
            GraphCanvas.Children.Remove(line);
        }

        double width = GraphCanvas.ActualWidth;
        double height = GraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var gridBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));

        // Horizontal lines (25%, 50%, 75%)
        for (int i = 1; i <= 3; i++)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = height * i / 4,
                X2 = width,
                Y2 = height * i / 4,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            Canvas.SetZIndex(line, -1);
            GraphCanvas.Children.Add(line);
        }

        // Vertical lines (every 10 seconds)
        for (int i = 1; i < 6; i++)
        {
            var line = new Line
            {
                X1 = width * i / 6,
                Y1 = 0,
                X2 = width * i / 6,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            Canvas.SetZIndex(line, -1);
            GraphCanvas.Children.Add(line);
        }
    }

    private void UpdateGraph()
    {
        double width = GraphCanvas.ActualWidth;
        double height = GraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0 || _values.Count < 2)
        {
            return;
        }

        var points = new PointCollection();
        var fillPoints = new PointCollection();

        double step = width / (MaxPoints - 1);

        for (int i = 0; i < _values.Count; i++)
        {
            double x = i * step;
            double y = height - (_values[i] / 100.0 * height);
            points.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }

        // Add bottom corners for fill
        fillPoints.Add(new Point((_values.Count - 1) * step, height));
        fillPoints.Add(new Point(0, height));

        GraphLine.Points = points;
        GraphFill.Points = fillPoints;
    }
}
