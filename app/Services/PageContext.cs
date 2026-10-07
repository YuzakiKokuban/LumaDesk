using JiYaoChu.Model;

namespace JiYaoChu.Services;

public sealed record DisplayPageDraft(string DeviceName = "", uint? RefreshRate = null);

/// <summary>Small, process-local page drafts. Never retains controls, readers or hardware state.</summary>
public static class PageContext
{
    public const string ScrollAutomationPrefix = "page-context:";
    private static readonly object Gate = new();
    private static readonly Dictionary<string, double> ScrollOffsets = new(StringComparer.Ordinal);
    private static DisplayPageDraft _display = new();
    private static string _keyboardColor = "";
    private static GpuMode? _gpu;

    public static DisplayPageDraft DisplayDraft { get { lock (Gate) return _display; } set { lock (Gate) _display = value; } }
    public static string KeyboardColorInput { get { lock (Gate) return _keyboardColor; } set { lock (Gate) _keyboardColor = value; } }
    public static GpuMode? GpuDraft { get { lock (Gate) return _gpu; } set { lock (Gate) _gpu = value; } }

    public static DisplayPageDraft ReconcileDisplay(DisplayPageDraft draft, IReadOnlyList<DisplayInfo> displays)
    {
        var device = draft.DeviceName.Length == 0 ? displays.FirstOrDefault()?.DeviceName ?? "" : draft.DeviceName;
        var actual = DisplayPolicy.Find(displays, device);
        var rate = draft.RefreshRate;
        if (actual is null || rate == actual.CurrentHz || rate is { } hz && !DisplayPolicy.CanApply(displays, device, hz)) rate = null;
        return new(device, rate);
    }

    public static GpuMode? ReconcileGpu(GpuMode? draft, GpuModeInfo info)
        => !info.Supported || draft == info.ConfiguredMode || draft == GpuMode.Igpu && !info.SupportsIgpu ? null : draft;

    // A completion from an old page must not clear a newer page's draft.
    public static void ClearDisplayRate(string device, uint rate)
    {
        lock (Gate)
            if (string.Equals(_display.DeviceName, device, StringComparison.OrdinalIgnoreCase) && _display.RefreshRate == rate)
                _display = _display with { RefreshRate = null };
    }

    public static void ClearGpuDraft(GpuMode? mode)
    {
        lock (Gate) if (_gpu == mode) _gpu = null;
    }

    public static double ScrollOffset(string key) { lock (Gate) return ScrollOffsets.GetValueOrDefault(key); }
    public static void RememberScroll(string key, double offset)
    {
        if (!double.IsFinite(offset) || offset < 0) return;
        lock (Gate) ScrollOffsets[key] = offset;
    }
}

/// <summary>One restore per mount, deferred until the page has a usable layout.</summary>
internal sealed class PageScrollRestore(double offset)
{
    public bool Pending { get; private set; } = double.IsFinite(offset) && offset > 0;
    public void Cancel() => Pending = false;
    public bool TryRestore(double extent, double viewport, bool contentReady, out double target)
    {
        target = 0;
        if (!Pending || !contentReady || !double.IsFinite(extent) || extent < 0 || !double.IsFinite(viewport) || viewport <= 0) return false;
        target = Math.Min(offset, extent);
        return true;
    }

    public bool ConfirmRestore(bool accepted, double actualOffset, double target)
    {
        if (!Pending || !double.IsFinite(target) || target < 0) return false;
        if (!accepted && (!double.IsFinite(actualOffset) || Math.Abs(actualOffset - target) >= 1)) return false;
        Pending = false;
        return true;
    }
}
