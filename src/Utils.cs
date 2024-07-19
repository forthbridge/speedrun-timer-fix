using MoreSlugcats;
using RWCustom;
using System;
using static MoreSlugcats.SpeedRunTimer;

namespace SpeedrunTimerFix;

public static class Utils
{
    // Optional: Shows milliseconds depending on the mod's configuration
    // Conditional: Shows milliseconds if the Remix timer is enabled or speedrun verification is enabled
    public static string GetIGTFormatOptionalMs(this TimeSpan timeSpan) => timeSpan.GetIGTFormat(ModOptions.ShowMilliseconds.Value);
    public static string GetIGTFormatConditionalMs(this TimeSpan timeSpan) => timeSpan.GetIGTFormat((ModManager.MMF && MMF.cfgSpeedrunTimer.Value) || Custom.rainWorld.options.validation);


    public static CampaignTimeTracker? GetCampaignTimeTracker() => (Custom.rainWorld?.processManager?.currentMainLoop as RainWorldGame)?.GetCampaignTimeTracker();
    public static CampaignTimeTracker? GetCampaignTimeTracker(this RainWorldGame? game) => game?.GetStorySession?.saveStateNumber?.GetCampaignTimeTracker();
    public static CampaignTimeTracker? GetCampaignTimeTracker(this SlugcatStats.Name? slugcat) => SpeedRunTimer.GetCampaignTimeTracker(slugcat);

    public static RainWorldGame? RainWorldGame => Custom.rainWorld?.processManager?.currentMainLoop as RainWorldGame;
}
