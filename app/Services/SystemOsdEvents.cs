using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.UI.Reactor;
using JiYaoChu.Interop;

namespace JiYaoChu.Services;

/// <summary>
/// Receives keyboard, power and audio notifications. These subscriptions stay
/// idle until Windows delivers an event; they never scan or poll hardware.
/// </summary>
internal static class SystemOsdEvents
{
    private static readonly Guid PowerSource = new("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548");
    private static readonly HookProc KeyboardCallback = OnKeyboard;
    private static readonly Dictionary<uint, bool> LockStates = [];
    private static readonly HashSet<uint> HeldLockKeys = [];
    private static nint _keyboardHook;
    private static nint _powerRegistration;
    private static AudioNotifications? _audio;
    private static uint? _powerSource;
    private static bool _started;

    /// <summary>Call on the UI thread after the backend has initialized.</summary>
    public static void Start(nint hwnd)
    {
        if (_started) return;
        _started = true;
        // Mock validation must never attach listeners to the user's devices.
        if (Backend.BackendName == "mock") return;
        foreach (var key in new uint[] { 0x14, 0x90, 0x91 })
            LockStates[key] = (GetKeyState((int)key) & 1) != 0;
        _keyboardHook = SetWindowsHookEx(13, KeyboardCallback, GetModuleHandle(null), 0);
        if (_keyboardHook == 0) StartupLog.Write(new Win32Exception(Marshal.GetLastWin32Error(), "无法订阅锁定键通知。"));
        var setting = PowerSource;
        _powerRegistration = RegisterPowerSettingNotification(hwnd, ref setting, 0);
        if (_powerRegistration == 0) StartupLog.Write(new Win32Exception(Marshal.GetLastWin32Error(), "无法订阅电源状态通知。"));
        try { _audio = new AudioNotifications(); }
        catch (Exception error) { StartupLog.Write(error); }
    }

    public static void Stop()
    {
        if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
        if (_powerRegistration != 0) UnregisterPowerSettingNotification(_powerRegistration);
        _keyboardHook = 0;
        _powerRegistration = 0;
        try { _audio?.Dispose(); }
        catch (Exception error) { StartupLog.Write(error); }
        _audio = null;
        _powerSource = null;
        LockStates.Clear();
        HeldLockKeys.Clear();
        _started = false;
    }

    /// <summary>Forward from the existing main-window subclass; does not consume the message.</summary>
    public static void HandleWindowMessage(uint message, nint wParam, nint lParam)
    {
        if (!_started || Backend.BackendName == "mock" || message != 0x218 || wParam.ToInt64() != 0x8013 || lParam == 0) return;
        // POWERBROADCAST_SETTING is GUID, DWORD length, then its variable payload.
        var setting = Marshal.PtrToStructure<Guid>(lParam);
        if (setting != PowerSource || Marshal.ReadInt32(lParam, 16) != 4) return;
        var source = unchecked((uint)Marshal.ReadInt32(lParam, 20));
        if (source > 2) return;
        var previous = _powerSource;
        _powerSource = source;
        if (!previous.HasValue || previous.Value != source) PowerPolicy.OnPowerSource(source);
        // RegisterPowerSettingNotification first reports the current value.
        if (previous.HasValue && previous.Value != source) PublishPowerSource(source);
    }

    private static nint OnKeyboard(int code, nint wParam, nint lParam)
    {
        // Let all downstream hooks and Windows receive the input unchanged.
        var result = CallNextHookEx(_keyboardHook, code, wParam, lParam);
        if (code < 0 || result != 0 || !_started) return result;
        var key = unchecked((uint)Marshal.ReadInt32(lParam));
        if (!LockStates.ContainsKey(key)) return result;
        var message = wParam.ToInt64();
        if (message is 0x100 or 0x104)
        {
            // The low-level callback runs before Windows updates key state.
            // Track one accepted transition per press, including SendInput.
            if (HeldLockKeys.Add(key)) LockStates[key] = !LockStates[key];
        }
        else if (message is 0x101 or 0x105 && HeldLockKeys.Remove(key))
        {
            var enabled = LockStates[key];
            // Leave the hook immediately; rendering takes place on the UI queue.
            ReactorApp.UIDispatcher?.TryEnqueue(() => PublishLockState(key, enabled));
        }
        return result;
    }

    /// <summary>Also used by the isolated mock integration checks.</summary>
    internal static void PublishLockState(uint key, bool enabled)
    {
        var title = key switch { 0x14 => "大写锁定", 0x90 => "数字锁定", 0x91 => "滚动锁定", _ => null };
        if (title is not null) Publish("lock", title, enabled ? "已开启" : "已关闭");
    }

    internal static void PublishPowerSource(uint source)
    {
        var detail = source switch { 0 => "已连接电源", 1 => "使用电池供电", 2 => "使用备用电源", _ => null };
        if (detail is not null) Publish("power", "供电状态", detail);
    }

    internal static void PublishAudioState(bool microphone, bool muted, float volume)
    {
        if (microphone) Publish("microphone", "麦克风", muted ? "已静音" : "已开启");
        else Publish("volume", "音量", muted ? "已静音" : $"{Math.Clamp((int)Math.Round(volume * 100), 0, 100)}%");
    }

    internal static void PublishDisplayBrightness(uint percent)
    {
        if (percent <= 100) Publish("brightness", "屏幕亮度", $"{percent}%");
    }

    private static void Publish(string kind, string title, string detail)
        => EventBus.Raise(new BackendEvent("osd://system", new JsonObject { ["kind"] = kind, ["title"] = title, ["detail"] = detail }));

    private sealed class AudioNotifications : IDisposable
    {
        private readonly IMMDeviceEnumerator _enumerator;
        private readonly EndpointChanges _changes;
        private AudioEndpoint? _output;
        private AudioEndpoint? _microphone;
        private bool _disposed;

        public AudioNotifications()
        {
            _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("bcde0395-e52f-467c-8e3d-c4579291692e"), true)!)!;
            _changes = new EndpointChanges(this);
            Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(_changes));
            Attach(0);
            Attach(1);
        }

        private void Attach(int flow)
        {
            if (_disposed) return;
            if (flow == 0) Detach(ref _output);
            else Detach(ref _microphone);
            // Resolve a default endpoint only at subscription or device-change time.
            // Capture uses the default communications microphone.
            if (_enumerator.GetDefaultAudioEndpoint(flow, flow == 0 ? 0 : 2, out var device) < 0) return;
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, 0, out var activated));
                var endpoint = new AudioEndpoint((IAudioEndpointVolume)activated, flow == 1);
                if (flow == 0) _output = endpoint;
                else _microphone = endpoint;
            }
            catch (Exception error) { StartupLog.Write(error); }
            finally { Marshal.ReleaseComObject(device); }
        }

        public void DefaultChanged(int flow, int role)
        {
            if ((flow == 0 && role == 0) || (flow == 1 && role == 2))
                ReactorApp.UIDispatcher?.TryEnqueue(() =>
                {
                    try { Attach(flow); }
                    catch (Exception error) { StartupLog.Write(error); }
                });
        }

        private static void Detach(ref AudioEndpoint? endpoint)
        {
            var previous = endpoint;
            endpoint = null;
            try { previous?.Dispose(); }
            catch (Exception error) { StartupLog.Write(error); }
        }

        public void Dispose()
        {
            _disposed = true;
            // Audio services can disappear during shutdown. An optional OSD
            // subscription must not prevent the application from exiting.
            try { _enumerator.UnregisterEndpointNotificationCallback(_changes); }
            catch (Exception error) { StartupLog.Write(error); }
            Detach(ref _output);
            Detach(ref _microphone);
            Marshal.ReleaseComObject(_enumerator);
        }
    }

    private sealed class AudioEndpoint : IDisposable
    {
        private readonly IAudioEndpointVolume _volume;
        private readonly VolumeChanges _changes;

        public AudioEndpoint(IAudioEndpointVolume volume, bool microphone)
        {
            _volume = volume;
            Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
            Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out var level));
            _changes = new VolumeChanges(microphone, muted, level);
            Marshal.ThrowExceptionForHR(volume.RegisterControlChangeNotify(_changes));
        }

        public void Dispose()
        {
            try { _volume.UnregisterControlChangeNotify(_changes); }
            finally { Marshal.ReleaseComObject(_volume); }
        }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class VolumeChanges(bool microphone, bool initialMuted, float initialLevel) : IAudioEndpointVolumeCallback
    {
        private bool _muted = initialMuted;
        private int _percent = (int)Math.Round(initialLevel * 100);
        public int OnNotify(nint data)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA: GUID context, BOOL mute, float level.
                var muted = Marshal.ReadInt32(data, 16) != 0;
                var level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data, 20));
                var percent = Math.Clamp((int)Math.Round(level * 100), 0, 100);
                var changed = muted != _muted || (!microphone && percent != _percent);
                _muted = muted;
                _percent = percent;
                if (changed) ReactorApp.UIDispatcher?.TryEnqueue(() => PublishAudioState(microphone, muted, level));
            }
            catch (Exception error) { StartupLog.Write(error); }
            return 0;
        }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class EndpointChanges(AudioNotifications owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string id, uint state) => 0;
        public int OnDeviceAdded(string id) => 0;
        public int OnDeviceRemoved(string id) => 0;
        public int OnDefaultDeviceChanged(int flow, int role, string? id)
        {
            try { owner.DefaultChanged(flow, role); }
            catch (Exception error) { StartupLog.Write(error); }
            return 0;
        }
        public int OnPropertyValueChanged(string id, PropertyKey key) => 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Id; }

    [ComImport, Guid("a95664d2-9614-4f35-a746-de8db63617e6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient callback);
    }

    [ComImport, Guid("d666063f-1587-4e43-81f1-b948e807363f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
        [PreserveSig] int OpenPropertyStore(uint access, out nint store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("7991eec9-7e89-4d85-8390-6c703cec60c0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [ComImport, Guid("657804fa-d6ad-4496-8a60-352752af4f89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(nint data);
    }

    [ComImport, Guid("5cdf2c82-841e-4546-9722-0cf74078229a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        [PreserveSig] int GetChannelCount(out uint channels);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint count);
        [PreserveSig] int VolumeStepUp(ref Guid context);
        [PreserveSig] int VolumeStepDown(ref Guid context);
        [PreserveSig] int QueryHardwareSupport(out uint support);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float increment);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProc(int code, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint RegisterPowerSettingNotification(nint window, ref Guid setting, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterPowerSettingNotification(nint registration);
}
