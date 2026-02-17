using UnityEngine;
using UnityEngine.Events;

namespace GOC.SOEvents
{
    [AddComponentMenu("SO Events/Game Event Listener")]
    public class GameEventListener : MonoBehaviour
    {
        [Tooltip("The event asset to listen to")]
        [SerializeField] private GameEvent _event;

        [Tooltip("Response to invoke when the event is raised")]
        [SerializeField] private UnityEvent _response;

        public GameEvent Event => _event;
        public UnityEvent Response => _response;

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

        public void OnEventRaised()
        {
            _response?.Invoke();
        }
    }
}
