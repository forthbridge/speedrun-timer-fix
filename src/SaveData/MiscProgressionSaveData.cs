using System.Collections.Generic;

namespace SpeedrunTimerFix;

// One of these is attached to each save slot
// The current save system would probably be unecessary if this was implemented into the PlayerProgression.MiscProgressionData class itself
public sealed class MiscProgressionSaveData
{
    // Dictionary of slugcat campaigns : save time data
    public Dictionary<string, CampaignTimerSaveData> CampaignTimers { get; } = new();
    
    // Typically used after the player exits a starve cycle, conversion will happen on death or when the game starts
    public void ConvertUndeterminedToDeathTime()
    {
        foreach (var campaign in CampaignTimers)
        {
            var campaignTimer = campaign.Value;

            campaignTimer.ConvertUndeterminedToLostTime();
        }
    }
}