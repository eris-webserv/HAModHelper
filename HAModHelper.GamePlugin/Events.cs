using System;
using System.Collections.Generic;

namespace HAModHelper.Events;

public sealed class EventBus
{
    public static EventBus Instance { get; } = new EventBus();

    private readonly Dictionary<Type, List<Delegate>> _subs = new();

    private EventBus() { }

    public IDisposable Subscribe<TEvent>(Action<TEvent> callback) where TEvent : BaseEvent
    {
        var t = typeof(TEvent);
        if (!_subs.TryGetValue(t, out var list))
        {
            list = new List<Delegate>();
            _subs[t] = list;
        }

        list.Add(callback);

        return new Subscription(() =>
        {
            if (_subs.TryGetValue(t, out var l))
                l.Remove(callback);
        });
    }

    public BaseEvent Fire<TEvent>(TEvent ev) where TEvent : BaseEvent
    {
        if (ev.Fired)
        {
            throw new Exception($"Event by type {typeof(TEvent).FullName} was fired multiple times!");
        };

        var t = typeof(TEvent);
        if (!_subs.TryGetValue(t, out var list) || list.Count == 0)
            return ev;

        ev.Fired = true; // NO REFIRING THE SAME EVENT YOU CHUD.

        // Copy to avoid issues if a handler unsubscribes while firing.
        var snapshot = list.ToArray();
        foreach (var d in snapshot)
        {
            if (ev.Cancelled) break;

            ((Action<TEvent>)d)(ev);
        }

        return ev;
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;
        public Subscription(Action dispose) => _dispose = dispose;
        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}

public abstract class BaseEvent
{
    public bool Handled { get; set; } = false;
    public bool Cancelled { get; set; } = false;
    public bool Fired { get; internal set; } = false;
}
