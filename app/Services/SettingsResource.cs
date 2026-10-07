using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;

namespace JiYaoChu.Services;

/// <summary>Keeps the mounted settings tree while a readback is in progress or fails.</summary>
public sealed record SettingsSnapshot<T>(T? Value = null, string? Error = null, bool Refreshing = false) where T : class
{
    public R Match<R>(Func<R> loading, Func<T, R> loaded, Func<Exception, R> error)
        => Value is { } value ? loaded(value) : Error is { } message ? error(new InvalidOperationException(message)) : loading();
}

public sealed class SettingsResource<T>(Func<Task<T>> fetch) : IDisposable where T : class
{
    private readonly object _gate = new();
    private SettingsSnapshot<T> _snapshot = new();
    private event Action? Changed;
    private bool _disposed;
    private bool _running;
    private RefreshBatch? _pending;
    private RefreshBatch? _active;

    private sealed class RefreshBatch
    {
        public bool Full;
        public List<(string? Key, Func<T, Task<T>> Update)> Updates { get; } = [];
        public List<TaskCompletionSource> Callers { get; } = [];
    }
    public SettingsSnapshot<T> Snapshot { get { lock (_gate) return _snapshot; } }
    public Action Subscribe(Action listener)
    {
        lock (_gate) { if (!_disposed) Changed += listener; }
        return () => { lock (_gate) Changed -= listener; };
    }
    public void Refresh() => _ = RefreshAsync();
    public void Refresh(string sectionKey, Func<T, Task<T>> update) => _ = RefreshAsync(sectionKey, update);

    /// <summary>Full reads coalesce. Unkeyed partial updates retain their individual order.</summary>
    public Task RefreshAsync(Func<T, Task<T>>? update = null) => QueueRefresh(null, update);

    /// <summary>
    /// Coalesces pending reads for the same section using its newest updater. Distinct sections
    /// receive the latest snapshot in turn. A full fetch must cover every section it supersedes.
    /// Requests arriving after a batch starts always belong to a trailing batch.
    /// </summary>
    public Task RefreshAsync(string sectionKey, Func<T, Task<T>> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionKey);
        ArgumentNullException.ThrowIfNull(update);
        return QueueRefresh(sectionKey, update);
    }

    private Task QueueRefresh(string? key, Func<T, Task<T>>? update)
    {
        var caller = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool start;
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            var batch = _pending ??= new();
            batch.Callers.Add(caller);
            if (update is null)
            {
                batch.Full = true;
                batch.Updates.Clear();
            }
            else if (!batch.Full)
            {
                var existing = key is null ? -1 : batch.Updates.FindIndex(item => item.Key == key);
                if (existing < 0) batch.Updates.Add((key, update));
                else batch.Updates[existing] = (key, update);
            }
            start = !_running;
            _running = true;
        }
        if (start) _ = ReadPendingAsync();
        return caller.Task;
    }

    private async Task ReadPendingAsync()
    {
        while (true)
        {
            RefreshBatch batch;
            lock (_gate)
            {
                if (_disposed || _pending is null) { _running = false; return; }
                batch = _pending;
                _pending = null;
                _active = batch;
            }
            Publish(Snapshot with { Refreshing = true });
            lock (_gate)
            {
                if (_disposed) { _active = null; _running = false; return; }
            }
            var value = Snapshot.Value;
            string? failure = null;
            try
            {
                if (batch.Full || value is null) value = await fetch();
                else
                {
                    foreach (var (_, update) in batch.Updates)
                    {
                        lock (_gate) { if (_disposed) break; }
                        try { value = await update(value); }
                        catch (Exception error) { StartupLog.Write(error); failure ??= error.Message; }
                    }
                }
            }
            catch (Exception error)
            {
                StartupLog.Write(error);
                failure = error.Message;
            }
            Publish(new(value, failure));
            lock (_gate) _active = null;
            foreach (var caller in batch.Callers) caller.TrySetResult();
        }
    }
    private void Publish(SettingsSnapshot<T> snapshot)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _snapshot = snapshot;
        }
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess) Notify();
        else dispatcher.TryEnqueue(Notify);
    }

    private void Notify()
    {
        // Synchronize notification with disposal, including callbacks already in the UI queue.
        lock (_gate)
        {
            if (_disposed || Changed is null) return;
            foreach (Action listener in Changed.GetInvocationList())
            {
                if (_disposed) break;
                try { listener(); }
                catch (Exception error) { StartupLog.Write(error); }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Changed = null;
            _snapshot = _snapshot with { Refreshing = false };
            if (_pending is { } pending) foreach (var caller in pending.Callers) caller.TrySetResult();
            if (_active is { } active) foreach (var caller in active.Callers) caller.TrySetResult();
            _pending = null;
        }
    }
}

public abstract class SettingsPage : Component
{
    protected (SettingsResource<T> Resource, SettingsSnapshot<T> State) UseSettings<T>(Func<Task<T>> fetch) where T : class
    {
        var resource = UseMemo(() => new SettingsResource<T>(fetch), []);
        var snapshot = UseExternalStore(resource.Subscribe, () => resource.Snapshot);
        UseEffect(() => { resource.Refresh(); return resource.Dispose; }, []);
        return (resource, snapshot);
    }
}
