using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Runs only in the isolated mock host; never opens a picker or touches physical hardware.</summary>
internal static class RestoreVerification
{
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    public static async Task RunAsync(Dictionary<string, object> report)
    {
        Require(Backend.BackendName == "mock", "Backup verification requires an isolated mock backend");
        var root = StartupLog.DataDirectory;
        var testRoot = Path.Combine(root, ".backup-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var original = await Backend.CallAsync<AppConfig>("get_app_config");
        var token = "restore_verify_" + Guid.NewGuid().ToString("N");
        var colorName = "colors/" + token + ".json";
        var scriptName = "brfx/" + token + ".brfx";
        var colorPath = SettingsBackup.SafeTarget(root, colorName);
        var scriptPath = SettingsBackup.SafeTarget(root, scriptName);
        var colorBytes = JsonSerializer.SerializeToUtf8Bytes(new ColorPreset { Id = token, Name = "restore test", Values = new Dictionary<string, string> { ["red"] = "100" } }, Core.Json);
        var checks = new List<string>();
        try
        {
            var incoming = original with { AutoPowerMode = true, Osd = original.Osd with { DurationMs = 15000, WatchPhysicalProfile = true } };
            await Backend.CallAsync<AppConfig>("restore_app_settings", new { cfg = incoming });
            Directory.CreateDirectory(Path.GetDirectoryName(colorPath)!); File.WriteAllBytes(colorPath, colorBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            // A highly repetitive, valid UTF-8 script must roundtrip even when >1MiB.
            File.WriteAllText(scriptPath, string.Concat(Enumerable.Repeat("# backup roundtrip comment\n", 90000)), new UTF8Encoding(false));
            var archive = await SupportExport.ExportAsync(true, reveal: false);
            SettingsBackup.ValidateArchive(archive);
            var preview = await SettingsBackup.PreviewAsync(archive);
            Require(preview.FormatVersion == 2 && preview.Files.Contains(colorName) && preview.Files.Contains(scriptName), "Export omitted colors/scripts or manifest");
            checks.Add("v2 export includes colors and large repetitive script, all hashes verify");
            File.WriteAllText(colorPath, "{\"id\":\"changed\",\"name\":\"changed\",\"values\":{\"red\":\"80\"}}");
            await Backend.CallAsync("set_battery_limit", new { limit = original.BatteryLimit == 80 ? 90u : 80u });
            await Backend.CallAsync("set_fan_boost", new { enabled = !original.FanBoost });
            await Backend.CallAsync("set_win_key_locked", new { locked = !original.WinKeyLocked });
            var hardwareBefore = await Backend.CallAsync<AppConfig>("get_app_config");
            await Backend.CallAsync<AppConfig>("restore_app_settings", new { cfg = hardwareBefore with { AutoPowerMode = false, Osd = hardwareBefore.Osd with { DurationMs = 1000, WatchPhysicalProfile = false } } });
            var writes = new List<string>(); JsonNode? effectiveEvent = null;
            using var eventSubscription = new Subscription(EventBus.Subscribe(raised => {
                if (raised.Name != "command://applied") return;
                var command = raised.Payload?["command"]?.GetValue<string>();
                if (command?.StartsWith("set_", StringComparison.Ordinal) == true || command == "apply_keyboard_lighting") lock (writes) writes.Add(command);
                if (command == "restore_app_settings") effectiveEvent = raised.Payload?["arguments"]?["cfg"]?.DeepClone();
            }));
            var result = await SettingsBackup.RestoreAsync(preview, hardware: false);
            Require(result.Success && File.Exists(result.BackupPath), "Restore or pre-restore backup failed: " + string.Join(" | ", result.Details));
            var restored = await Backend.CallAsync<AppConfig>("get_app_config");
            Require(File.ReadAllBytes(colorPath).SequenceEqual(colorBytes), "Color file did not restore");
            Require(restored.AutoPowerMode && restored.Osd.DurationMs == 15000 && restored.Osd.WatchPhysicalProfile, "Application preferences did not restore");
            Require(restored.BatteryLimit == hardwareBefore.BatteryLimit && restored.FanBoost == hardwareBefore.FanBoost && restored.WinKeyLocked == hardwareBefore.WinKeyLocked && restored.GpuMode == hardwareBefore.GpuMode && restored.Autostart == hardwareBefore.Autostart && restored.TakeoverOem == hardwareBefore.TakeoverOem, "Preference restore changed hardware/system values");
            await Task.Delay(200);
            Require(writes.Count == 0, "Preference restore triggered hardware writes: " + string.Join(", ", writes));
            Require(effectiveEvent is not null && effectiveEvent["battery_limit"]?.GetValue<uint>() == hardwareBefore.BatteryLimit, "Restore broadcast did not include effective config");
            checks.Add("preferences restore updates OSD/automation/files, preserves hardware/system state, broadcasts effective cfg, issues no hardware writes");
            var hardwareResult = await SettingsBackup.RestoreAsync(preview, hardware: true);
            Require(hardwareResult.Success, "Mock hardware restore failed: " + string.Join(" | ", hardwareResult.Details));
            var hardwareAfter = await Backend.CallAsync<AppConfig>("get_app_config");
            Require(hardwareAfter.BatteryLimit == incoming.BatteryLimit && hardwareAfter.FanBoost == incoming.FanBoost && hardwareAfter.WinKeyLocked == incoming.WinKeyLocked, "Explicit mock hardware restore did not apply supported values");
            Require(writes.Contains("set_battery_limit") && writes.Contains("set_fan_boost") && writes.Contains("set_win_key_locked"), "Hardware restore skipped required mock commands");
            checks.Add("explicit supported mock hardware restore uses actual commands and parameters");

            var markerBytes = JsonSerializer.SerializeToUtf8Bytes(new ColorPreset { Id = token, Name = "rollback marker", Values = new Dictionary<string, string> { ["red"] = "81" } }, Core.Json);
            File.WriteAllBytes(colorPath, markerBytes);
            var beforeFailure = JsonSerializer.Serialize(await Backend.CallAsync<AppConfig>("get_app_config"), Core.Json);
            var configPath = Path.Combine(root, "config.json");
            var diskBefore = File.ReadAllBytes(configPath);
            BackupRestoreResult failed;
            using (var configLock = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                failed = await SettingsBackup.RestoreAsync(preview, hardware: false);
            Require(!failed.Success && File.Exists(failed.BackupPath), "Persistence failure was not reported or pre-restore backup missing");
            Require(File.ReadAllBytes(colorPath).SequenceEqual(markerBytes), "Persistence failure did not roll back asset writes");
            Require(File.ReadAllBytes(configPath).SequenceEqual(diskBefore) && JsonSerializer.Serialize(await Backend.CallAsync<AppConfig>("get_app_config"), Core.Json) == beforeFailure, "Persistence failure changed memory or disk preferences");
            Require(failed.Details.Any(line => line.Contains("恢复失败", StringComparison.Ordinal)), "Persistence failure lacks detailed outcome");
            checks.Add("locked config causes persistence failure, rolls back assets, preserves memory/disk preferences and saved pre-restore backup");

            var rawConfig = JsonSerializer.SerializeToUtf8Bytes(incoming, Core.Json);
            var legacy = Path.Combine(testRoot, "legacy.zip");
            WriteZip(legacy, [("settings/config.json", rawConfig, 0)]);
            var legacyPreview = await SettingsBackup.PreviewAsync(legacy);
            Require(legacyPreview.FormatVersion == 1 && !legacyPreview.CompatibleHardware, "Legacy metadata handling failed");
            checks.Add("v1 backup without manifest accepted with hardware compatibility disabled");
            var legacyCompressed = Path.Combine(testRoot, "legacy-compressed.zip");
            WriteZip(legacyCompressed, [("settings/config.json", rawConfig, 0), ("settings/brfx/repetitive.brfx", File.ReadAllBytes(scriptPath), 0)], CompressionLevel.Optimal);
            SettingsBackup.ValidateArchive(legacyCompressed);
            checks.Add("legacy compressed repetitive script accepted within streamed size bounds");
            var mirrors = Path.Combine(testRoot, "legacy-osd.zip");
            WriteZip(mirrors, [("settings/config.json", Encoding.UTF8.GetBytes("{\"osd_theme\":\"light\",\"osd_position\":\"top_left\",\"osd_enabled\":false}"), 0)]);
            var mirrorPreview = await SettingsBackup.PreviewAsync(mirrors);
            Require(mirrorPreview.Configuration.Osd.Theme == "light" && mirrorPreview.Configuration.Osd.Position == "top_left" && !mirrorPreview.Configuration.Osd.Enabled, "Legacy OSD mirrors did not migrate");
            checks.Add("legacy OSD mirror-only settings migrate to nested config");
            File.AppendAllText(legacy, "changed");
            var unchanged = JsonSerializer.Serialize(await Backend.CallAsync<AppConfig>("get_app_config"), Core.Json);
            try { await SettingsBackup.RestoreAsync(legacyPreview, hardware: false); throw new InvalidOperationException("Changed archive accepted"); }
            catch (InvalidDataException) { }
            Require(JsonSerializer.Serialize(await Backend.CallAsync<AppConfig>("get_app_config"), Core.Json) == unchanged, "Changed archive wrote preferences");
            checks.Add("changed archive rejected before backup/write");

            var invalidCases = new Dictionary<string, (string, byte[], int)[]> {
                ["traversal"] = [("settings/../config.json", rawConfig, 0)],
                ["duplicate"] = [("settings/config.json", rawConfig, 0), ("settings/config.json", rawConfig, 0)],
                ["link"] = [("settings/config.json", rawConfig, unchecked((int)0xA0000000))],
                ["unknown"] = [("settings/config.json", rawConfig, 0), ("settings/oem-auto-restore.optout", [1], 0)],
                ["unknown-json-field"] = [("settings/config.json", Encoding.UTF8.GetBytes("{\"injected\":true}"), 0)],
                ["duplicate-json-field"] = [("settings/config.json", Encoding.UTF8.GetBytes("{\"power_mode\":1,\"power_mode\":2}"), 0)],
                ["wrong-json-type"] = [("settings/config.json", Encoding.UTF8.GetBytes("{\"battery_limit\":\"100\"}"), 0)],
                ["huge-entry"] = [("settings/config.json", new byte[SettingsBackup.MaxEntryBytes + 1], 0)],
            };
            foreach (var (name, entries) in invalidCases)
            {
                var invalid = Path.Combine(testRoot, name + ".zip"); WriteZip(invalid, entries);
                try { SettingsBackup.ValidateArchive(invalid); throw new InvalidOperationException("Invalid archive accepted: " + name); }
                catch (Exception error) when (error is InvalidDataException or JsonException or InvalidOperationException && !error.Message.StartsWith("Invalid archive accepted", StringComparison.Ordinal)) { }
            }
            checks.Add("traversal, duplicate paths/JSON keys, links, unknown files/fields, wrong JSON types, huge entries rejected");

            var commitRoot = Path.Combine(testRoot, "transaction"); Directory.CreateDirectory(Path.Combine(commitRoot, "colors", "two.json"));
            var one = Path.Combine(commitRoot, "colors", "one.json"); File.WriteAllText(one, "original");
            var originals = new Dictionary<string, byte[]?>(); var written = new List<string>();
            try { SettingsBackup.StageAndCommit(commitRoot, Path.Combine(commitRoot, ".stage"), new() { ["colors/one.json"] = [1], ["colors/two.json"] = [2] }, originals, written); throw new InvalidOperationException("File-commit failure was not surfaced"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            var errors = SettingsBackup.Rollback(commitRoot, originals, written);
            Require(errors.Count == 0 && File.ReadAllText(one) == "original", "File-commit rollback did not preserve original");
            checks.Add("partial file-commit failure rolls back preceding writes");
            report["settings_backup_restore"] = new { passed = true, checks, archive, pre_restore_backup = result.BackupPath, hardware_result = hardwareResult.Details };
        }
        finally
        {
            await Backend.CallAsync<AppConfig>("restore_app_settings", new { cfg = original });
            await Backend.CallAsync("set_battery_limit", new { limit = original.BatteryLimit });
            await Backend.CallAsync("set_fan_boost", new { enabled = original.FanBoost });
            await Backend.CallAsync("set_win_key_locked", new { locked = original.WinKeyLocked });
            File.Delete(colorPath); File.Delete(scriptPath);
            Directory.Delete(testRoot, recursive: true);
        }
    }
    private static void WriteZip(string path, (string Name, byte[] Bytes, int Attributes)[] entries, CompressionLevel compression = CompressionLevel.NoCompression)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes, attributes) in entries)
        {
            var entry = archive.CreateEntry(name, compression); entry.ExternalAttributes = attributes;
            using var output = entry.Open(); output.Write(bytes);
        }
    }
    private sealed class Subscription(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}
