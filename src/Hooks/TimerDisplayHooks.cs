using Menu;
using MoreSlugcats;
using RWCustom;
using System;
using UnityEngine;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyTimerDisplayHooks()
    {
        On.MoreSlugcats.SpeedRunTimer.Update += SpeedRunTimer_Update;
        
        On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;

        On.ProcessManager.CreateValidationLabel += ProcessManager_CreateValidationLabel;

        On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;
    }


    private static void SpeedRunTimer_Update(On.MoreSlugcats.SpeedRunTimer.orig_Update orig, SpeedRunTimer self)
    {
        // Last fade is a hack to get the timer to display in the fully faded position whilst being fully visible
        var lastPosX = self.pos.x;
        var lastFade = self.fade;

        if (ModOptions.PreventTimerFading.Value)
        {
            self.fade = 0.0f;
        }

        orig(self);


        var tracker = Utils.GetCampaignTimeTracker();

        if (tracker == null) return;


        self.timeLabel.text = Utils.GetIGTFormatOptionalMs(tracker.TotalFreeTimeSpan);


        if (ModOptions.ShowOldTimer.Value)
        {
            var game = Utils.RainWorldGame;

            if (game != null)
            {
                var oldTiming = TimeSpan.FromSeconds(
                    game.GetStorySession.saveState.totTime +
                    game.GetStorySession.saveState.deathPersistentSaveData.deathTime +
                    game.GetStorySession.playerSessionRecords[0].time / 40 +
                    game.GetStorySession.playerSessionRecords[0].playerGrabbedTime / 40);
                
                self.timeLabel.text += $"\nOLD ({SpeedRunTimer.TimeFormat(oldTiming)})";
            }
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.timeLabel.text += $"\nLAG ({Utils.GetIGTFormatOptionalMs(tracker.TotalFixedTimeSpan)})";
        }

        if (!ModOptions.ShowMilliseconds.Value)
        {
            self.lastPos.x = lastPosX;
            self.pos.x += 30.0f;
        }

        if (ModOptions.ShowOldTimer.Value && ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.pos.y -= 15.0f;
        }

        if ((ModOptions.ShowOldTimer.Value || ModOptions.ShowFixedUpdateTimer.Value) && Utils.RainWorldGame?.devToolsActive == true)
        {
            self.pos.y -= 15.0f;
        }


        if (ModOptions.PreventTimerFading.Value)
        {
            self.lastFade = lastFade;
            self.fade = 1.0f;
        }

        self.timeLabel.color = ModOptions.TimerColor.Value;
    }


    // Replace timers on the slugcat select menu
    private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
    {
        orig(self, menu, owner, pageIndex, slugcatNumber);

        
        if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;


        var tracker = slugcatNumber.GetCampaignTimeTracker();

        if (tracker == null) return;

        var existingTimerFormatted = Custom.GetIGTFormat(tracker.TotalFreeTimeSpan, true);
        var existingTimerText = $" ({existingTimerFormatted})";

        var newTimerText = $" ({Utils.GetIGTFormatConditionalMs(tracker.TotalFreeTimeSpan)})";

        if (ModOptions.ShowOldTimer.Value)
        {
            var oldTiming = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
            var oldTimerFormatted = $" ({SpeedRunTimer.TimeFormat(oldTiming)})";
            newTimerText += $" - OLD{oldTimerFormatted}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - LAG ({Utils.GetIGTFormatConditionalMs(tracker.TotalFixedTimeSpan)})";
        }

        if (ModOptions.ShowCompletedAndLost.Value)
        {
            newTimerText += $"\n(Completed: {Utils.GetIGTFormatConditionalMs(TimeSpan.FromMilliseconds(tracker.CompletedFreeTime))} - Lost: {Utils.GetIGTFormatConditionalMs(TimeSpan.FromMilliseconds(tracker.LostFreeTime))}";
        
            if (tracker.UndeterminedFreeTime != 0.0f)
            {
                newTimerText += $" - Undetermined: {Utils.GetIGTFormatConditionalMs(TimeSpan.FromMilliseconds(tracker.UndeterminedFreeTime))}";
            }

            newTimerText += ")";
        }

        self.regionLabel.text = self.regionLabel.text.Replace(existingTimerText, newTimerText);
    }

    
    // Replace the timer on the validation label
    private static void ProcessManager_CreateValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
    {
        orig(self);

        var slugcat = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;
        var saveGameData = SlugcatSelectMenu.MineForSaveData(self, slugcat);

        if (saveGameData == null) return;


        var tracker = slugcat.GetCampaignTimeTracker();

        if (tracker == null) return;

        var existingTimerFormatted = Custom.GetIGTFormat(tracker.TotalFreeTimeSpan, true);
        var existingTimerText = $" ({existingTimerFormatted})";

        var newTimerText = $" ({Utils.GetIGTFormatConditionalMs(tracker.TotalFreeTimeSpan)})";

        if (ModOptions.ShowOldTimer.Value)
        {
            var oldTiming = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
            var oldTimerFormatted = $" ({SpeedRunTimer.TimeFormat(oldTiming)})";
            newTimerText += $" - OLD{oldTimerFormatted}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - LAG ({Utils.GetIGTFormatConditionalMs(tracker.TotalFixedTimeSpan)})";
        }

        self.validationLabel.text = self.validationLabel.text.Replace(existingTimerText, newTimerText);
    }


    // Optionally add the timer to the sleep & death screen 
    private static void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
    {
        orig(self, package);

        if (!ModManager.MMF || !MMF.cfgSpeedrunTimer.Value) return;

        if (!ModOptions.ShowTimerInSleepScreen.Value) return;


        var tracker = package?.characterStats?.name?.GetCampaignTimeTracker();

        if (tracker == null) return;


        var speedrunTimerText = Utils.GetIGTFormatConditionalMs(tracker.TotalFreeTimeSpan);

        if (ModOptions.ShowOldTimer.Value)
        {
            if (package?.saveState != null)
            {
                var oldTimeAlive = package.saveState.totTime;
                var oldTimeLost = package.saveState.deathPersistentSaveData.deathTime;

                var oldTimerTimeSpan = TimeSpan.FromSeconds(oldTimeAlive + oldTimeLost);

                speedrunTimerText += $"\nOLD ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";
            }
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            speedrunTimerText += $"\nLAG ({Utils.GetIGTFormatConditionalMs(tracker.TotalFixedTimeSpan)})";
        }

        var timerPos = new Vector2(0.0f, 700.0f);
        var timerSize = new Vector2(1366.0f, 20.0f);

        var speedrunTimer = new MenuLabel(self, self.pages[0], speedrunTimerText, timerPos, timerSize, true, null);

        self.pages[0].subObjects.Add(speedrunTimer);

        if (ModOptions.ShowOldTimer.Value || ModOptions.ShowFixedUpdateTimer.Value)
        {
            speedrunTimer.pos.y -= 15.0f;
        }

        if (ModOptions.ShowOldTimer.Value && ModOptions.ShowFixedUpdateTimer.Value)
        {
            speedrunTimer.pos.y -= 15.0f;
        }
    }
}