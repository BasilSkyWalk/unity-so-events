using System;
using System.Collections.Generic;
using UnityEngine;

namespace GOC.SOEvents.Editor
{
    [Serializable]
    public class EventFireRecord
    {
        public int FireNumber;
        public float Timestamp;
        public string Value;
    }

    [Serializable]
    public class EventDebuggerEntry
    {
        public int InstanceId;
        public ScriptableObject Asset;
        public string Name;
        public string TypeLabel;
        public string AssetPath;
        public bool IsTyped;
        public Type ValueType;
        public int FireCount;
        public float LastFiredTime = -1f;
        public string LastValue;
        public List<EventFireRecord> History = new(20);

        public void RecordFire(float timestamp, string value = null)
        {
            FireCount++;
            LastFiredTime = timestamp;
            LastValue = value;

            History.Insert(0, new EventFireRecord
            {
                FireNumber = FireCount,
                Timestamp = timestamp,
                Value = value
            });

            if (History.Count > 20)
                History.RemoveAt(History.Count - 1);
        }

        public void Reset()
        {
            FireCount = 0;
            LastFiredTime = -1f;
            LastValue = null;
            History.Clear();
        }
    }
}
