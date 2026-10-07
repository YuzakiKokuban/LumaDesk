using System.Text.Json;
using System.Text.Json.Nodes;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Merge only explicitly queried fields; omitted sections retain their last known values.</summary>
internal static class MachineTelemetry
{
    internal static readonly string[] Pages = ["overview", "tuning", "display", "system", "lighting"];

    public static HardwareStatus Merge(HardwareStatus? previous, JsonNode data, string page)
    {
        if (data["page"]?.GetValue<string>() != page) throw new InvalidDataException("状态读回页面与请求不一致。");
        var fields = data["fields"]?.Deserialize<string[]>(Core.Json) ?? throw new InvalidDataException("状态读回缺少查询范围。");
        var status = previous ?? new();
        T Section<T>(string field) where T : class
            => data[field]?.Deserialize<T>(Core.Json) ?? throw new InvalidDataException($"状态读回缺少 {field}。");
        foreach (var field in fields)
        {
            if (data is not JsonObject body || !body.ContainsKey(field)) throw new InvalidDataException($"状态读回字段 {field} 不存在。");
            status = field switch
            {
                "cpu" => status with { Cpu = Section<CpuStatus>(field) },
                "gpu" => status with { Gpu = Section<GpuStatus>(field) },
                "fans" => status with { Fans = Section<FanStatus>(field) },
                "battery" => status with { Battery = Section<BatteryStatus>(field) },
                "device" => status with { Device = Section<DeviceStatus>(field) },
                "support_flags" => status with { SupportFlags = Section<SupportFlags>(field) },
                "power_mode" => status with { PowerMode = data[field]?.Deserialize<byte?>(Core.Json) },
                "power_mode_error" => status with { PowerModeError = data[field]?.GetValue<string>() },
                "gpu_mode" => status with { GpuMode = data[field]?.Deserialize<GpuMode?>(Core.Json) },
                "fan_boost" => status with { FanBoost = data[field]?.Deserialize<bool?>(Core.Json) },
                "windows_power_scheme" => status with { WindowsPowerScheme = data[field]?.GetValue<string>() ?? "" },
                "elevated" => status with { Elevated = data[field]?.GetValue<bool>() ?? throw new InvalidDataException("权限读回未知。") },
                _ => throw new InvalidDataException($"状态读回包含未知字段 {field}。"),
            };
        }
        return status;
    }
}
