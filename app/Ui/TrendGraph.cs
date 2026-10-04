using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Reactor.Core.V1Protocol.Descriptor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using JiYaoChu.Services;
using Windows.Foundation;

namespace JiYaoChu.Ui;

public enum TrendMetric { Temperature, Load, Fan }
public sealed record TrendGraphElement(IReadOnlyList<TrendSample> Samples, TrendMetric Metric) : Element;

/// <summary>Native WinUI graph, registered through the same descriptor protocol as the shell.</summary>
public static class TrendGraph
{
    private static readonly ControlDescriptor<TrendGraphElement, TrendGraphControl> Descriptor =
        new ControlDescriptor<TrendGraphElement, TrendGraphControl> { Factory = static () => new() }
            .OneWay(static element => element, static (control, element) => control.Show(element));
    public static void Configure()
    {
        ControlRegistry.Register<TrendGraphElement, TrendGraphControl>(static () => new DescriptorHandler<TrendGraphElement, TrendGraphControl>(Descriptor));
    }
    public static string Summary(IReadOnlyList<TrendSample> samples, TrendMetric metric)
    {
        var (first, second, unit) = Select(metric);
        static string Describe(IReadOnlyList<TrendSample> items, Func<TrendSample, double?> select, string unit)
        {
            var values = items.Select(select).OfType<double>().ToArray();
            var current = items.LastOrDefault() is { } latest ? select(latest) : null;
            return values.Length == 0 ? "无有效读数" : $"当前 {Chrome.Number(current)} {unit} · 最低 {values.Min():0.#} · 最高 {values.Max():0.#}";
        }
        var names = metric == TrendMetric.Temperature ? ("ACPI 热区", "GPU") : ("CPU", "GPU");
        return $"{names.Item1}（实线）：{Describe(samples, first, unit)}\n{names.Item2}（虚线）：{Describe(samples, second, unit)}";
    }
    internal static (Func<TrendSample, double?> First, Func<TrendSample, double?> Second, string Unit) Select(TrendMetric metric) => metric switch
    {
        TrendMetric.Temperature => (sample => sample.CpuTemp, sample => sample.GpuTemp, "°C"),
        TrendMetric.Load => (sample => sample.CpuLoad, sample => sample.GpuLoad, "%"),
        _ => (sample => sample.CpuRpm, sample => sample.GpuRpm, "RPM"),
    };
}

public sealed class TrendGraphControl : Grid
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private TrendGraphElement? _data;
    public TrendGraphControl()
    {
        Height = 140;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Children.Add(_canvas);
        SizeChanged += (_, _) => Draw();
        ActualThemeChanged += (_, _) => Draw();
        Loaded += (_, _) => Draw();
        Unloaded += (_, _) => _canvas.Children.Clear();
    }
    public void Show(TrendGraphElement data)
    {
        _data = data;
        AutomationProperties.SetName(this, "最近五分钟趋势。" + TrendGraph.Summary(data.Samples, data.Metric));
        Draw();
    }
    private Brush Brush(string name)
        => Application.Current.Resources[name] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
    private void Draw()
    {
        _canvas.Children.Clear();
        if (_data is not { } data || ActualWidth < 70) return;
        const double left = 48, top = 8, plotHeight = 105;
        var plotWidth = Math.Max(1, ActualWidth - left - 8);
        var (first, second, _) = TrendGraph.Select(data.Metric);
        var maximum = data.Metric switch
        {
            TrendMetric.Temperature => Math.Max(120, Math.Ceiling(data.Samples.SelectMany(item => new[] { first(item), second(item) }).OfType<double>().DefaultIfEmpty(0).Max() / 20) * 20),
            TrendMetric.Load => 100,
            _ => Math.Max(1000, Math.Ceiling(data.Samples.SelectMany(item => new[] { first(item), second(item) }).OfType<double>().DefaultIfEmpty(0).Max() / 1000) * 1000),
        };
        void Label(string text, double x, double y)
        {
            var label = new TextBlock { Text = text, FontSize = 11, Foreground = Brush("TextFillColorSecondaryBrush") };
            AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); _canvas.Children.Add(label);
        }
        foreach (var fraction in new double[] { 0, 0.5, 1 })
        {
            var y = top + plotHeight * (1 - fraction);
            _canvas.Children.Add(new Line { X1 = left, X2 = left + plotWidth, Y1 = y, Y2 = y, Stroke = Brush("DividerStrokeColorDefaultBrush"), StrokeThickness = 1 });
            Label($"{maximum * fraction:0}", 0, y - 7);
        }
        Label("−5 分钟", left, top + plotHeight + 6);
        Label(data.Samples.Count == 0 ? "最近采样" : data.Samples[^1].At.ToLocalTime().ToString("HH:mm:ss"), left + plotWidth - 48, top + plotHeight + 6);
        if (data.Samples.Count == 0) { Label("等待窗口可见时的有效采样", left + 8, top + 42); return; }
        var end = data.Samples[^1].At;
        var start = end - TrendHistory.Window;
        void Series(Func<TrendSample, double?> select, bool dashed)
        {
            var points = new PointCollection();
            void Flush()
            {
                if (points.Count > 1)
                {
                    var line = new Polyline { Points = points, Stroke = Brush(dashed ? "TextFillColorSecondaryBrush" : "SystemControlHighlightAccentBrush"), StrokeThickness = 2 };
                    if (dashed) line.StrokeDashArray = new DoubleCollection { 3, 2 };
                    _canvas.Children.Add(line);
                }
                else if (points.Count == 1)
                {
                    var dot = new Ellipse { Width = 4, Height = 4, Fill = Brush(dashed ? "TextFillColorSecondaryBrush" : "SystemControlHighlightAccentBrush") };
                    Canvas.SetLeft(dot, points[0].X - 2); Canvas.SetTop(dot, points[0].Y - 2); _canvas.Children.Add(dot);
                }
                points = new();
            }
            foreach (var sample in data.Samples)
            {
                if (sample.BreakBefore) Flush();
                if (select(sample) is not { } value) { Flush(); continue; }
                points.Add(new Point(left + plotWidth * Math.Clamp((sample.At - start).TotalSeconds / TrendHistory.Window.TotalSeconds, 0, 1), top + plotHeight * (1 - Math.Clamp(value / maximum, 0, 1))));
            }
            Flush();
        }
        Series(first, false); Series(second, true);
    }
}
