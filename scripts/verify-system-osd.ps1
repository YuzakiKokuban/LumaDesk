param([switch]$SkipNativeAudio)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$probeDirectory = Join-Path $repoRoot ('artifacts/system-osd-probe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null
$source = Join-Path $repoRoot 'app/Services/SystemOsdEvents.cs'
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><Compile Include="$source" Link="SystemOsdEvents.cs" /></ItemGroup>
</Project>
"@
[IO.File]::WriteAllText((Join-Path $probeDirectory 'Probe.csproj'), $project)
$program = @'
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiYaoChu.Services;
using JiYaoChu.Interop;

static void Require(bool passed, string reason) { if (!passed) throw new Exception(reason); }
static FieldInfo Field(string name) => typeof(SystemOsdEvents).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
static string Last(string property) => EventBus.Events[^1].Payload![property]!.GetValue<string>();
SystemOsdEvents.Start(0);
Require((nint)Field("_keyboardHook").GetValue(null)! == 0, "Mock initialization attached a keyboard listener");
Require((nint)Field("_powerRegistration").GetValue(null)! == 0, "Mock initialization attached a power listener");
SystemOsdEvents.Stop();
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

    // Keyboard input must report one toggle per press, even with auto-repeat.
    var states = (Dictionary<uint, bool>)Field("LockStates").GetValue(null)!;
    var keyboard = typeof(SystemOsdEvents).GetMethod("OnKeyboard", BindingFlags.NonPublic | BindingFlags.Static)!;
    foreach (var key in new uint[] { 0x14, 0x90, 0x91 })
    {
        states[key] = false;
        Marshal.WriteInt32(block, (int)key);
        before = EventBus.Events.Count;
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
        Require(EventBus.Events.Count == before + 1 && Last("kind") == "lock" && Last("detail") == "已开启", "Lock repeat emitted extra/incorrect transitions");
        keyboard.Invoke(null, new object[] { 0, (nint)0x100, block });
        keyboard.Invoke(null, new object[] { 0, (nint)0x101, block });
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
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, events_checked = EventBus.Events.Count, native_audio_endpoints = endpoints, native_audio_skipped = args.Contains("--skip-native-audio"), hardware_writes = 0 }));

namespace JiYaoChu.Interop
{
    public sealed record BackendEvent(string Name, System.Text.Json.Nodes.JsonNode? Payload);
    public static class Backend { public static string BackendName { get; set; } = "mock"; }
}
namespace JiYaoChu.Services
{
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
dotnet @probeArguments
if ($LASTEXITCODE -ne 0) { throw 'System OSD event checks failed.' }
