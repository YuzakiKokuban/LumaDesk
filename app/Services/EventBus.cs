using Microsoft.UI.Reactor;
using JiYaoChu.Interop;

namespace JiYaoChu.Services;

/// <summary>
/// Fans out the events the backend queues (<c>osd://…</c>, <c>shell://…</c>,
/// <c>physical-mode-switched</c>) to whichever components care.
/// </summary>
/// <remarks>
/// Delivered on the UI thread, so a handler may touch Reactor state directly.
/// Events are transient: subscribe before the poll that would raise them.
/// </remarks>
public static class EventBus
{
    private static readonly object Gate = new();
    private static readonly List<Action<BackendEvent>> Listeners = [];

    /// <summary>Registers a handler and returns the function that removes it.</summary>
    public static Action Subscribe(Action<BackendEvent> listener)
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
            };
        };
    }

    /// <summary>Delivers one event to every listener. Safe to call from any thread.</summary>
    internal static void Raise(BackendEvent raised)
    {
        Action<BackendEvent>[] listeners;
        lock (Gate)
        {
            if (Listeners.Count == 0)
            {
                return;
            }

            listeners = [.. Listeners];
        }

        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            Deliver(listeners, raised);
        }
        else
        {
            dispatcher.TryEnqueue(() => Deliver(listeners, raised));
        }
    }

    private static void Deliver(Action<BackendEvent>[] listeners, BackendEvent raised)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener(raised);
            }
            catch (Exception)
            {
                // A handler that throws must not take the poll loop down with it.
            }
        }
    }
}
