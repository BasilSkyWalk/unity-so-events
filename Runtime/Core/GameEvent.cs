using System;
using System.Collections.Generic;
using UnityEngine;

namespace GOC.SOEvents
{
    [CreateAssetMenu(menuName = "SO Events/Game Event (Void)", order = 0)]
    public class GameEvent : ScriptableObject
    {
        private readonly List<GameEventListener> _listeners = new();
        private readonly List<Action> _codeCallbacks = new();

        public void Raise()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i].OnEventRaised();

            for (int i = _codeCallbacks.Count - 1; i >= 0; i--)
                _codeCallbacks[i]?.Invoke();
        }

        public void RegisterListener(GameEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(GameEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Subscribe(Action callback)
        {
            if (callback != null && !_codeCallbacks.Contains(callback))
                _codeCallbacks.Add(callback);
        }

        public void Unsubscribe(Action callback)
        {
            _codeCallbacks.Remove(callback);
        }

        public int ListenerCount => _listeners.Count + _codeCallbacks.Count;
    }
}
