using System;
using UnityEngine;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyTimerFunctionHooks()
    {
        On.PlayerProgression.LoadProgression += PlayerProgression_LoadProgression;

        On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;
        On.RainWorldGame.Update += RainWorldGame_Update;

        On.PlayerProgression.SaveWorldStateAndProgression += PlayerProgression_SaveWorldStateAndProgression;
        On.StoryGameSession.AppendTimeOnCycleEnd += StoryGameSession_AppendTimeOnCycleEnd;

        On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
        On.ProcessManager.RequestMainProcessSwitch_ProcessID_float += ProcessManager_RequestMainProcessSwitch_ProcessID_float;

        On.Menu.SlugcatSelectMenu.Update += SlugcatSelectMenu_Update;

        On.RainWorld.Update += RainWorld_Update;
    }


    // Convert undetermined time to death time whenever progression is loaded, the only time this should occur is when the game is started after the player quit while starving
    // Disadvantage: this will be called anytime progression is reloaded, such as when the slot is changed - there may be a better way to go about this
    private static void PlayerProgression_LoadProgression(On.PlayerProgression.orig_LoadProgression orig, PlayerProgression self)
    {
        orig(self);

        if (self.loadInProgress) return;

        var miscProg = Utils.GetMiscProgression();

        if (miscProg == null) return;

        miscProg.ConvertUndeterminedToDeathTime();
    }



    // TimeTick is used by the old timer, however the old timer only counts secondns
    private static void StoryGameSession_TimeTick(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
    {
        orig(self, dt);

        // Ensure the timer should be incrementing
        if (RainWorld.lockGameTimer) return;


        // Ensure the tracker can be accessed properly & hud is available
        var tracker = self.game.GetCampaignTimeTracker();

        if (tracker == null) return;

        if (self.game.cameras[0].hud == null) return;


        tracker.UndeterminedFreeTime += GetTimerTickIncrement(self.game, dt);
    }

    // Updating the timer within RainWorldGame.Update means it is within the fixed update loop - it will account for lag, but this may not be desired when glitches intentionally drop frames
    private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);

        // Ensure the timer should be incrementing
        if (RainWorld.lockGameTimer) return;

        if (self.GamePaused) return;

        if (!self.IsStorySession) return;

        if (ModManager.MSC && (self.rainWorld.safariMode || self.manager.artificerDreamNumber != -1)) return;


        // Ensure the tracker can be accessed properly & hud is available
        var tracker = self.GetCampaignTimeTracker();

        if (tracker == null) return;

        if (self.cameras[0].hud == null) return;

        var deltaTime = 1.0 / self.framesPerSecond;

        tracker.UndeterminedFixedTime += GetTimerTickIncrement(self, deltaTime);
    }

    // Returns the amount the timer should be incremented by for a given time tick, based on certain conditions
    private static double GetTimerTickIncrement(RainWorldGame self, double deltaTime)
    {
        var toIncrement = 0.0;

        if (self.cameras[0].hud.textPrompt.gameOverMode)
        {
            if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.AlivePlayers.Count > 0))
            {
                toIncrement += deltaTime * 1000;
            }
        }
        else if (!self.cameras[0].voidSeaMode)
        {
            toIncrement += deltaTime * 1000;
        }

        return toIncrement;
    }

    

    // Add completed time on save, if the player is not starving
    private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
    {
        var tracker = self.PlayingAsSlugcat.GetCampaignTimeTracker();

        if (tracker == null) return orig(self, malnourished);

        // Time is only able to be determined if we are not starving
        if (!malnourished)
        {
            tracker.ConvertUndeterminedToCompletedTime();
        }

        return orig(self, malnourished);
    }
     
    // Add lost time if the player died
    private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
    {
        var tracker = self.game.GetCampaignTimeTracker();

        if (tracker != null)
        {
            if (deathOrGhost)
            {
                tracker.ConvertUndeterminedToLostTime();
            }
        }
     
        orig(self, deathOrGhost);
    }


    // Wipe the tracker when its campaign is wiped
    private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
    {
        orig(self, saveStateNumber);

        var tracker = saveStateNumber.GetCampaignTimeTracker();

        if (tracker == null) return;

        tracker.WipeTimes();
    }

    // This issue only occurs when the main menu is skipped and a campaign is entered immediately (e.g. through the use of start screen: 0 in setup.txt)
    // If the campaign is a new game, the timer will not be wiped and the time from the pre-wipe save will be used
    // So, wipe the tracker when a new game is started (this is based on the NEW GAME validation label logic, I'm not sure this is the best way to do this)
    // We run the risk of incorrectly wiping the times if MineForSaveData doesn't do it's job for whatever reason
    private static void ProcessManager_RequestMainProcessSwitch_ProcessID_float(On.ProcessManager.orig_RequestMainProcessSwitch_ProcessID_float orig, ProcessManager self, ProcessManager.ProcessID ID, float fadeOutSeconds)
    {
        orig(self, ID, fadeOutSeconds);

        if (ID != ProcessManager.ProcessID.Game) return;

        if (self.arenaSitting != null) return;

        if (ModManager.MSC && self.rainWorld.safariMode) return;


        var campaign = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;
        var tracker = campaign.GetCampaignTimeTracker();
        
        if (tracker == null) return;
        

        var saveGameData = Menu.SlugcatSelectMenu.MineForSaveData(self, campaign);

        if (saveGameData == null)
        {
            tracker.WipeTimes();
        }
    }


    // Allow a manual trigger of the new tracker to fallback to the old timer from the slugcat select menu, if SHIFT + R is pressed while the restart checkbox is checked
    private static void SlugcatSelectMenu_Update(On.Menu.SlugcatSelectMenu.orig_Update orig, Menu.SlugcatSelectMenu self)
    {
        orig(self);

        if (self.restartChecked && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.R))
        {
            self.restartChecked = false;
            
            var slugcatPage = self.slugcatPages[self.slugcatPageIndex];
            var tracker = slugcatPage.slugcatNumber.GetCampaignTimeTracker();

            if (tracker == null) return;

            tracker.WipeTimes();

            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlugcatSelect);
        }
    }


    // Update the global timespan
    private static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        orig(self);

        if (self.processManager?.currentMainLoop is RainWorldGame game)
        {
            var tracker = game.StoryCharacter?.GetCampaignTimeTracker();

            if (tracker != null)
            {
                Utils.SpeedrunTimerFix_CurrentFreeTimeSpan = tracker.TotalFreeTimeSpan;
            }
        }
        else
        {
            Utils.SpeedrunTimerFix_CurrentFreeTimeSpan = TimeSpan.Zero;
        }
    }
}