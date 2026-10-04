using JiYaoChu.Model;

namespace JiYaoChu.Services;

public static class DisplayPolicy
{
    public static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, string device)
        => displays.FirstOrDefault(display => string.Equals(display.DeviceName, device, StringComparison.OrdinalIgnoreCase));
    public static uint[] Rates(DisplayInfo display) => display.AvailableHz.Where(hz => hz > 0).Distinct().Order().ToArray();
    public static bool CanApply(IReadOnlyList<DisplayInfo> displays, string device, uint hz)
        => Find(displays, device) is { } display && Rates(display).Contains(hz);
    public static uint? Brightness(double value) => double.IsFinite(value) ? (uint)Math.Clamp(Math.Round(value), 0, 100) : null;
}
