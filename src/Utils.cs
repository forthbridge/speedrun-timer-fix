using MoreSlugcats;
using RWCustom;
using System;

namespace SpeedrunTimerFix;

public static class Utils
{
    // Takes in TimeSpan and returns a string formatted to display as the IGT
    public static string GetIGTFormat(this TimeSpan timeSpan, bool includeMilliseconds)
    {
        string formattedTime = string.Format("{0:D3}h:{1:D2}m:{2:D2}s", new object[3]
        {
            timeSpan.Hours + (timeSpan.Days * 24),
            timeSpan.Minutes,
            timeSpan.Seconds
        });

        if (!includeMilliseconds)
        {
            return formattedTime;
        }

        return formattedTime + $":{timeSpan.Milliseconds:000}ms";
    }


    // Optional: Shows milliseconds depending on the mod's configuration
    // Conditional: Shows milliseconds if the Remix timer is enabled or speedrun verification is enabled
    public static string GetIGTFormatOptionalMs(this TimeSpan timeSpan) => GetIGTFormat(timeSpan, ModOptions.ShowMilliseconds.Value);
    public static string GetIGTFormatConditionalMs(this TimeSpan timeSpan) => GetIGTFormat(timeSpan, (ModManager.MMF && MMF.cfgSpeedrunTimer.Value) || Custom.rainWorld.options.validation);


    public static CampaignTimerSaveData? GetCampaignTimeTracker(this RainWorldGame? game) => game?.GetStorySession?.saveStateNumber?.GetCampaignTimeTracker();
    public static CampaignTimerSaveData? GetCampaignTimeTracker(this SlugcatStats.Name? slugcat)
    {
        if (slugcat == null) return null;

        var save = GetMiscProgression();

        if (save == null) return null;


        if (!save.CampaignTimers.TryGetValue(slugcat.value, out var tracker))
        {
            save.CampaignTimers.Add(slugcat.value, tracker = new());
        }

        return tracker;
    }


    public static MiscProgressionSaveData? GetMiscProgression() => Custom.rainWorld?.progression?.miscProgressionData?.GetMiscProgression();
    public static MiscProgressionSaveData GetMiscProgression(this PlayerProgression.MiscProgressionData data)
    {
        if (!data.GetSaveDataHandler().TryGet(Plugin.MOD_ID, out MiscProgressionSaveData save))
        {
            data.GetSaveDataHandler().Set(Plugin.MOD_ID, save = new());
        }

        return save;
    }


    // A globally accessible static variable constantly updated to the current timer, which is easy to read should external programs need to read the IGT
    public static TimeSpan SpeedrunTimerFix_CurrentFreeTimeSpan = TimeSpan.Zero;
}
