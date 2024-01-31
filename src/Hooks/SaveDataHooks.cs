using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System;

namespace SpeedrunTimerFix;

// Following is adapted from SlugBase, code by Vigaro used with permission
// In theory the data this mod stores should be able to be integrated into the built-in save system, so this system is not required
public static partial class Hooks
{
    private static void ApplySaveDataHooks()
    {
        On.PlayerProgression.MiscProgressionData.ToString += MiscProgressionData_ToString;
    }


    private static string MiscProgressionData_ToString(On.PlayerProgression.MiscProgressionData.orig_ToString orig, PlayerProgression.MiscProgressionData self)
    {
        self.GetSaveDataHandler().SaveToStrings(self.unrecognizedSaveStrings);

        return orig(self);
    }


    public static SaveDataHandler GetSaveDataHandler(this PlayerProgression.MiscProgressionData data)
    {
        return SaveDataHandler.ProgressionData.GetValue(data, mwsd => new(mwsd.unrecognizedSaveStrings));
    }

    public class SaveDataHandler
    {
        internal const string SAVE_DATA_PREFIX = "_SpeedrunTimerFixSaveData_";

        internal static readonly ConditionalWeakTable<PlayerProgression.MiscProgressionData, SaveDataHandler> ProgressionData = new();

        private readonly Dictionary<string, object> _data;
        private readonly List<string> _unrecognizedSaveStrings;

        internal SaveDataHandler(List<string> unrecognizedSaveStrings)
        {
            _data = new Dictionary<string, object>();
            _unrecognizedSaveStrings = unrecognizedSaveStrings;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T castObj)
            {
                value = castObj;
                return true;
            }

            if (LoadStringFromUnrecognizedStrings(key, out var stringValue))
            {
                value = JsonConvert.DeserializeObject<T>(stringValue)!;
                _data[key] = value;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            _data[key] = value!;
        }

        internal void SaveToStrings(List<string> strings)
        {
            foreach (var pair in _data)
            {
                SavePairToStrings(strings, pair.Key, JsonConvert.SerializeObject(pair.Value));
            }
        }

        private static void SavePairToStrings(List<string> strings, string key, string value)
        {
            var prefix = key + SAVE_DATA_PREFIX;
            var dataToStore = prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

            for (var i = 0; i < strings.Count; i++)
            {
                if (strings[i].StartsWith(prefix))
                {
                    strings[i] = dataToStore;
                    return;
                }
            }

            strings.Add(dataToStore);
        }

        internal bool LoadStringFromUnrecognizedStrings(string key, out string value)
        {
            var prefix = key + SAVE_DATA_PREFIX;

            foreach (var s in _unrecognizedSaveStrings)
            {
                if (s.StartsWith(prefix))
                {
                    value = Encoding.UTF8.GetString(Convert.FromBase64String(s.Substring(prefix.Length)));
                    return true;
                }
            }

            value = default!;
            return false;
        }
    }
}