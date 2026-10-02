namespace JiYaoChu.Services;

/// <summary>Retains startup failures even when native XAML terminates the process.</summary>
public static class StartupLog
{
    public static void Write(Exception error)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiYaoChu");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "ui-errors.log"), $"{DateTimeOffset.Now:O}\n{error}\n\n");
        }
        catch { /* A diagnostic failure must not replace the original exception. */ }
    }
}
