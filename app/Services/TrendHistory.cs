using JiYaoChu.Model;

namespace JiYaoChu.Services;

public sealed record TrendSample(DateTimeOffset At, double? CpuTemp, double? CpuLoad, double? GpuTemp, double? GpuLoad, double? CpuRpm, double? GpuRpm, bool BreakBefore);

/// <summary>A five-minute bounded ring fed solely by successful existing machine reads.</summary>
public sealed class TrendHistory
{
    public const int Capacity = 600;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly object _gate = new();
    private readonly TrendSample?[] _ring = new TrendSample[Capacity];
    private int _start, _count;
    private bool _gap = true;

    public void MarkGap() { lock (_gate) _gap = true; }
    public void Clear()
    {
        lock (_gate) { Array.Clear(_ring); _start = _count = 0; _gap = true; }
    }
    public void Record(DateTimeOffset at, HardwareStatus status, bool active, bool visible)
    {
        lock (_gate)
        {
            if (!active || !visible) { _gap = true; return; }
            var previous = _count > 0 ? _ring[(_start + _count - 1) % Capacity] : null;
            if (previous is not null && at <= previous.At) return;
            while (_count > 0 && _ring[_start]!.At < at - Window)
            {
                _ring[_start] = null;
                _start = (_start + 1) % Capacity;
                _count--;
            }
            if (_count == Capacity) { _start = (_start + 1) % Capacity; _count--; }
            _ring[(_start + _count++) % Capacity] = new(at,
                Valid(status.Cpu.Temp), Valid(status.Cpu.Load, 100),
                status.Gpu.Present ? Valid(status.Gpu.Temp) : null,
                status.Gpu.Present ? Valid(status.Gpu.Load, 100) : null,
                status.Fans.Available ? status.Fans.CpuRpm : null,
                status.Fans.Available ? status.Fans.GpuRpm : null,
                _gap || previous is null || at - previous.At > TimeSpan.FromSeconds(3));
            _gap = false;
        }
    }
    public IReadOnlyList<TrendSample> Snapshot()
    {
        lock (_gate)
        {
            var copy = new TrendSample[_count];
            for (var i = 0; i < _count; i++) copy[i] = _ring[(_start + i) % Capacity]!;
            return copy;
        }
    }
    private static double? Valid(double? value, double max = double.MaxValue)
        => value is { } number && double.IsFinite(number) && number >= 0 && number <= max ? number : null;
}
