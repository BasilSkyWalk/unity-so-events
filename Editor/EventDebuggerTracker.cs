using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GOC.SOEvents.Editor
{
    [InitializeOnLoad]
    public static class EventDebuggerTracker
    {
        private static readonly Dictionary<int, EventDebuggerEntry> _entries = new();
        private static readonly Dictionary<int, Delegate> _hooks = new();

        public static IReadOnlyDictionary<int, EventDebuggerEntry> Entries => _entries;
        public static bool IsTracking { get; private set; }
        public static float PlayModeStartTime { get; private set; }

        static EventDebuggerTracker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ScanProject();
        }

        public static void ScanProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameEvent");
            var seen = new HashSet<int>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                    continue;

                int id = asset.GetInstanceID();
                seen.Add(id);

                if (_entries.TryGetValue(id, out var existing))
                {
                    existing.Asset = asset;
                    existing.Name = asset.name;
                    existing.AssetPath = path;
                    existing.IsTyped = TryGetEventValueType(asset.GetType(), out var existingType);
                    existing.ValueType = existingType;
                    existing.TypeLabel = GetTypeLabel(asset, existingType);

                    if (IsTracking && !_hooks.ContainsKey(id))
                        HookEntry(id, existing);

                    continue;
                }

                bool isTyped = TryGetEventValueType(asset.GetType(), out var valueType);
                var entry = new EventDebuggerEntry
                {
                    InstanceId = id,
                    Asset = asset,
                    Name = asset.name,
                    TypeLabel = GetTypeLabel(asset, valueType),
                    AssetPath = path,
                    IsTyped = isTyped,
                    ValueType = valueType,
                    LastFiredTime = -1f
                };

                _entries[id] = entry;

                if (IsTracking)
                    HookEntry(id, entry);
            }

            var staleIds = new List<int>();
            foreach (var kvp in _entries)
            {
                if (!seen.Contains(kvp.Key))
                    staleIds.Add(kvp.Key);
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                int id = staleIds[i];

                if (_hooks.TryGetValue(id, out var hook))
                {
                    UnhookEntry(_entries[id], hook);
                    _hooks.Remove(id);
                }

                _entries.Remove(id);
            }
        }

        public static bool TryGetEntry(int instanceId, out EventDebuggerEntry entry)
        {
            return _entries.TryGetValue(instanceId, out entry);
        }

        public static void ResetAll()
        {
            foreach (var entry in _entries.Values)
                entry.Reset();
        }

        public static void ResetEntry(int instanceId)
        {
            if (_entries.TryGetValue(instanceId, out var entry))
                entry.Reset();
        }

        public static int GetListenerCount(ScriptableObject asset)
        {
            GetListenerBreakdown(asset, out int inspectorCount, out int codeCount);
            return inspectorCount + codeCount;
        }

        public static void GetListenerBreakdown(ScriptableObject asset, out int inspectorCount, out int codeCount)
        {
            inspectorCount = 0;
            codeCount = 0;

            if (asset == null)
                return;

            inspectorCount = GetPrivateCollectionCount(asset, "_listeners");
            codeCount = GetPrivateCollectionCount(asset, "_codeCallbacks");
        }

        private static int GetPrivateCollectionCount(object target, string fieldName)
        {
            FieldInfo field = GetFieldRecursive(target.GetType(), fieldName);
            if (field == null)
                return 0;

            object value = field.GetValue(target);
            if (value is ICollection collection)
                return collection.Count;

            return 0;
        }

        private static FieldInfo GetFieldRecursive(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    PlayModeStartTime = Time.realtimeSinceStartup;
                    IsTracking = true;
                    ScanProject();
                    HookAll();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    UnhookAll();
                    IsTracking = false;
                    break;
            }
        }

        private static void HookAll()
        {
            foreach (var kvp in _entries)
            {
                if (_hooks.ContainsKey(kvp.Key))
                    continue;

                HookEntry(kvp.Key, kvp.Value);
            }
        }

        private static void HookEntry(int id, EventDebuggerEntry entry)
        {
            if (entry.Asset == null)
                return;

            if (entry.Asset is GameEvent voidEvent)
            {
                Action voidHook = () => RecordFire(id, null);
                voidEvent.Subscribe(voidHook);
                _hooks[id] = voidHook;
                return;
            }

            if (!TryGetEventValueType(entry.Asset.GetType(), out var valueType))
                return;

            MethodInfo method = typeof(EventDebuggerTracker).GetMethod(
                nameof(CreateTypedHook),
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (method == null)
                return;

            MethodInfo genericMethod = method.MakeGenericMethod(valueType);
            object hookObj = genericMethod.Invoke(null, new object[] { id });
            if (hookObj is not Delegate hook)
                return;

            MethodInfo subscribeMethod = FindEventMethod(entry.Asset.GetType(), "Subscribe");
            if (subscribeMethod == null)
                return;

            subscribeMethod.Invoke(entry.Asset, new object[] { hook });
            _hooks[id] = hook;
        }

        private static Action<T> CreateTypedHook<T>(int id)
        {
            return value => RecordFire(id, value);
        }

        private static void UnhookAll()
        {
            foreach (var kvp in _hooks)
            {
                if (_entries.TryGetValue(kvp.Key, out var entry))
                    UnhookEntry(entry, kvp.Value);
            }

            _hooks.Clear();
        }

        private static void UnhookEntry(EventDebuggerEntry entry, Delegate hook)
        {
            if (entry?.Asset == null || hook == null)
                return;

            if (entry.Asset is GameEvent voidEvent && hook is Action voidHook)
            {
                voidEvent.Unsubscribe(voidHook);
                return;
            }

            MethodInfo unsubscribeMethod = FindEventMethod(entry.Asset.GetType(), "Unsubscribe");
            unsubscribeMethod?.Invoke(entry.Asset, new object[] { hook });
        }

        private static MethodInfo FindEventMethod(Type type, string methodName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                );

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != methodName)
                        continue;

                    if (method.GetParameters().Length == 1)
                        return method;
                }
            }

            return null;
        }

        private static void RecordFire(int id, object value)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return;

            float timestamp = Time.realtimeSinceStartup;
            string valueText = value?.ToString();
            entry.RecordFire(timestamp, valueText);
        }

        private static string GetTypeLabel(ScriptableObject asset, Type valueType)
        {
            if (asset is GameEvent)
                return "Void";

            if (valueType == typeof(int))
                return "Int";
            if (valueType == typeof(float))
                return "Float";
            if (valueType == typeof(bool))
                return "Bool";
            if (valueType == typeof(string))
                return "String";
            if (valueType == typeof(Vector2))
                return "Vector2";
            if (valueType == typeof(Vector3))
                return "Vector3";

            return "Custom";
        }

        private static bool TryGetEventValueType(Type type, out Type valueType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;

                if (current.GetGenericTypeDefinition() != typeof(GameEvent<>))
                    continue;

                valueType = current.GetGenericArguments()[0];
                return true;
            }

            valueType = null;
            return false;
        }
    }
}
