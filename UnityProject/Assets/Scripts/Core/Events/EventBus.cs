using System;
using System.Collections.Generic;

namespace BorrowedSouls.Core.Events
{
    /// <summary>
    /// Typed, instance-owned event bus. Events are structs so publishing does not allocate.
    /// Gameplay publishes facts (hit confirmed, vessel broken...); presentation subscribes.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            Type key = typeof(T);
            _handlers.TryGetValue(key, out Delegate existing);
            _handlers[key] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            Type key = typeof(T);
            if (!_handlers.TryGetValue(key, out Delegate existing)) return;
            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(key);
            else _handlers[key] = remaining;
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out Delegate d))
            {
                ((Action<T>)d).Invoke(evt);
            }
        }

        public int HandlerCount<T>() where T : struct
        {
            return _handlers.TryGetValue(typeof(T), out Delegate d) ? d.GetInvocationList().Length : 0;
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
