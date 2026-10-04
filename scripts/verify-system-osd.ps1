param([switch]$SkipNativeAudio, [switch]$RoundTripMicrophone, [switch]$ReadNativeRadio, [switch]$RoundTripRadio, [int]$WatchFnSeconds = 0, [ValidateRange(0,300)][int]$WatchAudioSeconds = 0)
$ErrorActionPreference = 'Stop'
if ($SkipNativeAudio -and $RoundTripMicrophone) { throw 'Microphone round trip requires native audio.' }
$repoRoot = Split-Path $PSScriptRoot -Parent
$probeDirectory = Join-Path $repoRoot ('artifacts/system-osd-probe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null
$source = Join-Path $repoRoot 'app/Services/SystemOsdEvents.cs'
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><Compile Include="$source" Link="SystemOsdEvents.cs" /></ItemGroup>
  <ItemGroup><Compile Include="$(Join-Path $repoRoot 'app/Services/RadioStateNotifications.cs')" Link="RadioStateNotifications.cs" /></ItemGroup>
  <ItemGroup><Compile Include="$(Join-Path $repoRoot 'app/Services/TouchpadStateNotifications.cs')" Link="TouchpadStateNotifications.cs" /></ItemGroup>
  <ItemGroup><Compile Include="$(Join-Path $repoRoot 'app/Model/Status.cs')" Link="Status.cs" /></ItemGroup>
  <ItemGroup><Compile Include="$(Join-Path $repoRoot 'app/Services/FnKeyInput.cs')" Link="FnKeyInput.cs" /></ItemGroup>
  <ItemGroup><Compile Include="$(Join-Path $repoRoot 'app/Services/AirplaneModeControl.cs')" Link="AirplaneModeControl.cs" /></ItemGroup>
</Project>
"@
[IO.File]::WriteAllText((Join-Path $probeDirectory 'Probe.csproj'), $project)
$program = @'
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiYaoChu.Services;
using JiYaoChu.Interop;
using Microsoft.Win32;

static void Require(bool passed, string reason) { if (!passed) throw new Exception(reason); }
static FieldInfo Field(string name) => typeof(SystemOsdEvents).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
static string Last(string property) => EventBus.Events[^1].Payload![property]!.GetValue<string>();
Require(TouchpadStateNotifications.Decode(0) == false && TouchpadStateNotifications.Decode(1) == true &&
    TouchpadStateNotifications.Decode(-1) == true && TouchpadStateNotifications.Decode(null) is null &&
    TouchpadStateNotifications.Decode("0") is null, "Touchpad setting decoding invented a state");
// Exercise real registry notifications only in a unique test key. Never write
// the user's PrecisionTouchPad setting or cause any physical device change.
var touchpadFixture = @"Software\LumaDeskTest\Touchpad-" + Guid.NewGuid().ToString("N");
var touchpadChanges = new System.Collections.Concurrent.ConcurrentQueue<bool>();
try
{
    using var parent = Registry.CurrentUser.CreateSubKey(touchpadFixture);
    using var status = parent.CreateSubKey("Status");
    status.SetValue("Enabled", 1, RegistryValueKind.DWord);
    using var notifications = new TouchpadStateNotifications(
        () => Registry.CurrentUser.OpenSubKey(touchpadFixture, false), touchpadChanges.Enqueue);
    await notifications.Ready.WaitAsync(TimeSpan.FromSeconds(5));
    Require(touchpadChanges.IsEmpty, "Touchpad baseline displayed a notice");
    foreach (var state in new[] { 0, 1 })
    {
        status.SetValue("Enabled", state, RegistryValueKind.DWord);
        var expected = state == 0 ? 1 : 2;
        var deadline = Environment.TickCount64 + 5000;
        while (touchpadChanges.Count < expected && Environment.TickCount64 < deadline) await Task.Delay(10);
        Require(touchpadChanges.Count == expected, "Touchpad registry change was missed");
    }
    status.SetValue("Enabled", 1, RegistryValueKind.DWord);
    status.SetValue("OtherSetting", 0, RegistryValueKind.DWord);
    await Task.Delay(100);
    Require(touchpadChanges.SequenceEqual(new[] { false, true }), "Duplicate touchpad settings displayed extra notices");
    notifications.Dispose();
    status.SetValue("Enabled", 0, RegistryValueKind.DWord);
    await Task.Delay(100);
    Require(touchpadChanges.Count == 2, "Stopped touchpad listener displayed a notice");
}
finally { Registry.CurrentUser.DeleteSubKeyTree(touchpadFixture, false); }
var toggles = 0;
using (var input = new FnKeyInput(() => { toggles++; return Task.CompletedTask; }))
{
    input.Process(0x73, 0x3e, 0);
    input.Process(0x73, 0x3e, 1);
    Require(toggles == 0, "Ordinary F4 toggled airplane mode");
    input.Process(0xff, 0x78, 2);
    input.Process(0x73, 0x3e, 0);
    input.Process(0x73, 0x3e, 0);
    input.Process(0x73, 0x3e, 1);
    Require(toggles == 1, "Captured Fn+F4 was missed or repeated");
    input.Process(0x73, 0x3e, 0);
    input.Process(0x73, 0x3e, 1);
    Require(toggles == 2, "Second Fn+F4 did not toggle");
    input.Process(0xff, 0x78, 3);
    input.Process(0xff, 0x78, 0);
    input.Process(0x73, 0x3e, 0);
    Require(toggles == 2, "Released Fn or an unrelated marker toggled");
}
var pending = new TaskCompletionSource();
using (var input = new FnKeyInput(() => { toggles++; return pending.Task; }))
{
    input.Process(0xff, 0x78, 2);
    input.Process(0x73, 0x3e, 0);
    input.Process(0x73, 0x3e, 1);
    input.Process(0x73, 0x3e, 0);
    Require(toggles == 3, "Concurrent radio changes were allowed");
    pending.SetResult();
}
SystemOsdEvents.Start(0);
Require((nint)Field("_keyboardHook").GetValue(null)! == 0, "Mock initialization attached a keyboard listener");
Require((nint)Field("_powerRegistration").GetValue(null)! == 0, "Mock initialization attached a power listener");
Require(Field("_touchpad").GetValue(null) is null, "Mock initialization attached a touchpad listener");
SystemOsdEvents.Stop();
SystemOsdEvents.PublishAirplaneState(true);
Require(Last("kind") == "airplane" && Last("detail") == "已开启", "Airplane mode on wasn't displayed");
SystemOsdEvents.PublishAirplaneState(false);
Require(Last("detail") == "已关闭", "Airplane mode off wasn't displayed");
SystemOsdEvents.PublishTouchpadState(false);
Require(Last("kind") == "touchpad" && Last("detail") == "已关闭", "Touchpad disabled state wasn't displayed");
SystemOsdEvents.PublishTouchpadState(true);
Require(Last("detail") == "已开启", "Touchpad enabled state wasn't displayed");
SystemOsdEvents.PublishPowerMode(1);
Require(Last("kind") == "performance" && Last("detail") == "均衡", "Profile readback wasn't displayed");
SystemOsdEvents.PublishDisplayBrightness(0);
Require(Last("kind") == "brightness" && Last("detail") == "0%", "Zero brightness wasn't displayed");
SystemOsdEvents.PublishDisplayBrightness(100);
Require(Last("detail") == "100%", "Full brightness wasn't displayed");
var validBrightnessEvents = EventBus.Events.Count;
SystemOsdEvents.PublishDisplayBrightness(101);
Require(EventBus.Events.Count == validBrightnessEvents, "Invalid brightness was displayed");
EventBus.Events.Clear();
Field("_started").SetValue(null, true);
Backend.BackendName = "windows";
var block = Marshal.AllocHGlobal(32);
try
{
    // Decode the actual POWERBROADCAST_SETTING binary format, ignore its initial
    // baseline, duplicate values, malformed lengths and unrelated GUIDs.
    Marshal.StructureToPtr(new Guid("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548"), block, false);
    Marshal.WriteInt32(block, 16, 4);
    Marshal.WriteInt32(block, 20, 0);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Require(EventBus.Events.Count == 0, "Power registration displayed its baseline or duplicate");
    Marshal.WriteInt32(block, 20, 1);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Require(Last("kind") == "power" && Last("detail") == "使用电池供电", "Battery source wasn't decoded");
    Marshal.WriteInt32(block, 20, 0);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Require(Last("detail") == "已连接电源", "AC source wasn't decoded");
    var before = EventBus.Events.Count;
    Marshal.WriteInt32(block, 20, 3);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Marshal.WriteInt32(block, 16, 3);
    Marshal.WriteInt32(block, 20, 1);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Marshal.StructureToPtr(Guid.Empty, block, false);
    Marshal.WriteInt32(block, 16, 4);
    SystemOsdEvents.HandleWindowMessage(0x218, 0x8013, block);
    Require(EventBus.Events.Count == before, "Malformed/unrelated power messages displayed an OSD");
    Require(PowerPolicy.Sources.SequenceEqual(new uint[] { 0, 1, 0 }), "Power rules missed the initial source or received duplicate/malformed transitions");

    // The authoritative reader deliberately disagrees with the cached state.
    // Keyboard input must report the real value once, even with auto-repeat.
    var states = (Dictionary<uint, bool>)Field("LockStates").GetValue(null)!;
    var actualLockState = true;
    SystemOsdEvents.ReadLockState = _ => actualLockState;
    var keyboard = typeof(SystemOsdEvents).GetMethod("OnKeyboard", BindingFlags.NonPublic | BindingFlags.Static)!;
    foreach (var key in new uint[] { 0x14, 0x90, 0x91 })
    {
        states[key] = true;
        actualLockState = true;
        Marshal.WriteInt32(block, (int)key);
        before = EventBus.Events.Count;
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
        await Task.Delay(100);
        Require(EventBus.Events.Count == before + 1 && Last("kind") == "lock" && Last("detail") == "已开启", "Lock repeat emitted extra/incorrect transitions");
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        actualLockState = false;
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
        await Task.Delay(100);
        Require(Last("detail") == "已关闭", "Second lock press did not switch off");
    }

    // Exercise the production COM callback with AUDIO_VOLUME_NOTIFICATION_DATA,
    // including microphone gain changes that must not look like mute toggles.
    var callbackType = typeof(SystemOsdEvents).GetNestedType("VolumeChanges", BindingFlags.NonPublic)!;
    foreach (var microphone in new bool[] { false, true })
    {
        var callback = Activator.CreateInstance(callbackType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { microphone, false, 0.5f }, null)!;
        var notify = callbackType.GetMethod("OnNotify")!;
        Marshal.WriteInt32(block, 16, 0);
        Marshal.WriteInt32(block, 20, BitConverter.SingleToInt32Bits(0.5f));
        before = EventBus.Events.Count;
        notify.Invoke(callback, new object[] { block });
        Require(EventBus.Events.Count == before, "Duplicate audio state was displayed");
        Marshal.WriteInt32(block, 20, BitConverter.SingleToInt32Bits(0.4f));
        notify.Invoke(callback, new object[] { block });
        Require(EventBus.Events.Count == before + (microphone ? 0 : 1), "Microphone gain/volume filter is incorrect");
        Marshal.WriteInt32(block, 16, 1);
        notify.Invoke(callback, new object[] { block });
        Require(Last("kind") == (microphone ? "microphone" : "volume") && Last("detail") == "已静音", "Audio mute state was not decoded");
    }
}
finally { Marshal.FreeHGlobal(block); SystemOsdEvents.Stop(); }
// Request handling must use authoritative state, suppress companions and
// refuse writes after shutdown or when the OEM still owns the shortcut.
var nativeReadMicrophone = SystemOsdEvents.ReadMicrophoneMute;
var nativeToggleMicrophone = SystemOsdEvents.ToggleMicrophoneMute;
Backend.BackendName = "mock";
SystemOsdEvents.Start(0); // mock: no listeners on the user's hardware
EventBus.Events.Clear();
foreach (var enabled in new[] { true, false })
{
    SystemOsdEvents.ReadFnLockState = () => Task.FromResult(enabled);
    await SystemOsdEvents.RefreshFnLockStateAsync();
    Require(Last("kind") == "fn_lock" && Last("detail") == (enabled ? "已锁定" : "已解锁"), "Fn lock did not use readback");
}
SystemOsdEvents.ReadFnLockState = () => Task.FromException<bool>(new Exception("unsupported EC project"));
await SystemOsdEvents.RefreshFnLockStateAsync();
Require(Last("detail") == "状态未知", "Failed Fn lock read invented a state");
var delayedFn = new TaskCompletionSource<bool>();
SystemOsdEvents.ReadFnLockState = () => delayedFn.Task;
var oldFn = SystemOsdEvents.RefreshFnLockStateAsync();
await Task.Delay(80);
SystemOsdEvents.ReadFnLockState = () => Task.FromResult(false);
await SystemOsdEvents.RefreshFnLockStateAsync();
var afterNewFn = EventBus.Events.Count;
delayedFn.SetResult(true);
await oldFn;
Require(EventBus.Events.Count == afterNewFn && Last("detail") == "已解锁", "Stale Fn read overwrote the latest state");

var microphoneWrites = 0;
var fakeMuted = false;
Backend.Respond = _ => new JiYaoChu.Model.OemStatus { TakenOver = true };
SystemOsdEvents.ReadMicrophoneMute = () => Task.FromResult(fakeMuted);
SystemOsdEvents.ToggleMicrophoneMute = () => { microphoneWrites++; fakeMuted = !fakeMuted; return Task.FromResult(fakeMuted); };
foreach (var expected in new[] { true, false })
{
    Field("_lastMicrophoneRequest").SetValue(null, 0L);
    await SystemOsdEvents.ToggleFnMicrophoneAsync();
    Require(Last("kind") == "microphone" && Last("detail") == (expected ? "已静音" : "已开启"), "Microphone action did not display readback");
    var count = EventBus.Events.Count;
    SystemOsdEvents.PublishAudioState(true, expected, 0.5f);
    await SystemOsdEvents.ToggleFnMicrophoneAsync();
    Require(EventBus.Events.Count == count, "Duplicate endpoint callback or firmware request displayed an extra notice");
}
Require(microphoneWrites == 2 && !fakeMuted, "Microphone companions changed state twice");
Backend.Respond = _ => new JiYaoChu.Model.OemStatus { TakenOver = false };
fakeMuted = true;
Field("_lastMicrophoneRequest").SetValue(null, 0L);
await SystemOsdEvents.ToggleFnMicrophoneAsync();
Require(microphoneWrites == 2 && Last("detail") == "已静音", "OEM-owned microphone was toggled instead of observed");
Backend.Respond = _ => new JiYaoChu.Model.OemStatus { TakenOver = true };
var delayedMicrophone = new TaskCompletionSource<bool>();
SystemOsdEvents.ToggleMicrophoneMute = () => { microphoneWrites++; return delayedMicrophone.Task; };
Field("_lastMicrophoneRequest").SetValue(null, 0L);
var oldMicrophone = SystemOsdEvents.ToggleFnMicrophoneAsync();
await SystemOsdEvents.ToggleFnMicrophoneAsync();
Require(microphoneWrites == 3, "Concurrent microphone toggles were allowed");
var beforeStop = EventBus.Events.Count;
SystemOsdEvents.Stop();
delayedMicrophone.SetResult(false);
await oldMicrophone;
Require(EventBus.Events.Count == beforeStop, "Stopped microphone request displayed an OSD");
SystemOsdEvents.Start(0);
SystemOsdEvents.ToggleMicrophoneMute = () => Task.FromException<bool>(new Exception("no microphone"));
Field("_lastMicrophoneRequest").SetValue(null, 0L);
await SystemOsdEvents.ToggleFnMicrophoneAsync();
Require(Last("detail") == "状态未知", "Microphone failure invented a mute state");
SystemOsdEvents.ReadFnLockState = () => Task.FromException<bool>(new Exception("unsupported EC project"));
SystemOsdEvents.ToggleMicrophoneMute = () => { microphoneWrites++; return Task.FromResult(true); };
Field("_lastMicrophoneRequest").SetValue(null, 0L);
await SystemOsdEvents.ToggleFnMicrophoneAsync();
Require(microphoneWrites == 3 && Last("detail") == "状态未知", "Unknown firmware project was allowed to toggle a microphone");
var pendingValidation = new TaskCompletionSource<bool>();
SystemOsdEvents.ReadFnLockState = () => pendingValidation.Task;
Field("_lastMicrophoneRequest").SetValue(null, 0L);
var stoppedValidation = SystemOsdEvents.ToggleFnMicrophoneAsync();
SystemOsdEvents.Stop();
pendingValidation.SetResult(false);
await stoppedValidation;
Require(microphoneWrites == 3, "Shutdown during validation still wrote the microphone state");
SystemOsdEvents.ReadMicrophoneMute = nativeReadMicrophone;
SystemOsdEvents.ToggleMicrophoneMute = nativeToggleMicrophone;
StartupLog.Errors.Clear(); // The failures above were deliberate regression cases.
bool? microphoneInitial = null;
bool? microphoneChanged = null;
bool? microphoneRestored = null;
if (args.Contains("--round-trip-microphone"))
{
    microphoneInitial = await SystemOsdEvents.ReadMicrophoneMute();
    try
    {
        microphoneChanged = await SystemOsdEvents.ToggleMicrophoneMute();
        Require(microphoneChanged != microphoneInitial, "Native microphone toggle wasn't read back");
    }
    finally
    {
        if (await SystemOsdEvents.ReadMicrophoneMute() != microphoneInitial)
            await SystemOsdEvents.ToggleMicrophoneMute();
        microphoneRestored = await SystemOsdEvents.ReadMicrophoneMute();
    }
    Require(microphoneRestored == microphoneInitial, "Native microphone was not restored");
}
var endpoints = 0;
if (!args.Contains("--skip-native-audio"))
{
    // Register/unregister the real Windows COM callbacks and read the initial
    // defaults. This never changes any volume, mute, keyboard or power setting.
    var nativeType = typeof(SystemOsdEvents).GetNestedType("AudioNotifications", BindingFlags.NonPublic)!;
    using var native = (IDisposable)Activator.CreateInstance(nativeType, true)!;
    Require(StartupLog.Errors.Count == 0, "Native audio subscription failed: " + string.Join("; ", StartupLog.Errors));
    foreach (var fieldName in new[] { "_output", "_microphone" })
        if (nativeType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(native) is not null) endpoints++;
    if (args.FirstOrDefault(value => value.StartsWith("--watch-audio=")) is { } audioWatch)
    {
        var volumeType = typeof(SystemOsdEvents).GetNestedType("IAudioEndpointVolume", BindingFlags.NonPublic)!;
        var endpointType = typeof(SystemOsdEvents).GetNestedType("AudioEndpoint", BindingFlags.NonPublic)!;
        bool? previous = null;
        Console.WriteLine("READY: read-only Windows microphone mute capture");
        var until = Environment.TickCount64 + int.Parse(audioWatch.Split('=')[1]) * 1000L;
        while (Environment.TickCount64 < until)
        {
            var endpoint = nativeType.GetField("_microphone", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(native);
            if (endpoint is not null)
            {
                var volume = endpointType.GetField("_volume", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(endpoint);
                var arguments = new object[] { false };
                var result = (int)volumeType.GetMethod("GetMute")!.Invoke(volume, arguments)!;
                if (result < 0) throw new Exception("Microphone read failed: " + result);
                var muted = (bool)arguments[0];
                if (muted != previous)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { utc = DateTime.UtcNow, microphone_muted = muted }));
                    previous = muted;
                }
            }
            await Task.Delay(100);
        }
    }
}
bool? radioInitial = null;
bool? radioChanged = null;
bool? radioRestored = null;
var nativeFnToggles = 0;
if (args.FirstOrDefault(value => value.StartsWith("--watch-fn=")) is { } watch)
    nativeFnToggles = NativeFnProbe.Run(int.Parse(watch.Split('=')[1]));
if (args.Contains("--read-native-radio") || args.Contains("--round-trip-radio"))
{
    radioInitial = AirplaneModeControl.Read();
    if (args.Contains("--round-trip-radio"))
    {
        try
        {
            radioChanged = AirplaneModeControl.Set(!radioInitial.Value);
            Require(radioChanged == !radioInitial, "Native radio switch wasn't read back");
        }
        finally { radioRestored = AirplaneModeControl.Set(radioInitial.Value); }
        Require(radioRestored == radioInitial, "Native radio state wasn't restored");
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, events_checked = EventBus.Events.Count, touchpad_notifications_checked = touchpadChanges.Count, fn_input_toggles_checked = toggles, native_fn_toggles = nativeFnToggles, native_audio_endpoints = endpoints, native_audio_skipped = args.Contains("--skip-native-audio"), microphone_initial = microphoneInitial, microphone_changed = microphoneChanged, microphone_restored = microphoneRestored, windows_microphone_writes = args.Contains("--round-trip-microphone") ? 2 : 0, radio_initial = radioInitial, radio_changed = radioChanged, radio_restored = radioRestored, hardware_writes = 0 }));

internal static class NativeFnProbe
{
    internal static int Run(int seconds)
    {
        if (seconds is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(seconds));
        var initial = AirplaneModeControl.Read();
        var count = 0;
        var hwnd = CreateWindowEx(0, "STATIC", "LumaDesk Fn verification", 0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (hwnd == 0) throw new Exception("Cannot create native Fn probe window");
        using var input = new FnKeyInput(hwnd, async () =>
        {
            var state = await Task.Run(() => AirplaneModeControl.Set(!AirplaneModeControl.Read()));
            Interlocked.Increment(ref count);
            Console.WriteLine(JsonSerializer.Serialize(new { native_fn_toggle = Volatile.Read(ref count), airplane = state }));
        });
        input.SpecialKeyObserved = (key, scan, flags) => Console.WriteLine(JsonSerializer.Serialize(new { raw_key = key, scan, flags }));
        SubclassProc callback = (window, message, w, l, id, data) =>
        {
            if (message == 0xff) input.Handle(l);
            return DefSubclassProc(window, message, w, l);
        };
        try
        {
            if (!SetWindowSubclass(hwnd, callback, 1, 0)) throw new Exception("Cannot attach native Fn probe");
            Console.WriteLine("Native Fn probe ready; press Fn+F4 twice. Original radio state will be restored.");
            var until = Environment.TickCount64 + seconds * 1000L;
            while (Environment.TickCount64 < until && Volatile.Read(ref count) < 2)
            {
                while (PeekMessage(out var message, 0, 0, 0, 1))
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
                Thread.Sleep(10);
            }
        }
        finally
        {
            input.Dispose();
            RemoveWindowSubclass(hwnd, callback, 1);
            DestroyWindow(hwnd);
            AirplaneModeControl.Set(initial);
            GC.KeepAlive(callback);
        }
        if (count != 2) throw new Exception("Physical Fn+F4 roundtrip was not captured twice");
        return count;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Message { public nint Window; public uint Kind; public nuint W; public nint L; public uint Time; public int X, Y; public uint Private; }
    private delegate nint SubclassProc(nint window, uint message, nuint w, nint l, nuint id, nuint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint extended, string cls, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint flags);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint message, nuint w, nint l);
}

namespace JiYaoChu.Interop
{
    public sealed record BackendEvent(string Name, System.Text.Json.Nodes.JsonNode? Payload);
    public static class Backend
    {
        public static string BackendName { get; set; } = "mock";
        public static Func<string, object> Respond { get; set; } = command => throw new Exception("Unexpected backend call: " + command);
        public static Task<T> CallAsync<T>(string command) => Task.FromResult((T)Respond(command));
    }
}
namespace JiYaoChu.Model
{
    public sealed record OemStatus { public bool TakenOver { get; init; } }
}
namespace JiYaoChu.Services
{
    public static class OsdOverlay { public static void Show(string title, string detail) { } }
    public static class PowerPolicy
    {
        public static readonly List<uint> Sources = [];
        public static void OnPowerSource(uint source) => Sources.Add(source);
    }
    public static class EventBus
    {
        public static readonly List<BackendEvent> Events = [];
        internal static void Raise(BackendEvent raised) => Events.Add(raised);
    }
    public static class StartupLog
    {
        public static readonly List<Exception> Errors = [];
        public static void Write(Exception error) => Errors.Add(error);
    }
}
namespace Microsoft.UI.Reactor
{
    public static class ReactorApp { public static ImmediateDispatcher UIDispatcher { get; } = new(); }
    public sealed class ImmediateDispatcher { public bool TryEnqueue(Action callback) { callback(); return true; } }
}
'@
[IO.File]::WriteAllText((Join-Path $probeDirectory 'Program.cs'), $program)
dotnet build (Join-Path $probeDirectory 'Probe.csproj') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'System OSD probe build failed.' }
$probeArguments = @((Join-Path $probeDirectory 'bin/Release/net10.0-windows/Probe.dll'))
if ($SkipNativeAudio) { $probeArguments += '--skip-native-audio' }
if ($RoundTripMicrophone) { $probeArguments += '--round-trip-microphone' }
if ($ReadNativeRadio) { $probeArguments += '--read-native-radio' }
if ($RoundTripRadio) { $probeArguments += '--round-trip-radio' }
if ($WatchFnSeconds -gt 0) { $probeArguments += "--watch-fn=$WatchFnSeconds" }
if ($WatchAudioSeconds -gt 0) { $probeArguments += "--watch-audio=$WatchAudioSeconds" }
dotnet @probeArguments
if ($LASTEXITCODE -ne 0) { throw 'System OSD event checks failed.' }
