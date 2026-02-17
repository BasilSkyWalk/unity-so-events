using UnityEngine;
using UnityEditor;
using System.IO;

namespace GOC.SOEvents.Editor
{
    public class EventCreatorWindow : EditorWindow
    {
        private enum EventType
        {
            Void,
            Int,
            Float,
            Bool,
            String,
            Vector2,
            Vector3
        }

        private EventType _selectedType = EventType.Void;
        private string _eventName = "NewEvent";
        private string _folder = "Assets/ScriptableObjects/Events";

        [MenuItem("Tools/SO Events/Event Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventCreatorWindow>("Event Creator");
            window.minSize = new Vector2(350, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Create SO Event Asset", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _selectedType = (EventType)EditorGUILayout.EnumPopup("Event Type", _selectedType);
            _eventName = EditorGUILayout.TextField("Event Name", _eventName);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            _folder = EditorGUILayout.TextField("Folder", _folder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var picked = EditorUtility.OpenFolderPanel("Pick folder", _folder, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    if (picked.StartsWith(Application.dataPath))
                        _folder = "Assets" + picked.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            var typeSuffix = _selectedType == EventType.Void ? "GameEvent" : $"{_selectedType}Event";
            EditorGUILayout.HelpBox(
                $"Will create:\n{_folder}/{_eventName}.asset\nType: {typeSuffix}",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            GUI.enabled = !string.IsNullOrWhiteSpace(_eventName);
            if (GUILayout.Button("Create Event", GUILayout.Height(30)))
                CreateEvent();
            GUI.enabled = true;
        }

        private void CreateEvent()
        {
            if (!AssetDatabase.IsValidFolder(_folder))
                CreateFolderRecursive(_folder);

            string path = $"{_folder}/{_eventName}.asset";

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
            {
                EditorUtility.DisplayDialog("Duplicate", $"Asset already exists at:\n{path}", "OK");
                return;
            }

            ScriptableObject asset = _selectedType switch
            {
                EventType.Void => CreateInstance<GameEvent>(),
                EventType.Int => CreateInstance<IntEvent>(),
                EventType.Float => CreateInstance<FloatEvent>(),
                EventType.Bool => CreateInstance<BoolEvent>(),
                EventType.String => CreateInstance<StringEvent>(),
                EventType.Vector2 => CreateInstance<Vector2Event>(),
                EventType.Vector3 => CreateInstance<Vector3Event>(),
                _ => null
            };

            if (asset == null) return;

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
