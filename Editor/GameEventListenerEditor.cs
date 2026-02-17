using UnityEngine;
using UnityEditor;

namespace GOC.SOEvents.Editor
{
    [CustomEditor(typeof(GameEventListener))]
    public class GameEventListenerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var listener = (GameEventListener)target;

            if (listener.Event != null)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button($"Ping: {listener.Event.name}"))
                {
                    EditorGUIUtility.PingObject(listener.Event);
                    Selection.activeObject = listener.Event;
                }
            }
        }
    }
}
