using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

internal sealed record BackupPreview(string ArchivePath, string Digest, int FormatVersion, string AppVersion,
    string Model, string Project, bool CompatibleHardware, AppConfig Configuration,
    IReadOnlyList<string> Files, IReadOnlyList<string> Differences);
internal sealed record BackupRestoreResult(string BackupPath, bool Success, IReadOnlyList<string> Details);

/// <summary>Bounded, validated settings import. No ZIP extraction and no implicit hardware writes.</summary>
internal static class SettingsBackup
{
    internal const long MaxEntryBytes = 4 * 1024 * 1024;
    private const long MaxTotalBytes = 32 * 1024 * 1024;
    private const int MaxEntries = 256;
    private static readonly SemaphoreSlim RestoreGate = new(1, 1);
    private static readonly JsonSerializerOptions StrictJson = new(Core.Json) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, NumberHandling = JsonNumberHandling.Strict };
    private sealed record Contents(Dictionary<string, byte[]> Files, AppConfig Config, int Version, string AppVersion, string Model, string Project);

    public static async Task<BackupPreview> PreviewAsync(string path)
    {
        var full = Path.GetFullPath(path);
        var (contents, digest) = await Task.Run(() => Read(full));
        var status = await Backend.CallAsync<HardwareStatus>("get_hardware_status");
        var current = await Backend.CallAsync<AppConfig>("get_app_config");
        var compatible = Compatible(contents.Model, contents.Project, status.Device);
        var previous = JsonSerializer.SerializeToNode(current, Core.Json)!.AsObject();
        var incoming = JsonSerializer.SerializeToNode(contents.Config, Core.Json)!.AsObject();
        var differences = incoming.Where(pair => !JsonNode.DeepEquals(pair.Value, previous[pair.Key]))
            .Select(pair => $"{DifferenceLabel(pair.Key)}：{PreviewValue(previous[pair.Key])} → {PreviewValue(pair.Value)}").ToList();
        if (contents.Config.Lighting.CustomScriptId != current.Lighting.CustomScriptId)
            differences.Add($"自定义背光脚本选择：{current.Lighting.CustomScriptId ?? "未选择"} → {contents.Config.Lighting.CustomScriptId ?? "未选择"}");
        foreach (var (name, bytes) in contents.Files.Where(pair => pair.Key != "config.json"))
        {
            var target = SafeTarget(StartupLog.DataDirectory, name);
            differences.Add(!compatible && (name == "models.json" || name.StartsWith("profiles/", StringComparison.Ordinal)) ? "机型不匹配，跳过：" + name : !File.Exists(target) ? "新增：" + name : SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(SHA256.HashData(bytes)) ? "相同：" + name : "替换：" + name);
        }
        return new(full, digest, contents.Version, contents.AppVersion, contents.Model, contents.Project, compatible,
            contents.Config, contents.Files.Keys.ToArray(), differences);
    }
    private static string PreviewValue(JsonNode? value)
    {
        var text = value?.ToJsonString() ?? "未设置";
        if (text == "true") return "开启";
        if (text == "false") return "关闭";
        return text.Length > 160 ? text[..160] + "…" : text;
    }
    private static string DifferenceLabel(string key) => key switch {
        "osd" => "屏幕提示", "osd_enabled" => "屏幕提示开关", "log_enabled" => "日志开关", "log_level" => "日志详细程度",
        "auto_power_mode" => "自动性能档位", "power_mode_ac" => "接电时自动档位", "power_mode_battery" => "电池时自动档位",
        "auto_min_refresh_on_battery" => "电池时降低刷新率", "active_profile_id" => "预设选择",
        "power_mode" => "性能档位（默认保留）", "battery_limit" => "充电上限（默认保留）", "battery_mode" => "充电模式（保留）",
        "fan_boost" => "强冷（默认保留）", "lighting" => "键盘背光（默认保留）", "win_key_locked" => "Win 键锁（默认保留）",
        "gpu_mode" => "显卡模式（保留）", "autostart" => "开机自启（保留）", "takeover_oem" => "官方接管（保留）",
        "cooler_strategy" => "散热策略（保留）", "water_cooler_enabled" => "水冷开关（保留）", "cooler_curve_points" => "散热曲线（保留）",
        "cpu_temp_target_min" => "CPU 最低温度目标（保留）", "cpu_temp_target_max" => "CPU 最高温度目标（保留）",
        "cpu_pl4_margin" => "CPU PL4 余量（保留）", "gpu_offset_step" => "GPU 调整步长（保留）", "cpu_safety_guard" => "CPU 安全保护（保留）",
        "high_perf_scheme" => "高性能电源计划（保留）", "display_tuning_enabled" => "显示调节开关（保留）", "refresh_rate" => "刷新率（保留）",
        "usb_charge_enabled" => "USB 供电（保留）", "ac_recovery_enabled" => "接电恢复（保留）", "fn_lock_enabled" => "Fn 键锁（保留）",
        _ => key,
    };

    public static async Task<BackupRestoreResult> RestoreAsync(BackupPreview preview, bool hardware)
    {
        await RestoreGate.WaitAsync();
        try
        {
            // Hold the source closed to writers through hashing and parsing; use only those bytes afterwards.
            var (contents, digest) = await Task.Run(() => Read(preview.ArchivePath));
            if (!string.Equals(digest, preview.Digest, StringComparison.Ordinal)) throw new InvalidDataException("备份已改变，请重新选择并预览。");
            var status = await Backend.CallAsync<HardwareStatus>("get_hardware_status");
            var compatible = Compatible(contents.Model, contents.Project, status.Device);
            if (hardware && (!compatible || (!status.Elevated && Backend.BackendName != "mock"))) throw new InvalidOperationException("机型不匹配或未获得管理员权限，无法恢复硬件设置。");
            var current = await Backend.CallAsync<AppConfig>("get_app_config");
            var backupPath = await SupportExport.ExportAsync(true, reveal: false);
            var details = new List<string> { "恢复前备份：" + backupPath };
            var files = contents.Files.Where(pair => pair.Key != "config.json" && (compatible || !(pair.Key == "models.json" || pair.Key.StartsWith("profiles/", StringComparison.Ordinal)))).ToDictionary();
            if (!compatible) details.Add("机型信息不匹配或缺失，已跳过机型资料与性能预设；仅恢复应用偏好、配色和脚本。");
            var originals = new Dictionary<string, byte[]?>();
            var written = new List<string>();
            var staging = Path.Combine(StartupLog.DataDirectory, ".restore-" + Guid.NewGuid().ToString("N"));
            try
            {
                await Task.Run(() => StageAndCommit(StartupLog.DataDirectory, staging, files, originals, written));
                await Backend.CallAsync<AppConfig>("restore_app_settings", new { cfg = WireConfig(compatible ? contents.Config : contents.Config with { ActiveProfileId = current.ActiveProfileId }) });
                details.Add($"已恢复应用偏好和 {files.Count} 个资料文件。硬件选项、自启、官方接管与显卡切换计划保留当前值。");
            }
            catch (Exception error)
            {
                details.Add("恢复失败：" + error.Message);
                var rollback = Rollback(StartupLog.DataDirectory, originals, written);
                try { await Backend.CallAsync<AppConfig>("restore_app_settings", new { cfg = WireConfig(current) }); }
                catch (Exception rollbackError) { rollback.Add("应用偏好回滚失败：" + rollbackError.Message); }
                details.AddRange(rollback.Count == 0 ? ["已回滚本次文件与偏好修改。"] : rollback);
                return new(backupPath, false, details);
            }
            finally
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                catch (Exception error) { details.Add("临时文件清理失败：" + error.Message); }
            }
            var success = true;
            if (hardware)
            {
                async Task Apply(string label, string command, object args, bool supported)
                {
                    if (!supported) { details.Add(label + "：当前设备未确认支持，已跳过。"); return; }
                    try { await Backend.CallAsync(command, args); details.Add(label + "：已恢复。"); }
                    catch (Exception error) { success = false; details.Add(label + "未恢复：" + error.Message); }
                }
                var cfg = contents.Config;
                await Apply("性能档位", "set_power_mode", new { mode = cfg.PowerMode }, status.PowerMode.HasValue && cfg.PowerMode < 3);
                await Apply("充电上限", "set_battery_limit", new { limit = cfg.BatteryLimit }, status.SupportFlags.BatteryLimit && status.Battery.Limit.HasValue);
                await Apply("强冷", "set_fan_boost", new { enabled = cfg.FanBoost }, status.SupportFlags.FanBoost && status.FanBoost.HasValue);
                try
                {
                    var lighting = await Backend.CallAsync<LightingStatus>("get_lighting_status");
                    // Only the verified static hardware lighting path is eligible. Scripts remain inert files.
                    await Apply("键盘背光", "apply_keyboard_lighting", new { lighting = cfg.Lighting },
                        lighting.Runtime is not null && lighting.RuntimeError is null && cfg.Lighting.KbEngine == LightingEngine.Hardware && cfg.Lighting.KbEffect == 0 && !cfg.Lighting.FirmwareManaged);
                }
                catch (Exception error) { success = false; details.Add("键盘背光支持状态无法确认：" + error.Message); }
                try
                {
                    var switches = await Backend.CallAsync<List<DeviceSwitch>>("get_device_switches");
                    await Apply("Win 键锁", "set_win_key_locked", new { locked = cfg.WinKeyLocked }, switches.Any(item => item.Id == "win_key_lock" && item.Supported));
                }
                catch (Exception error) { success = false; details.Add("Win 键锁支持状态无法确认：" + error.Message); }
                details.Add("硬件恢复结果逐项列出；未操作 MUX、官方服务、登录任务或重启。");
            }
            MachineStore.Refresh();
            return new(backupPath, success, details);
        }
        finally { RestoreGate.Release(); }
    }

    private static bool Compatible(string model, string project, DeviceStatus device) =>
        !string.IsNullOrWhiteSpace(model) && !string.Equals(model, "Unknown", StringComparison.OrdinalIgnoreCase)
        && string.Equals(model, device.Model, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(project) && string.Equals(project, device.Project, StringComparison.OrdinalIgnoreCase);
    private static JsonNode WireConfig(AppConfig config)
    {
        var node = JsonSerializer.SerializeToNode(config, Core.Json)!;
        // Rust's historical engine variant omits the separator used by the C# enum naming policy.
        node["lighting"]!["kb_engine"] = config.Lighting.KbEngine == LightingEngine.Hardware ? "hardware" : "betterrgb";
        return node;
    }

    internal static void ValidateArchive(string path) => _ = Read(path);
    private static (Contents Contents, string Digest) Read(string path)
    {
        _ = SafeTarget(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileName(path));
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("不能读取链接文件。");
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length > MaxTotalBytes) throw new InvalidDataException("备份 ZIP 超过 32 MiB。");
        var digest = Convert.ToHexString(SHA256.HashData(source));
        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("备份文件数量超过 256。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new Dictionary<string, byte[]>();
        JsonObject? manifest = null;
        JsonObject? diagnostics = null;
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateName(entry.FullName);
            if (!seen.Add(entry.FullName)) throw new InvalidDataException("备份包含重复路径。");
            var mode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (mode == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("备份包含链接。");
            if (entry.Length > MaxEntryBytes || (total += entry.Length) > MaxTotalBytes) throw new InvalidDataException("备份解压大小超过限制。");
            using var input = entry.Open();
            using var buffer = new MemoryStream();
            var block = new byte[8192]; int count;
            while ((count = input.Read(block)) != 0)
            {
                if (buffer.Length + count > MaxEntryBytes) throw new InvalidDataException("备份文件解压大小异常。");
                buffer.Write(block, 0, count);
            }
            var bytes = buffer.ToArray();
            if (bytes.LongLength != entry.Length) throw new InvalidDataException("备份文件大小不一致。");
            if (entry.FullName == "manifest.json") manifest = ParseJson(bytes).AsObject();
            else if (entry.FullName == "diagnostics.json") diagnostics = ParseJson(bytes).AsObject();
            else if (entry.FullName.StartsWith("settings/", StringComparison.Ordinal))
            {
                var name = entry.FullName[9..];
                if (!Recognized(name)) throw new InvalidDataException("不支持的设置文件：" + name);
                ValidatePayload(name, bytes);
                files.Add(name, bytes);
            }
            else if (entry.FullName != "validation-notes.txt") throw new InvalidDataException("备份包含未识别文件：" + entry.FullName);
        }
        if (!files.TryGetValue("config.json", out var config)) throw new InvalidDataException("备份缺少 settings/config.json。");
        var version = manifest is null ? 1 : manifest["format_version"]?.GetValue<int>() ?? 0;
        if (version != 1 && version != 2) throw new InvalidDataException("备份格式版本不受支持。");
        if (manifest is not null)
        {
            if (manifest["type"]?.GetValue<string>() != "lumadesk-settings" || version != 2) throw new InvalidDataException("该 ZIP 不是设置备份。");
            var hashes = manifest["files"]?.AsObject() ?? throw new InvalidDataException("备份缺少文件校验表。");
            if (hashes.Count != files.Count) throw new InvalidDataException("备份文件清单不匹配。");
            foreach (var (name, bytes) in files)
                if (!string.Equals(hashes["settings/" + name]?.GetValue<string>(), Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份校验失败：" + name);
        }
        return (new(files, ReadConfiguration(ParseJson(config).AsObject()), version,
            manifest?["app_version"]?.GetValue<string>() ?? diagnostics?["version"]?["version"]?.GetValue<string>() ?? "未知",
            manifest?["model"]?.GetValue<string>() ?? diagnostics?["hardware"]?["device"]?["model"]?.GetValue<string>() ?? "",
            manifest?["project"]?.GetValue<string>() ?? diagnostics?["hardware"]?["device"]?["project"]?.GetValue<string>() ?? ""), digest);
    }

    internal static bool Recognized(string name) => name is "config.json" or "models.json"
        || (name.Split('/') is [var folder, var file] && ((folder is "profiles" or "colors" && file.EndsWith(".json", StringComparison.Ordinal)) || folder == "brfx" && file.EndsWith(".brfx", StringComparison.Ordinal)));
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.Contains('\\') || Path.IsPathRooted(name)) throw new InvalidDataException("备份路径无效。");
        foreach (var part in name.Split('/'))
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(part.Split('.')[0], StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("备份包含不安全路径。");
    }
    internal static string SafeTarget(string root, string name)
    {
        ValidateName(name);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(fullRoot, name.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("文件超出配置目录。");
        for (string? part = target; part is not null; part = Path.GetDirectoryName(part))
        {
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("配置路径包含链接。");
            if (string.Equals(part, Path.GetPathRoot(part), StringComparison.OrdinalIgnoreCase)) break;
        }
        return target;
    }
    private static JsonNode ParseJson(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        void Check(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw new InvalidDataException("JSON 包含重复字段。"); Check(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Check(item);
        }
        Check(doc.RootElement);
        return JsonNode.Parse(bytes)!;
    }
    private static void ValidatePayload(string name, byte[] bytes)
    {
        if (name.EndsWith(".brfx", StringComparison.Ordinal))
        {
            var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0')) throw new InvalidDataException("脚本内容无效。");
            return;
        }
        var node = ParseJson(bytes);
        if (name == "config.json")
        {
            var obj = node.AsObject();
            if (obj.Count == 0) throw new InvalidDataException("应用配置为空。");
            var cfg = ReadConfiguration(obj);
            if (cfg.Osd is null || cfg.Lighting is null || cfg.CoolerCurvePoints is null || cfg.Lighting.FourZoneColors is null || cfg.CoolerCurvePoints.Any(point => point is null)) throw new InvalidDataException("应用配置包含空对象或空数组。");
            if (cfg.PowerMode > 3 || cfg.PowerModeAc > 3 || cfg.PowerModeBattery > 3 || cfg.BatteryLimit is < 50 or > 100 || cfg.Osd.DurationMs is < 600 or > 15000 || cfg.Osd.Opacity is < 20 or > 100 || cfg.Lighting.KbBrightness > 4 || !new uint[] { 15, 30, 45, 60 }.Contains(cfg.Lighting.KbFps) || cfg.Lighting.FourZoneColors.Count != 4 || cfg.CpuTempTargetMin > cfg.CpuTempTargetMax || cfg.CpuTempTargetMin < 40 || cfg.CpuTempTargetMax > 100) throw new InvalidDataException("应用配置数值超出范围。");
            if (!new[] { "dark", "light" }.Contains(cfg.Osd.Theme) || !SystemOptions.OsdPositions.Any(item => item.Wire == cfg.Osd.Position) || !new[] { "off", "error", "warn", "info", "debug", "trace" }.Contains(cfg.LogLevel)) throw new InvalidDataException("应用配置选项无效。");
            foreach (var color in new[] { cfg.Lighting.KbColor, cfg.Lighting.LogoColor, cfg.Lighting.LightbarColor, cfg.Lighting.HingeColor }.Concat(cfg.Lighting.FourZoneColors))
                if (color is null || !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("背光颜色无效。");
            if (!Enum.IsDefined(cfg.GpuMode) || !Enum.IsDefined(cfg.BatteryMode) || !Enum.IsDefined(cfg.Lighting.KbEngine) || !new[] { "smart", "silent", "balanced", "extreme" }.Contains(cfg.CoolerStrategy) || cfg.RefreshRate > 1000 || cfg.CpuPl4Margin is < -50 or > 100 || cfg.GpuOffsetStep is < -500 or > 500 || cfg.CoolerCurvePoints.Any(point => point.Temp is < 0 or > 120 || point.Duty is < 0 or > 100 || point.Volt is not (0 or 7 or 8 or 11))) throw new InvalidDataException("应用配置参数无效。");
            if (cfg.Lighting.CustomScriptId is { Length: > 0 } id) ValidateName(id);
        }
        else if (name.StartsWith("profiles/", StringComparison.Ordinal))
        {
            var profile = node.Deserialize<ProfilePreset>(StrictJson)!;
            if (profile is null || profile.FanCurve is null || profile.FanCurve.Any(point => point is null)) throw new InvalidDataException("性能预设包含空对象或空数组。");
            if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name) || profile.PowerMode > 3 || profile.CpuPl1 is < 15 or > 220 || profile.CpuPl2 is < 15 or > 250 || profile.GpuTgp is < 15 or > 250 || profile.GpuDb is < 0 or > 80 || profile.FanCurve.Any(point => point.Temp is < 0 or > 120 || point.Duty is < 0 or > 100 || point.Volt is not (0 or 7 or 8 or 11))) throw new InvalidDataException("性能预设数值无效。");
        }
        else if (name.StartsWith("colors/", StringComparison.Ordinal))
        {
            var color = node.Deserialize<ColorPreset>(StrictJson)!;
            if (color is null || color.Values is null || color.Values.Any(pair => pair.Value is null)) throw new InvalidDataException("配色预设包含空对象。");
            if (string.IsNullOrWhiteSpace(color.Id) || string.IsNullOrWhiteSpace(color.Name) || color.Values.Count == 0) throw new InvalidDataException("配色预设无效。");
        }
        else if (name == "models.json")
        {
            var models = node.AsArray();
            if (models.Count is < 1 or > 128) throw new InvalidDataException("机型资料数量无效。");
            foreach (var model in models)
            {
                var obj = model!.AsObject();
                if (string.IsNullOrWhiteSpace(obj["model_id"]?.GetValue<string>()) || string.IsNullOrWhiteSpace(obj["display_name"]?.GetValue<string>())) throw new InvalidDataException("机型资料缺少标识。");
                if (obj["mine"] is { } mine) _ = mine.GetValue<bool>();
                if (obj["alias"] is { } alias) _ = alias.GetValue<string>();
                foreach (var (field, minimum, maximum) in new[] { ("base_tgp", 15d, 250d), ("max_db", 0d, 80d) })
                    if (obj[field] is { } value) { var number = value.GetValue<double>(); if (!double.IsFinite(number) || number < minimum || number > maximum) throw new InvalidDataException("机型参数数值无效。"); }
                foreach (var key in new[] { "factory_specs", "presets" })
                    foreach (var mode in new[] { "office", "balanced", "turbo" })
                    {
                        var tuning = obj[key]?[mode]?.AsObject() ?? throw new InvalidDataException("机型资料缺少档位。");
                        foreach (var (field, minimum, maximum) in new[] { ("pl1", 15d, 220d), ("pl2", 15d, 250d), ("pl4", 60d, 250d), ("temp_offset", 0d, 30d) })
                        { var number = tuning[field]?.GetValue<double>() ?? double.NaN; if (!double.IsFinite(number) || number < minimum || number > maximum) throw new InvalidDataException("机型调校数值无效。"); }
                    }
            }
        }
    }
    private static AppConfig ReadConfiguration(JsonObject obj)
    {
        // Older backups may contain only the legacy OSD mirrors. Prefer an explicit nested value.
        var typed = obj.DeepClone().AsObject();
        var osd = typed["osd"] as JsonObject;
        if (typed.ContainsKey("osd") && osd is null) throw new InvalidDataException("OSD 配置必须是对象。");
        osd ??= new JsonObject();
        foreach (var (legacy, nested) in new[] { ("osd_theme", "theme"), ("osd_position", "position"), ("osd_enabled", "enabled") })
            if (typed[legacy] is { } value)
            {
                if (nested == "enabled") _ = value.GetValue<bool>(); else _ = value.GetValue<string>();
                if (!osd.ContainsKey(nested)) osd[nested] = value.DeepClone();
                if (legacy != "osd_enabled") typed.Remove(legacy);
            }
        if (typed["osd"] is null) typed["osd"] = osd;
        return typed.Deserialize<AppConfig>(StrictJson) ?? throw new InvalidDataException("配置无效。");
    }
    internal static void StageAndCommit(string root, string staging, Dictionary<string, byte[]> files, Dictionary<string, byte[]?> originals, List<string> written)
    {
        SafeTarget(root, Path.GetFileName(staging));
        Directory.CreateDirectory(staging);
        foreach (var (name, bytes) in files)
        {
            var target = SafeTarget(root, name);
            originals[name] = File.Exists(target) ? File.ReadAllBytes(target) : null;
            var stage = SafeTarget(staging, name);
            Directory.CreateDirectory(Path.GetDirectoryName(stage)!);
            File.WriteAllBytes(stage, bytes);
        }
        foreach (var name in files.Keys)
        {
            var target = SafeTarget(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(SafeTarget(staging, name), target, overwrite: true);
            written.Add(name);
        }
    }
    internal static List<string> Rollback(string root, Dictionary<string, byte[]?> originals, List<string> written)
    {
        var failures = new List<string>();
        foreach (var name in written.AsEnumerable().Reverse())
            try { var path = SafeTarget(root, name); if (originals[name] is { } bytes) { var temp = path + ".rollback-" + Guid.NewGuid().ToString("N"); File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); } else File.Delete(path); }
            catch (Exception error) { failures.Add(name + " 回滚失败：" + error.Message); }
        return failures;
    }
}
