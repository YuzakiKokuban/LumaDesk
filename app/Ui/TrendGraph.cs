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
            var minimum = double.PositiveInfinity;
            var maximum = double.NegativeInfinity;
            foreach (var item in items)
                if (select(item) is { } value) { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); }
            var current = items.LastOrDefault() is { } latest ? select(latest) : null;
            return double.IsPositiveInfinity(minimum) ? "无有效读数" : $"当前 {Chrome.Number(current)} {unit} · 最低 {minimum:0.#} · 最高 {maximum:0.#}";
        }
        var names = metric == TrendMetric.Temperature ? ("CPU（EC）", "GPU") : ("CPU", "GPU");
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
    private readonly Line[] _gridLines = [new(), new(), new()];
    private readonly TextBlock[] _labels = Enumerable.Range(0, 6).Select(_ => new TextBlock { FontSize = 11 }).ToArray();
    private readonly Microsoft.UI.Xaml.Shapes.Path _first = new() { StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Microsoft.UI.Xaml.Shapes.Path _second = new() { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 3, 2 }, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private TrendGraphElement? _data;
    private bool _inViewport = true;
    public TrendGraphControl()
    {
        Height = 140;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Children.Add(_canvas);
        foreach (var line in _gridLines) _canvas.Children.Add(line);
        foreach (var label in _labels)
        {
            AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
            _canvas.Children.Add(label);
        }
        _canvas.Children.Add(_first);
        _canvas.Children.Add(_second);
        SetBrushes();
        SizeChanged += (_, _) => Draw();
        ActualThemeChanged += (_, _) => { SetBrushes(); Draw(); };
        Loaded += (_, _) => Draw();
        EffectiveViewportChanged += (_, args) =>
        {
            if (XamlRoot is null) return;
            var viewport = args.EffectiveViewport;
            var visible = viewport.Width > 0 && viewport.Height > 0 && viewport.Right > 0 && viewport.Bottom > 0
                && viewport.Left < ActualWidth && viewport.Top < ActualHeight;
            if (visible == _inViewport) return;
            _inViewport = visible;
            if (visible) Draw();
        };
    }
    public void Show(TrendGraphElement data)
    {
        if (_data is { } previous && ReferenceEquals(previous.Samples, data.Samples) && previous.Metric == data.Metric) return;
        _data = data;
        AutomationProperties.SetName(this, "最近五分钟趋势。" + TrendGraph.Summary(data.Samples, data.Metric));
        Draw();
    }
    private Brush Brush(string name)
        => Application.Current.Resources[name] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
    private void SetBrushes()
    {
        foreach (var line in _gridLines) { line.Stroke = Brush("DividerStrokeColorDefaultBrush"); line.StrokeThickness = 1; }
        foreach (var label in _labels) label.Foreground = Brush("TextFillColorSecondaryBrush");
        _first.Stroke = Brush("SystemControlHighlightAccentBrush");
        _second.Stroke = Brush("TextFillColorSecondaryBrush");
    }
    private void Draw()
    {
        if (!_inViewport || _data is not { } data || ActualWidth < 70) return;
        const double left = 48, top = 8, plotHeight = 105;
        var plotWidth = Math.Max(1, ActualWidth - left - 8);
        var (first, second, _) = TrendGraph.Select(data.Metric);
        var largest = 0d;
        foreach (var item in data.Samples) largest = Math.Max(largest, Math.Max(first(item) ?? 0, second(item) ?? 0));
        var maximum = data.Metric switch
        {
            TrendMetric.Temperature => Math.Max(120, Math.Ceiling(largest / 20) * 20),
            TrendMetric.Load => 100,
            _ => Math.Max(1000, Math.Ceiling(largest / 1000) * 1000),
        };
        void Label(int index, string text, double x, double y)
        {
            var label = _labels[index];
            if (label.Text != text) label.Text = text;
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y);
        }
        for (var i = 0; i < 3; i++)
        {
            var fraction = i * 0.5;
            var y = top + plotHeight * (1 - fraction);
            var line = _gridLines[i];
            line.X1 = left; line.X2 = left + plotWidth; line.Y1 = line.Y2 = y;
            Label(i, $"{maximum * fraction:0}", 0, y - 7);
        }
        Label(3, "−5 分钟", left, top + plotHeight + 6);
        Label(4, data.Samples.Count == 0 ? "最近采样" : data.Samples[^1].At.ToLocalTime().ToString("HH:mm:ss"), left + plotWidth - 48, top + plotHeight + 6);
        _labels[5].Visibility = data.Samples.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (data.Samples.Count == 0)
        {
            _first.Data = _second.Data = null;
            Label(5, "等待窗口可见时的有效采样", left + 8, top + 42); return;
        }
        var end = data.Samples[^1].At;
        var start = end - TrendHistory.Window;
        Geometry? Series(Func<TrendSample, double?> select)
        {
            // Parse each series in one native call instead of crossing WinRT once per point.
            var path = new System.Text.StringBuilder(data.Samples.Count * 20);
            var connected = false;
            foreach (var sample in data.Samples)
            {
                if (sample.BreakBefore) connected = false;
                if (select(sample) is not { } value) { connected = false; continue; }
                var x = left + plotWidth * Math.Clamp((sample.At - start).TotalSeconds / TrendHistory.Window.TotalSeconds, 0, 1);
                var y = top + plotHeight * (1 - Math.Clamp(value / maximum, 0, 1));
                path.Append(System.Globalization.CultureInfo.InvariantCulture, $"{(connected ? 'L' : 'M')}{x:0.###},{y:0.###} ");
                // A tiny round-capped segment keeps isolated samples visible.
                if (!connected) path.Append(System.Globalization.CultureInfo.InvariantCulture, $"L{x + 0.01:0.###},{y:0.###} ");
                connected = true;
            }
            return path.Length == 0 ? null : (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), path.ToString());
        }
        _first.Data = Series(first); _second.Data = Series(second);
    }
}
