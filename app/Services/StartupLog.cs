namespace JiYaoChu.Services;

/// <summary>Retains startup failures even when native XAML terminates the process.</summary>
public static class StartupLog
{
    private static readonly object Gate = new();
    internal static string DataDirectory => Environment.GetEnvironmentVariable("JIYAOCHU_DATA_DIR") is { Length: > 0 } isolated
        ? Path.GetFullPath(isolated) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiYaoChu");
    public static void Write(Exception error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DataDirectory);
                var path = Path.Combine(DataDirectory, "ui-errors.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    for (var i = 2; i >= 1; i--)
                    {
                        var source = i == 1 ? path : path + "." + (i - 1);
                        if (File.Exists(source)) File.Move(source, path + "." + i, overwrite: true);
                    }
                }
                File.AppendAllText(path, $"{DateTimeOffset.Now:O}\n{error}\n\n");
            }
        }
        catch { /* A diagnostic failure must not replace the original exception. */ }
    }
}
