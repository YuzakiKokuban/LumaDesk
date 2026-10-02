using Microsoft.UI.Reactor;
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

    /// <summary>True while a fresh snapshot is being read and none has ever arrived.</summary>
    public bool HasData => Status is not null;
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
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token);
        deadline.CancelAfter(milliseconds);

        try
        {
            await Nudge.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Either the cadence elapsed or the application is closing.
        }
    }

    private static async Task PollAsync()
    {
        try
        {
            while (!Stopping.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                try
                {
                    var bootstrap = await Backend.InitializeAsync().ConfigureAwait(false);
                    Publish(state => state with
                    {
                        Backend = bootstrap.Backend,
                        ConfigError = bootstrap.ConfigError,
                    });
                    var status = await Backend.CallAsync<HardwareStatus>("get_hardware_status")
                        .ConfigureAwait(false);
                    var events = await Backend.DrainEventsAsync().ConfigureAwait(false);

                    Publish(state => state with
                    {
                        Status = status,
                        Error = null,
                        Loading = false,
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
                    Publish(state => state with { Error = error.Message, Loading = false });
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
