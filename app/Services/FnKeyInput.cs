using System.ComponentModel;
using System.Runtime.InteropServices;

namespace JiYaoChu.Services;

/// <summary>Model-specific raw Fn marker captured on Yaoshi 15 Air.</summary>
internal sealed class FnKeyInput : IDisposable
{
    private readonly Func<Task> _airplane;
    private bool _f4Held;
    private bool _busy;
    private bool _registered;
    public bool FnHeld { get; private set; }
    internal Action<ushort, ushort, ushort>? SpecialKeyObserved { get; set; }

    public FnKeyInput(nint window, Func<Task> airplane)
    {
        _airplane = airplane;
        var device = new RawDevice { Page = 1, Usage = 6, Flags = 0x100, Target = window };
        if (!RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawDevice>()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法订阅 Fn 组合键输入。");
        _registered = true;
    }

    internal FnKeyInput(Func<Task> airplane) { _airplane = airplane; }

    internal void Process(ushort key, ushort scan, ushort flags)
    {
        if (key is 0xff or 0x73) SpecialKeyObserved?.Invoke(key, scan, flags);
        var release = (flags & 1) != 0;
        if (key == 0xff && scan == 0x78 && (flags & 2) != 0)
        {
            FnHeld = !release;
            if (release) _f4Held = false;
        }
        else if (key == 0x73)
        {
            if (release) { _f4Held = false; return; }
            if (!FnHeld || _f4Held) return;
            _f4Held = true;
            if (!_busy) _ = ToggleAsync();
        }
    }

    private async Task ToggleAsync()
    {
        _busy = true;
        try { await _airplane(); }
        catch (Exception error) { StartupLog.Write(error); }
        finally { _busy = false; }
    }

    public void Handle(nint input)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawHeader>();
        if (GetRawInputData(input, 0x10000003, 0, ref size, headerSize) == uint.MaxValue || size < headerSize || size > 4096) return;
        var memory = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(input, 0x10000003, memory, ref size, headerSize) == uint.MaxValue) return;
            var header = Marshal.PtrToStructure<RawHeader>(memory);
            if (header.Kind != 1 || size < headerSize + 16) return;
            var keyboard = memory + (int)headerSize;
            Process(unchecked((ushort)Marshal.ReadInt16(keyboard, 6)),
                unchecked((ushort)Marshal.ReadInt16(keyboard)), unchecked((ushort)Marshal.ReadInt16(keyboard, 2)));
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    public void Dispose()
    {
        if (_registered)
        {
            var device = new RawDevice { Page = 1, Usage = 6, Flags = 1 };
            RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawDevice>());
            _registered = false;
        }
        FnHeld = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawDevice { public ushort Page, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)]
    private struct RawHeader { public uint Kind, Size; public nint Device, Param; }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(ref RawDevice device, uint count, uint size);
    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
}
