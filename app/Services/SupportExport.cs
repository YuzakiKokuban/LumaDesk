using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using JiYaoChu.Interop;

namespace JiYaoChu.Services;

internal static class SupportExport
{
    public static async Task<string> ExportAsync(bool backup, bool reveal = true)
    {
        var version = await Backend.CallNodeAsync("system.ping");
        var subscriptions = await Backend.CallNodeAsync("system.subscriptions");
        var configuration = backup ? await Backend.CallNodeAsync("get_app_config") : null;
        var directory = Path.Combine(StartupLog.DataDirectory, "exports");
        Directory.CreateDirectory(directory);
        var kind = backup ? "settings" : "diagnostics";
        var path = Path.Combine(directory, $"LumaDesk-{kind}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
                var digests = new Dictionary<string, string>();
                var snapshot = MachineStore.Snapshot;
                var report = new
                {
                    exported_at = DateTimeOffset.Now,
                    version, backend = Backend.BackendName,
                    os = Environment.OSVersion.VersionString,
                    architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    subscriptions,
                    hardware = snapshot.Status is { } status ? status with { Device = status.Device with { Serial = "" } } : null,
                    telemetry_updated_at = snapshot.LastUpdated,
                    telemetry_stale = snapshot.IsStale,
                    error = snapshot.Error, config_error = snapshot.ConfigError,
                    memory_collections = BackgroundMemory.Collections,
                    last_collection_ms = BackgroundMemory.LastCollectionMs,
                };
                Write(archive, "diagnostics.json", JsonSerializer.Serialize(report, Core.Json));
                Write(archive, "validation-notes.txt", "模拟检查不能替代实机操作。仍需确认 MUX 重启后的显示路由、充电截止、背光实际效果、重新登录自启和完整 Fn 按键。\n");
                if (backup)
                {
                    var persisted = SettingsBackup.SafeTarget(StartupLog.DataDirectory, "config.json");
                    CopySetting("settings/config.json", File.Exists(persisted) ? ReadSetting(persisted) : JsonSerializer.SerializeToUtf8Bytes(configuration, Core.Json));
                    var models = SettingsBackup.SafeTarget(StartupLog.DataDirectory, "models.json");
                    if (File.Exists(models)) CopySetting("settings/models.json", ReadSetting(models));
                    foreach (var folder in new[] { "profiles", "colors", "brfx" })
                    {
                        var source = Path.Combine(StartupLog.DataDirectory, folder);
                        if (!Directory.Exists(source)) continue;
                        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) continue;
                        foreach (var file in Directory.EnumerateFiles(source))
                        {
                            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                            var extension = Path.GetExtension(file);
                            if (extension is ".json" or ".brfx")
                                CopySetting($"settings/{folder}/{Path.GetFileName(file)}", ReadSetting(file));
                        }
                    }
                    Write(archive, "manifest.json", JsonSerializer.Serialize(new {
                        format_version = 2, type = "lumadesk-settings", app_version = version?["version"]?.GetValue<string>() ?? "unknown",
                        model = snapshot.Status?.Device.Model ?? "", project = snapshot.Status?.Device.Project ?? "", files = digests,
                    }, Core.Json));
                }
                else
                {
                    foreach (var name in new[] { "boot.log", "boot.log.1", "boot.log.2", "boot.log.3", "boot.log.4", "ui-errors.log", "ui-errors.log.1", "ui-errors.log.2" })
                        CopyIfPresent(archive, name, "logs/" + name);
                }
                void CopySetting(string name, byte[] bytes)
                {
                    if (bytes.LongLength > SettingsBackup.MaxEntryBytes) throw new InvalidDataException("备份文件过大：" + name);
                    using var output = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
                    output.Write(bytes);
                    digests.Add(name, Convert.ToHexString(SHA256.HashData(bytes)));
                }
            });
            if (backup)
            {
                SettingsBackup.ValidateArchive(path);
            }
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        if (reveal)
        {
            var explorer = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            explorer.ArgumentList.Add("/select,");
            explorer.ArgumentList.Add(path);
            System.Diagnostics.Process.Start(explorer);
        }
        return path;
    }
    private static void CopyIfPresent(ZipArchive archive, string relative, string name)
    {
        var path = Path.Combine(StartupLog.DataDirectory, relative);
        if (File.Exists(path))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            Write(archive, name, reader.ReadToEnd());
        }
    }
    private static void Write(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }
    private static byte[] ReadSetting(string path)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length > SettingsBackup.MaxEntryBytes) throw new InvalidDataException("备份文件超过 4 MiB：" + Path.GetFileName(path));
        using var output = new MemoryStream(); source.CopyTo(output);
        return output.ToArray();
    }
}
