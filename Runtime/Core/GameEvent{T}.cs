using System;
using System.Collections.Generic;
using UnityEngine;

namespace GOC.SOEvents
{
    public abstract class GameEvent<T> : ScriptableObject
    {
        private readonly List<GameEventListener<T>> _listeners = new();
        private readonly List<Action<T>> _codeCallbacks = new();

        public void Raise(T value)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i].OnEventRaised(value);

            for (int i = _codeCallbacks.Count - 1; i >= 0; i--)
                _codeCallbacks[i]?.Invoke(value);
        }

        public void RegisterListener(GameEventListener<T> listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(GameEventListener<T> listener)
        {
            _listeners.Remove(listener);
        }

        public void Subscribe(Action<T> callback)
        {
            if (callback != null && !_codeCallbacks.Contains(callback))
                _codeCallbacks.Add(callback);
        }

        public void Unsubscribe(Action<T> callback)
        {
            _codeCallbacks.Remove(callback);
        }

        public int ListenerCount => _listeners.Count + _codeCallbacks.Count;
    }
}
