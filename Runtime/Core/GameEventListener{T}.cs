using UnityEngine;
using UnityEngine.Events;

namespace GOC.SOEvents
{
    public abstract class GameEventListener<T> : MonoBehaviour
    {
        [SerializeField] private GameEvent<T> _event;
        [SerializeField] private UnityEvent<T> _response;

        public GameEvent<T> Event => _event;
        public UnityEvent<T> Response => _response;

        private void OnEnable()
        {
            if (_event != null)
                _event.RegisterListener(this);
        }

        private void OnDisable()
        {
            if (_event != null)
                _event.UnregisterListener(this);
        }

        public void OnEventRaised(T value)
        {
            _response?.Invoke(value);
        }
    }
}
