using JiYaoChu.Interop;
using JiYaoChu.Services;

namespace JiYaoChu.Ui;

/// <summary>
/// Click-handler plumbing: every page needs to fire an async backend call and
/// surface whatever went wrong, without blocking the render pass.
/// </summary>
public static class Act
{
    /// <summary>A callback that reports to the page's error and status slots.</summary>
    public delegate void Report(string? error, string? status);

    /// <summary>
    /// Wraps a backend write into a click handler.
    /// </summary>
    /// <param name="command">The wire command, used in the status line.</param>
    /// <param name="work">The call to make.</param>
    /// <param name="report">Where the outcome is published.</param>
    /// <param name="busy">Called with true before the call and false after.</param>
    public static Action Fire(
        string command,
        Func<Task> work,
        Report report,
        Action<bool>? busy = null)
        => () => _ = RunAsync(command, work, report, busy);

    /// <summary>
    /// Awaits a write, translating both backend refusals and transport faults
    /// into the same one-line message.
    /// </summary>
    public static async Task RunAsync(
        string command,
        Func<Task> work,
        Report report,
        Action<bool>? busy = null)
    {
        busy?.Invoke(true);
        report(null, null);

        try
        {
            await work();
            report(null, $"{command} 已应用");
        }
        catch (CoreException ex)
        {
            report($"{command} 失败：{ex.Message}", null);
        }
        catch (Exception ex)
        {
            report($"{command} 失败：{ex.Message}", null);
        }
        finally
        {
            busy?.Invoke(false);
        }
    }

    /// <summary>Asks the store for a fresh reading after a write lands.</summary>
    public static void Refresh() => MachineStore.Refresh();
}
