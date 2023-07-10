
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public class SaveMiscProgression
    {
        public Dictionary<SlugcatStats.Name, SaveTimeTracker> CampaignSaveTimeTrackers { get; } = new();

        public void ConvertUndeterminedToDeathTime()
        {
            foreach (var campaign in CampaignSaveTimeTrackers)
            {
                var tracker = campaign.Value;

                tracker.DeathTime += tracker.UndeterminedTime;
                tracker.UndeterminedTime = 0.0f;
            }
        }
    }

    public class SaveTimeTracker
    {
        public double CompletedTime { get; set; }
        public double DeathTime { get; set; }
        public double UndeterminedTime { get; set; }

        public TimeSpan TotalTimeSpan => TimeSpan.FromMilliseconds(TotalTime);
        public double TotalTime => CompletedTime + DeathTime + UndeterminedTime;

        public string GetFormattedTime(TimeSpan timeSpan)
        {
            if (!ModOptions.formatTimers.Value)
                return ModOptions.fixedUpdateTimer.Value ? ((int)(timeSpan.TotalSeconds * FIXED_FRAMERATE)).ToString("0000000") : (timeSpan.TotalSeconds * FIXED_FRAMERATE).ToString("0000000.00");

            string formattedTime = string.Format("{0:D3}h:{1:D2}m:{2:D2}s", new object[3]
            {
                timeSpan.Hours + (timeSpan.Days * 24),
                timeSpan.Minutes,
                timeSpan.Seconds
            });

            if (!ModOptions.includeMilliseconds.Value)
                return formattedTime;

            return formattedTime + $":{timeSpan.Milliseconds:000}ms";
        }

        public void WipeTimes()
        {
            CompletedTime = 0.0f;
            DeathTime = 0.0f;
            UndeterminedTime = 0.0f;
        }
    }


    public static SaveTimeTracker GetSaveTimeTracker(this RainWorldGame game) => game.rainWorld.GetSaveTimeTracker(game.GetStorySession.saveStateNumber);
    public static SaveTimeTracker GetSaveTimeTracker(this RainWorld rainWorld) => rainWorld.GetSaveTimeTracker(rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat);
    public static SaveTimeTracker GetSaveTimeTracker(this RainWorld rainWorld, SlugcatStats.Name slugcat)
    {
        var save = rainWorld.GetMiscProgression();

        if (!save.CampaignSaveTimeTrackers.TryGetValue(slugcat, out var tracker))
            save.CampaignSaveTimeTrackers.Add(slugcat, tracker = new());

        return tracker;
    }

    public static SaveMiscProgression GetMiscProgression(this RainWorld rainWorld) => GetMiscProgression(rainWorld.progression.miscProgressionData);
    public static SaveMiscProgression GetMiscProgression(this RainWorldGame game) => GetMiscProgression(game.GetStorySession.saveState.progression.miscProgressionData);
    public static SaveMiscProgression GetMiscProgression(this PlayerProgression.MiscProgressionData data)
    {
        if (!data.GetSaveData().TryGet(Plugin.MOD_ID, out SaveMiscProgression save))
            data.GetSaveData().Set(Plugin.MOD_ID, save = new());

        return save;
    }

    private static void SaveCustomData(this PlayerProgression.MiscProgressionData self) => self.GetSaveData().SaveToStrings(self.unrecognizedSaveStrings);


    // Following is adapted from SlugBase, code by Vigaro
    private static void ApplySaveDataHooks() => On.PlayerProgression.MiscProgressionData.ToString += MiscProgressionData_ToString;

    private static string MiscProgressionData_ToString(On.PlayerProgression.MiscProgressionData.orig_ToString orig, PlayerProgression.MiscProgressionData self)
    {
        self.SaveCustomData();
        return orig(self);
    }

    public static SaveData GetSaveData(this PlayerProgression.MiscProgressionData data) => SaveData.ProgressionData.GetValue(data, mwsd => new(mwsd.unrecognizedSaveStrings));

    public class SaveData
    {
        internal const string SAVE_DATA_PREFIX = "_SpeedrunTimerFixSaveData_";

        internal static readonly ConditionalWeakTable<PlayerProgression.MiscProgressionData, SaveData> ProgressionData = new();
        
        private readonly Dictionary<string, object> data;
        private readonly List<string> _unrecognizedSaveStrings;

        internal SaveData(List<string> unrecognizedSaveStrings)
        {
            data = new Dictionary<string, object>();
            _unrecognizedSaveStrings = unrecognizedSaveStrings;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (data.TryGetValue(key, out var obj) && obj is T castObj)
            {
                value = castObj;
                return true;
            }

            if (LoadStringFromUnrecognizedStrings(key, out var stringValue))
            {
                value = JsonConvert.DeserializeObject<T>(stringValue)!;
                data[key] = value!;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value) => data[key] = value!;

        internal void SaveToStrings(List<string> strings)
        {
            foreach (var pair in data)
                SavePairToStrings(strings, pair.Key, JsonConvert.SerializeObject(pair.Value));
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

/*
    MIT License

    Copyright(c) [year]
    [fullname]

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/