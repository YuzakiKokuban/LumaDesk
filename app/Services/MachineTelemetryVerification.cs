using System.Text.Json.Nodes;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

internal static class MachineTelemetryVerification
{
    public static Task RunAsync(Dictionary<string, object> report)
    {
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        var original = new HardwareStatus { Cpu = new() { Temp = 40, Load = 10 }, Gpu = new() { Temp = 50, Present = true }, PowerMode = 2, FanBoost = true };
        var data = JsonNode.Parse("""{"page":"lighting","fields":["power_mode","fan_boost"],"power_mode":null,"fan_boost":null}""")!;
        var light = MachineTelemetry.Merge(original, data, "lighting");
        Require(ReferenceEquals(original.Cpu, light.Cpu) && ReferenceEquals(original.Gpu, light.Gpu) && light.PowerMode is null && light.FanBoost is null,
            "Unqueried telemetry was discarded or unknown controls were converted to known state");
        foreach (var malformed in new[]
        {
            """{"page":"display","fields":[]}""",
            """{"page":"lighting","fields":["cpu"],"cpu":null}""",
            """{"page":"lighting","fields":["unexpected"],"unexpected":0}""",
        })
        {
            try { MachineTelemetry.Merge(original, JsonNode.Parse(malformed)!, "lighting"); throw new InvalidOperationException("Invalid scoped status was accepted"); }
            catch (InvalidDataException) { }
        }
        report["scoped_status_preserves_unqueried_and_unknown_fields"] = true;
        var at = DateTimeOffset.UtcNow;
        var state = new MachineState { Status = light, Page = "lighting", Fields = ["power_mode", "fan_boost"], LastUpdated = at, Loading = false };
        Require(state.ForPage("overview") is { Status: null, LastUpdated: null, Loading: true }, "Unqueried overview sensors were presented as a fresh reading");
        Require(ReferenceEquals(state, state.ForPage("lighting")), "Current scoped state was unnecessarily replaced");
        var failed = state with { Page = "overview", Fields = [], LastUpdated = null, Error = "refused" };
        Require(failed.ForPage("overview") is { Status: null, Loading: false, Error: "refused" }, "Failed first page read displayed default telemetry");
        var good = state with { Page = "overview", Fields = ["cpu"], Error = "refused" };
        Require(good.ForPage("overview") is { Status: not null, IsStale: true }, "Failed same-page read discarded the last good sensors");
        report["scoped_page_freshness_and_first_failure_preserved"] = true;
        return Task.CompletedTask;
    }
}
