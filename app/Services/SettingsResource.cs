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
    private readonly SemaphoreSlim _reads = new(1, 1);
    private SettingsSnapshot<T> _snapshot = new();
    private event Action? Changed;
    private bool _disposed;
    public SettingsSnapshot<T> Snapshot { get { lock (_gate) return _snapshot; } }
    public Action Subscribe(Action listener)
    {
        lock (_gate) Changed += listener;
        return () => { lock (_gate) Changed -= listener; };
    }
    public void Refresh() => _ = RefreshAsync();
    public async Task RefreshAsync(Func<T, Task<T>>? update = null)
    {
        await _reads.WaitAsync();
        try
        {
            if (_disposed) return;
            Publish(Snapshot with { Refreshing = true });
            try
            {
                var previous = Snapshot.Value;
                var value = update is not null && previous is not null ? await update(previous) : await fetch();
                Publish(new(value));
            }
            catch (Exception error)
            {
                StartupLog.Write(error);
                Publish(Snapshot with { Error = error.Message, Refreshing = false });
            }
        }
        finally { _reads.Release(); }
    }
    private void Publish(SettingsSnapshot<T> snapshot)
    {
        Action? listeners;
        lock (_gate)
        {
            if (_disposed) return;
            _snapshot = snapshot;
            listeners = Changed;
        }
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess) listeners?.Invoke();
        else dispatcher.TryEnqueue(() => { if (!_disposed) listeners?.Invoke(); });
    }
    public void Dispose() { lock (_gate) { _disposed = true; Changed = null; } }
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
