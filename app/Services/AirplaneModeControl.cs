using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>Windows RmSvc global radio switch; independent of any single Wi-Fi radio.</summary>
internal static class AirplaneModeControl
{
    // This desktop COM interface is not a public WinRT contract. Its layout is
    // recorded in reverse/native/RADIO.md; verify support and every readback.
    private static IRadioManager Open()
        => (IRadioManager)Activator.CreateInstance(Type.GetTypeFromCLSID(
            new Guid("581333f6-28db-41be-bc7a-ff201f12f3f6"), true)!)!;

    private static bool Read(IRadioManager manager)
    {
        Marshal.ThrowExceptionForHR(manager.IsRMSupported(out var supported));
        if (supported == 0) throw new NotSupportedException("Windows 不支持系统无线开关。");
        Marshal.ThrowExceptionForHR(manager.GetSystemRadioState(out var radioEnabled, out _, out _));
        return radioEnabled switch { 0 => true, 1 => false, _ => throw new InvalidOperationException("系统无线状态无法识别。") };
    }

    internal static bool Read()
    {
        var manager = Open();
        try { return Read(manager); }
        finally { Marshal.ReleaseComObject(manager); }
    }

    internal static bool Set(bool airplane)
    {
        var manager = Open();
        try
        {
            if (Read(manager) == airplane) return airplane;
            Marshal.ThrowExceptionForHR(manager.SetSystemRadioState(airplane ? 0 : 1));
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (Read(manager) == airplane) return airplane;
                Thread.Sleep(100);
            }
            throw new InvalidOperationException("飞行模式切换后未能确认系统状态。");
        }
        finally { Marshal.ReleaseComObject(manager); }
    }

    internal static async Task ToggleAsync()
    {
        try
        {
            var enabled = await Task.Run(() => Set(!Read()));
            SystemOsdEvents.PublishAirplaneState(enabled);
        }
        catch (Exception error)
        {
            StartupLog.Write(error);
            Microsoft.UI.Reactor.ReactorApp.UIDispatcher?.TryEnqueue(
                () => OsdOverlay.Show("飞行模式切换失败", "请在 Windows 设置中检查无线开关"));
        }
    }

    [ComImport, Guid("db3afbfb-08e6-46c6-aa70-bf9a34c30ab7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRadioManager
    {
        [PreserveSig] int IsRMSupported(out uint supported);
        [PreserveSig] int GetUIRadioInstances(out nint instances);
        [PreserveSig] int GetSystemRadioState(out int radioEnabled, out int hardware, out uint reason);
        [PreserveSig] int SetSystemRadioState(int radioEnabled);
        [PreserveSig] int Refresh();
        [PreserveSig] int OnHardwareSliderChange(int first, int second);
    }
}
