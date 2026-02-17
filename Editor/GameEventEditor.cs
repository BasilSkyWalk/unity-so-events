using UnityEngine;
using UnityEditor;

namespace GOC.SOEvents.Editor
{
    [CustomEditor(typeof(GameEvent))]
    public class GameEventEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var evt = (GameEvent)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Listeners", evt.ListenerCount.ToString());

            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Raise (Play Mode Only)"))
                evt.Raise();
            GUI.enabled = true;
        }
    }
}
