using Microsoft.UI.Reactor;
using System.Text.Json;
using JiYaoChu.Interop;
using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>
/// The live view of the machine, as the backend last reported it.
/// </summary>
/// <remarks>
/// A record, so that two polls that read the same values compare equal and
/// Reactor can skip the re-render.
/// </remarks>
public sealed record MachineState
{
    /// <summary>The last good snapshot, or null before the first one arrives.</summary>
    public HardwareStatus? Status { get; init; }

    /// <summary>Why the last poll failed, or null when it succeeded.</summary>
    public string? Error { get; init; }

    /// <summary>True until the first poll settles, one way or the other.</summary>
    public bool Loading { get; init; } = true;

    /// <summary>The HAL the backend chose: a Windows backend name, or "mock".</summary>
    public string Backend { get; init; } = "unknown";

    /// <summary>Why the stored configuration could not be read, if it could not.</summary>
    public string? ConfigError { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public IReadOnlyList<TrendSample> Trends { get; init; } = [];
    public string Page { get; init; } = "overview";
    public IReadOnlyList<string> Fields { get; init; } = [];
    public bool IsStale => Error is not null || LastUpdated is { } at && DateTimeOffset.UtcNow - at > TimeSpan.FromSeconds(5);

    /// <summary>True while a fresh snapshot is being read and none has ever arrived.</summary>
    public bool HasData => Status is not null;

    /// <summary>A different page's last read cannot make this page's sensors appear fresh.</summary>
    public MachineState ForPage(string page) => Page == page && Fields.Count > 0 ? this : this with
    {
        Status = null,
        LastUpdated = null,
        Loading = Page != page || Loading,
        Error = Page == page ? Error : null,
    };
}

/// <summary>
/// Polls the backend once a second and publishes the result to the UI.
/// </summary>
/// <remarks>
/// A single long-lived loop rather than a timer per page: the readings are
/// machine-wide, several pages want them, and the backend must never be entered
/// concurrently from two polls. Feed it to Reactor with
/// <c>UseExternalStore(MachineStore.Subscribe, () =&gt; MachineStore.Snapshot)</c>.
/// </remarks>
public static class MachineStore
{
    private static readonly object Gate = new();
    private static readonly List<Action> Listeners = [];
    private static readonly CancellationTokenSource Stopping = new();

    /// <summary>Lets a writer cut the poll's sleep short so the UI updates at once.</summary>
    private static readonly SemaphoreSlim Nudge = new(0, 1);

    private static MachineState _snapshot = new();
    private static int _started;
    private static int _active;
    private static bool _trendActive;
    private static string _page = "overview";
    private static long _pageRevision;
    private static long _hardwareReads;
    private static readonly TrendHistory History = new();
    internal static long HardwareReads => Interlocked.Read(ref _hardwareReads);
    public static bool IsActive => Volatile.Read(ref _active) != 0;

    public static void SetActive(bool active)
    {
        if (!active) History.MarkGap();
        if (Interlocked.Exchange(ref _active, active ? 1 : 0) != (active ? 1 : 0)) Refresh();
    }

    /// <summary>Trend recording belongs to the visible overview route, independently of other status consumers.</summary>
    public static void SetTrendActive(bool active)
    {
        lock (Gate)
        {
            if (_trendActive == active) return;
            _trendActive = active;
            if (!active) History.MarkGap();
        }
        if (active && IsActive) Refresh();
    }

    public static void SetPage(string page)
    {
        if (!MachineTelemetry.Pages.Contains(page)) throw new ArgumentOutOfRangeException(nameof(page));
        lock (Gate)
        {
            if (_page == page) return;
            _page = page;
            _pageRevision++;
            if (page != "overview") { _trendActive = false; History.MarkGap(); }
        }
        if (IsActive) Refresh();
    }

    /// <summary>The current reading. Safe to call from any thread.</summary>
    public static MachineState Snapshot
    {
        get
        {
            lock (Gate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>Registers a listener and returns the function that removes it.</summary>
    /// <remarks>
    /// The listener is not invoked here: Reactor calls the snapshot getter itself
    /// as part of subscribing, and notifying during render would be an update
    /// from inside the render pass.
    /// </remarks>
    public static Action Subscribe(Action listener)
    {
        lock (Gate)
        {
            Listeners.Add(listener);
        }

        return () =>
        {
            lock (Gate)
            {
                Listeners.Remove(listener);
            }
        };
    }

    /// <summary>Starts the poll loop. Calling it more than once is harmless.</summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _ = Task.Run(PollAsync);
    }

    /// <summary>
    /// Asks the poll loop to read again immediately instead of at the next tick.
    /// </summary>
    /// <remarks>
    /// Called after a write lands, so the page does not show a stale reading for
    /// up to a second afterwards. The extra read is harmless: only one poll runs
    /// at a time, and a duplicate nudge simply collapses into the next tick.
    /// </remarks>
    public static void Refresh()
    {
        if (_started == 0 || Nudge.CurrentCount > 0)
        {
            return;
        }

        try
        {
            Nudge.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another caller got there first; the poll is already awake.
        }
    }

    /// <summary>Sleeps for the poll cadence, unless a writer nudges us first.</summary>
    private static async Task WaitForTickAsync(int milliseconds)
    {
        await Nudge.WaitAsync(milliseconds, Stopping.Token).ConfigureAwait(false);
    }

    private static async Task PollAsync()
    {
        try
        {
            while (!Stopping.IsCancellationRequested)
            {
                if (!IsActive)
                {
                    await Nudge.WaitAsync(Stopping.Token).ConfigureAwait(false);
                    continue;
                }
                var started = Environment.TickCount64;
                string page;
                long revision;
                lock (Gate) { page = _page; revision = _pageRevision; }
                try
                {
                    var bootstrap = await Backend.InitializeAsync().ConfigureAwait(false);
                    if (!IsActive) continue;
                    Interlocked.Increment(ref _hardwareReads);
                    var data = await Backend.CallNodeAsync("get_page_status", new { page })
                        .ConfigureAwait(false);
                    if (!IsActive) continue;
                    var events = await Backend.DrainEventsAsync().ConfigureAwait(false);

                    var updated = DateTimeOffset.UtcNow;
                    Publish(state =>
                    {
                        if (revision != _pageRevision || !IsActive) return state;
                        var status = MachineTelemetry.Merge(state.Status, data, page);
                        // Publish holds Gate, so a route change cannot race an in-flight poll
                        // into recording or copying a trend snapshot after the tab is left.
                        var trends = state.Trends;
                        if (_trendActive && IsActive && BackgroundHost.IsVisible)
                        {
                            History.Record(updated, status, active: true, visible: true);
                            trends = History.Snapshot();
                        }
                        return state with
                        {
                            Status = status,
                            Error = null,
                            Loading = false,
                            Backend = bootstrap.Backend,
                            ConfigError = bootstrap.ConfigError,
                            LastUpdated = updated,
                            Trends = trends,
                            Page = page,
                            Fields = data["fields"]!.Deserialize<string[]>(Core.Json)!,
                        };
                    });

                    foreach (var raised in events)
                    {
                        EventBus.Raise(raised);
                    }
                }
                catch (Exception error)
                {
                    // A refused read is reportable, not fatal: keep the last good
                    // snapshot on screen and say why it is stale.
                    Publish(state =>
                    {
                        if (revision != _pageRevision || !IsActive) return state;
                        History.MarkGap();
                        return state with
                        {
                            Error = error.Message, Loading = false, Page = page,
                            Fields = state.Page == page ? state.Fields : [],
                            LastUpdated = state.Page == page ? state.LastUpdated : null,
                        };
                    });
                }

                // Keep the cadence even when a read itself took most of a second.
                var elapsed = Environment.TickCount64 - started;
                var wait = Math.Max(200, 1000 - (int)elapsed);
                await WaitForTickAsync(wait).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private static void Publish(Func<MachineState, MachineState> change)
    {
        Action[] listeners;
        lock (Gate)
        {
            var next = change(_snapshot);
            if (Equals(next, _snapshot))
            {
                return;
            }

            _snapshot = next;
            listeners = [.. Listeners];
        }

        // Listeners are Reactor state setters and belong on the UI thread.
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            Notify(listeners);
        }
        else
        {
            dispatcher.TryEnqueue(() => Notify(listeners));
        }
    }

    private static void Notify(Action[] listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener();
            }
            catch (Exception)
            {
                // One broken listener must not stop the others.
            }
        }
    }
}
