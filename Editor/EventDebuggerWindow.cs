using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GOC.SOEvents.Editor
{
    public class EventDebuggerWindow : EditorWindow
    {
        private enum SortColumn
        {
            Name,
            Type,
            Listeners,
            Fires,
            LastFired
        }

        private static readonly string[] TypeOptions =
        {
            "All",
            "Void",
            "Int",
            "Float",
            "Bool",
            "String",
            "Vector2",
            "Vector3",
            "Custom"
        };

        private string _searchFilter = string.Empty;
        private string _typeFilter = "All";
        private int _selectedId = -1;
        private Vector2 _tableScroll;
        private Vector2 _detailScroll;
        private readonly Dictionary<int, string> _raiseInputs = new();
        private SortColumn _sortColumn = SortColumn.Fires;
        private bool _sortDescending = true;
        private string _raiseError = string.Empty;

        [MenuItem("Tools/SO Events/Event Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventDebuggerWindow>("Event Debugger");
            window.minSize = new Vector2(700f, 420f);
        }

        private void OnEnable()
        {
            EventDebuggerTracker.ScanProject();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            float detailHeight = _selectedId == -1 ? 0f : Mathf.Min(260f, position.height * 0.42f);
            float tableHeight = Mathf.Max(150f, position.height - detailHeight - 45f);
            DrawTable(tableHeight);

            if (_selectedId != -1)
                DrawDetailPanel();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search", GUILayout.Width(45f));
            _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));

            GUILayout.Space(8f);
            GUILayout.Label("Type", GUILayout.Width(30f));

            int typeIndex = Array.IndexOf(TypeOptions, _typeFilter);
            if (typeIndex < 0)
                typeIndex = 0;

            typeIndex = EditorGUILayout.Popup(typeIndex, TypeOptions, EditorStyles.toolbarPopup, GUILayout.Width(90f));
            _typeFilter = TypeOptions[typeIndex];

            GUILayout.FlexibleSpace();
            DrawLiveStatus();

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                EventDebuggerTracker.ResetAll();
                _raiseError = string.Empty;
            }

            if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                EventDebuggerTracker.ScanProject();
                ValidateSelection();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLiveStatus()
        {
            Color previous = GUI.color;
            GUI.color = EventDebuggerTracker.IsTracking ? new Color(0.35f, 0.85f, 0.35f) : new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label(EventDebuggerTracker.IsTracking ? "LIVE" : "IDLE", EditorStyles.miniLabel, GUILayout.Width(32f));
            GUI.color = previous;
        }

        private void DrawTable(float tableHeight)
        {
            var entries = BuildFilteredEntries();
            DrawTableHeader();

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll, GUILayout.Height(tableHeight));
            for (int i = 0; i < entries.Count; i++)
                DrawRow(entries[i]);
            EditorGUILayout.EndScrollView();
        }

        private List<EventDebuggerEntry> BuildFilteredEntries()
        {
            IEnumerable<EventDebuggerEntry> query = EventDebuggerTracker.Entries.Values.Where(e => e != null && e.Asset != null);

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                string search = _searchFilter.Trim();
                query = query.Where(e => e.Name != null && e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.Equals(_typeFilter, "All", StringComparison.Ordinal))
                query = query.Where(e => string.Equals(e.TypeLabel, _typeFilter, StringComparison.Ordinal));

            return SortEntries(query).ToList();
        }

        private IEnumerable<EventDebuggerEntry> SortEntries(IEnumerable<EventDebuggerEntry> entries)
        {
            Func<EventDebuggerEntry, object> keySelector = _sortColumn switch
            {
                SortColumn.Name => e => e.Name ?? string.Empty,
                SortColumn.Type => e => e.TypeLabel ?? string.Empty,
                SortColumn.Listeners => e => EventDebuggerTracker.GetListenerCount(e.Asset),
                SortColumn.LastFired => e => e.LastFiredTime,
                _ => e => e.FireCount
            };

            IOrderedEnumerable<EventDebuggerEntry> ordered = _sortDescending
                ? entries.OrderByDescending(keySelector)
                : entries.OrderBy(keySelector);

            return ordered.ThenBy(e => e.Name ?? string.Empty);
        }

        private void DrawTableHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(string.Empty, GUILayout.Width(22f));
            DrawSortButton("Event", SortColumn.Name, 230f);
            DrawSortButton("Type", SortColumn.Type, 90f);
            DrawSortButton("Listeners", SortColumn.Listeners, 80f);
            DrawSortButton("Fires", SortColumn.Fires, 60f);
            DrawSortButton("Last Fired", SortColumn.LastFired, 90f);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSortButton(string label, SortColumn column, float width)
        {
            string suffix = _sortColumn == column ? (_sortDescending ? " v" : " ^") : string.Empty;

            if (GUILayout.Button(label + suffix, EditorStyles.miniButton, GUILayout.Width(width)))
                ToggleSort(column);
        }

        private void ToggleSort(SortColumn column)
        {
            if (_sortColumn == column)
            {
                _sortDescending = !_sortDescending;
                return;
            }

            _sortColumn = column;
            _sortDescending = true;
        }

        private void DrawRow(EventDebuggerEntry entry)
        {
            bool selected = _selectedId == entry.InstanceId;
            int listenerCount = EventDebuggerTracker.GetListenerCount(entry.Asset);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = selected ? new Color(0.78f, 0.86f, 0.98f) : previous;
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = previous;

            DrawStatusIcon(entry, listenerCount);

            GUIStyle nameStyle = selected ? EditorStyles.boldLabel : EditorStyles.label;
            if (GUILayout.Button(entry.Name, nameStyle, GUILayout.Width(230f)))
            {
                _selectedId = entry.InstanceId;
                _raiseError = string.Empty;
            }

            GUILayout.Label(entry.TypeLabel, GUILayout.Width(90f));
            GUILayout.Label(listenerCount.ToString(CultureInfo.InvariantCulture), GUILayout.Width(80f));
            GUILayout.Label(entry.FireCount.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
            GUILayout.Label(FormatTime(entry.LastFiredTime), GUILayout.Width(90f));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusIcon(EventDebuggerEntry entry, int listenerCount)
        {
            string icon = string.Empty;
            Color color = GUI.color;

            if (IsRecentlyFired(entry))
            {
                icon = "o";
                color = new Color(0.25f, 0.85f, 0.25f);
            }
            else if (Application.isPlaying && listenerCount == 0)
            {
                icon = "!";
                color = new Color(0.95f, 0.75f, 0.2f);
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUILayout.Label(icon, GUILayout.Width(22f));
            GUI.color = previous;
        }

        private bool IsRecentlyFired(EventDebuggerEntry entry)
        {
            if (!Application.isPlaying)
                return false;

            if (entry.LastFiredTime < 0f)
                return false;

            return Time.realtimeSinceStartup - entry.LastFiredTime <= 1f;
        }

        private void DrawDetailPanel()
        {
            if (!ValidateSelection())
                return;

            var entry = EventDebuggerTracker.Entries[_selectedId];
            EventDebuggerTracker.GetListenerBreakdown(entry.Asset, out int inspectorCount, out int codeCount);

            EditorGUILayout.BeginVertical("box");

            GUILayout.Label($"Selected: {entry.Name} ({entry.Asset.GetType().Name})", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDetailRow("Asset Path", entry.AssetPath);
            DrawDetailRow("Listeners (Inspector)", inspectorCount.ToString(CultureInfo.InvariantCulture));
            DrawDetailRow("Listeners (Code)", codeCount.ToString(CultureInfo.InvariantCulture));
            DrawDetailRow("Total Fires", entry.FireCount.ToString(CultureInfo.InvariantCulture));

            if (entry.IsTyped)
                DrawDetailRow("Last Value", string.IsNullOrEmpty(entry.LastValue) ? "-" : entry.LastValue);

            DrawDetailRow("Last Fired", FormatTime(entry.LastFiredTime));

            EditorGUILayout.Space(6f);
            DrawActionRow(entry);

            if (!string.IsNullOrEmpty(_raiseError))
                EditorGUILayout.HelpBox(_raiseError, MessageType.Warning);

            EditorGUILayout.Space(6f);
            GUILayout.Label("Fire History", EditorStyles.boldLabel);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.Height(120f));
            if (entry.History.Count == 0)
            {
                GUILayout.Label("No fires recorded.");
            }
            else
            {
                for (int i = 0; i < entry.History.Count; i++)
                    DrawHistoryRow(entry.History[i], entry.IsTyped);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private bool ValidateSelection()
        {
            if (_selectedId == -1)
                return false;

            if (!EventDebuggerTracker.TryGetEntry(_selectedId, out var entry) || entry == null || entry.Asset == null)
            {
                _selectedId = -1;
                return false;
            }

            return true;
        }

        private void DrawDetailRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            GUILayout.Label(value);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionRow(EventDebuggerEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            if (!entry.IsTyped)
            {
                if (GUILayout.Button("Raise", GUILayout.Width(80f)))
                    RaiseVoid(entry);
            }
            else
            {
                string currentInput = GetRaiseInput(entry.InstanceId);
                GUILayout.Label("Raise With Value", GUILayout.Width(110f));

                string updatedInput = GUILayout.TextField(currentInput, GUILayout.Width(180f));
                if (!string.Equals(updatedInput, currentInput, StringComparison.Ordinal))
                    _raiseInputs[entry.InstanceId] = updatedInput;

                bool supported = IsSupportedType(entry.ValueType);
                EditorGUI.BeginDisabledGroup(!supported);
                if (GUILayout.Button("Raise", GUILayout.Width(80f)))
                    RaiseTyped(entry, GetRaiseInput(entry.InstanceId));
                EditorGUI.EndDisabledGroup();

                if (!supported)
                    GUILayout.Label("Unsupported type", EditorStyles.miniLabel, GUILayout.Width(95f));
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Ping Asset", GUILayout.Width(90f)))
            {
                Selection.activeObject = entry.Asset;
                EditorGUIUtility.PingObject(entry.Asset);
            }

            if (GUILayout.Button("Reset Entry", GUILayout.Width(90f)))
                EventDebuggerTracker.ResetEntry(entry.InstanceId);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHistoryRow(EventFireRecord record, bool isTyped)
        {
            string value = isTyped ? (string.IsNullOrEmpty(record.Value) ? "-" : record.Value) : "-";

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"#{record.FireNumber}", GUILayout.Width(50f));
            GUILayout.Label(FormatTime(record.Timestamp), GUILayout.Width(90f));
            GUILayout.Label(value);
            EditorGUILayout.EndHorizontal();
        }

        private void RaiseVoid(EventDebuggerEntry entry)
        {
            _raiseError = string.Empty;
            if (entry.Asset is GameEvent eventAsset)
                eventAsset.Raise();
        }

        private void RaiseTyped(EventDebuggerEntry entry, string input)
        {
            _raiseError = string.Empty;

            if (!TryParseInput(entry.ValueType, input, out object parsedValue, out string error))
            {
                _raiseError = error;
                return;
            }

            MethodInfo raiseMethod = FindRaiseMethod(entry.Asset.GetType());
            if (raiseMethod == null)
            {
                _raiseError = "Unable to find Raise method.";
                return;
            }

            try
            {
                raiseMethod.Invoke(entry.Asset, new[] { parsedValue });
            }
            catch (Exception ex)
            {
                _raiseError = ex.InnerException?.Message ?? ex.Message;
            }
        }

        private static MethodInfo FindRaiseMethod(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "Raise")
                        continue;

                    if (method.GetParameters().Length == 1)
                        return method;
                }
            }

            return null;
        }

        private static bool TryParseInput(Type valueType, string input, out object parsedValue, out string error)
        {
            parsedValue = null;
            error = string.Empty;

            if (valueType == null)
            {
                error = "Unknown event value type.";
                return false;
            }

            if (valueType == typeof(string))
            {
                parsedValue = input ?? string.Empty;
                return true;
            }

            if (valueType == typeof(int))
            {
                if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    parsedValue = intValue;
                    return true;
                }

                error = "Enter a valid int value.";
                return false;
            }

            if (valueType == typeof(float))
            {
                if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    parsedValue = floatValue;
                    return true;
                }

                error = "Enter a valid float value.";
                return false;
            }

            if (valueType == typeof(bool))
            {
                if (bool.TryParse(input, out bool boolValue))
                {
                    parsedValue = boolValue;
                    return true;
                }

                error = "Enter true or false.";
                return false;
            }

            if (valueType == typeof(Vector2))
            {
                if (TryParseVector(input, 2, out var parts))
                {
                    parsedValue = new Vector2(parts[0], parts[1]);
                    return true;
                }

                error = "Enter Vector2 as x,y.";
                return false;
            }

            if (valueType == typeof(Vector3))
            {
                if (TryParseVector(input, 3, out var parts))
                {
                    parsedValue = new Vector3(parts[0], parts[1], parts[2]);
                    return true;
                }

                error = "Enter Vector3 as x,y,z.";
                return false;
            }

            if (valueType.IsEnum)
            {
                if (Enum.TryParse(valueType, input, true, out object enumValue))
                {
                    parsedValue = enumValue;
                    return true;
                }

                error = $"Enter a valid {valueType.Name} value.";
                return false;
            }

            error = $"Unsupported value type: {valueType.Name}.";
            return false;
        }

        private static bool TryParseVector(string input, int count, out float[] values)
        {
            values = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string cleaned = input.Trim().Replace("(", string.Empty).Replace(")", string.Empty);
            string[] tokens = cleaned.Split(',');
            if (tokens.Length != count)
                return false;

            var parsed = new float[count];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!float.TryParse(tokens[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                    return false;
            }

            values = parsed;
            return true;
        }

        private static bool IsSupportedType(Type valueType)
        {
            if (valueType == null)
                return false;

            return valueType == typeof(int)
                || valueType == typeof(float)
                || valueType == typeof(bool)
                || valueType == typeof(string)
                || valueType == typeof(Vector2)
                || valueType == typeof(Vector3)
                || valueType.IsEnum;
        }

        private string GetRaiseInput(int id)
        {
            if (_raiseInputs.TryGetValue(id, out var input))
                return input;

            _raiseInputs[id] = string.Empty;
            return string.Empty;
        }

        private static string FormatTime(float absoluteTime)
        {
            if (absoluteTime < 0f)
                return "-";

            float offset = Mathf.Max(0f, absoluteTime - EventDebuggerTracker.PlayModeStartTime);
            int totalHundredths = Mathf.FloorToInt(offset * 100f);
            int minutes = totalHundredths / 6000;
            int seconds = (totalHundredths / 100) % 60;
            int hundredths = totalHundredths % 100;

            return $"{minutes:00}:{seconds:00}.{hundredths:00}";
        }
    }
}
