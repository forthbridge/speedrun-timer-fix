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


        self.timeLabel.text = tracker.TotalFreeTimeSpan.GetIGTFormatOptionalMs();

        var additionalTimersShown = 0;


        if (ModOptions.ShowOldTimer.Value)
        {
            var game = Utils.RainWorldGame;

            if (game != null && game.IsStorySession)
            {
                var oldTiming = TimeSpan.FromSeconds(
                    game.GetStorySession.saveState.totTime +
                    game.GetStorySession.saveState.deathPersistentSaveData.deathTime +
                    game.GetStorySession.playerSessionRecords[0].time / 40.0f +
                    game.GetStorySession.playerSessionRecords[0].playerGrabbedTime / 40.0f);
                
                self.timeLabel.text += $"\nOLD ({SpeedRunTimer.TimeFormat(oldTiming)})";

                additionalTimersShown++;
            }
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.timeLabel.text += $"\nLAG ({tracker.TotalFixedTimeSpan.GetIGTFormatOptionalMs()})";

            additionalTimersShown++;
        }

        if (ModOptions.ShowTotTime.Value)
        {
            var game = Utils.RainWorldGame;

            if (game != null && game.IsStorySession)
            {
                var totTime = game.GetStorySession.saveState.totTime;

                var totTimeSpan = TimeSpan.FromSeconds(totTime);

                self.timeLabel.text += $"\nTOT ({SpeedRunTimer.TimeFormat(totTimeSpan)})";
            }

            additionalTimersShown++;
        }


        if (!ModOptions.ShowMilliseconds.Value)
        {
            self.lastPos.x = lastPosX;
            self.pos.x += 30.0f;
        }

        if (ModOptions.PreventTimerFading.Value)
        {
            self.lastFade = lastFade;
            self.fade = 1.0f;
        }

        self.timeLabel.color = ModOptions.TimerColor.Value;


        var screenSize = self.hud.rainWorld.options.ScreenSize;
        var additionalTimerOffset = -15.0f;

        switch (ModOptions.TimerPosition.Value)
        {
            case "Top (Default)":
                break;

            case "Top Left":
                self.pos.x += -(screenSize.x / 2.0f) + 1252.0f;
                break;

            case "Top Right":
                self.pos.x += (screenSize.x / 2.0f) - 125.0f;
                break;

            case "Bottom Left":
                additionalTimerOffset = 15.0f;

                self.pos.x += -(screenSize.x / 2.0f) + 125.0f;

                self.pos.y += -screenSize.y + 75.0f;
                break;

            case "Bottom Right":
                additionalTimerOffset = 15.0f;

                self.pos.x += (screenSize.x / 2.0f) - 125.0f;

                self.pos.y += -screenSize.y + 75.0f;
                break;

            case "Bottom":
                additionalTimerOffset = 15.0f;

                self.pos.y += -screenSize.y + 75.0f;
                break;
        }

        if (additionalTimersShown > 1)
        {
            self.pos.y += additionalTimerOffset * (additionalTimersShown - 1);
        }

        if (ModOptions.TimerPosition.Value == "Top (Default)" && additionalTimersShown > 0 && Utils.RainWorldGame?.devToolsActive == true)
        {
            self.pos.y += additionalTimerOffset;
        }

        if (ModOptions.TimerPosition.Value == "Bottom Left" && self.hud.karmaMeter.fade > 0.0f)
        {
            self.pos.y += 65.0f;
        }
    }


    // Replace timers on the slugcat select menu
    private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
    {
        orig(self, menu, owner, pageIndex, slugcatNumber);

        
        if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;


        var tracker = slugcatNumber.GetCampaignTimeTracker();

        if (tracker == null) return;


        var existingTimerFormatted = tracker.TotalFreeTimeSpan.GetIGTFormat(true);
        var existingTimerText = $" ({existingTimerFormatted})";

        var newTimerText = $" ({tracker.TotalFreeTimeSpan.GetIGTFormatConditionalMs()})";


        if (ModOptions.ShowOldTimer.Value)
        {
            var oldTiming = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
            var oldTimerFormatted = $" ({SpeedRunTimer.TimeFormat(oldTiming)})";
            newTimerText += $" - OLD{oldTimerFormatted}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - LAG ({tracker.TotalFixedTimeSpan.GetIGTFormatConditionalMs()})";
        }

        if (ModOptions.ShowTotTime.Value)
        {
            var totTime = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive);
            var totTimeFormatted = $" ({SpeedRunTimer.TimeFormat(totTime)})";
            newTimerText += $" - TOT{totTimeFormatted}";
        }


        if (ModOptions.ShowCompletedAndLost.Value)
        {
            newTimerText += $"\n(Completed: {TimeSpan.FromMilliseconds(tracker.CompletedFreeTime).GetIGTFormatConditionalMs()} - Lost: {TimeSpan.FromMilliseconds(tracker.LostFreeTime).GetIGTFormatConditionalMs()}";
        
            if (tracker.UndeterminedFreeTime != 0.0f)
            {
                newTimerText += $" - Undetermined: {TimeSpan.FromMilliseconds(tracker.UndeterminedFreeTime).GetIGTFormatConditionalMs()}";
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


        var existingTimerFormatted = tracker.TotalFreeTimeSpan.GetIGTFormat(true);
        var existingTimerText = $" ({existingTimerFormatted})";

        var newTimerText = $" ({tracker.TotalFreeTimeSpan.GetIGTFormatConditionalMs()})";


        if (ModOptions.ShowOldTimer.Value)
        {
            var oldTiming = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
            var oldTimerFormatted = $" ({SpeedRunTimer.TimeFormat(oldTiming)})";
            newTimerText += $" - OLD{oldTimerFormatted}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - LAG ({tracker.TotalFixedTimeSpan.GetIGTFormatConditionalMs()})";
        }

        if (ModOptions.ShowTotTime.Value)
        {
            var totTime = TimeSpan.FromSeconds(saveGameData.gameTimeAlive);
            var totTimeFormatted = $" ({SpeedRunTimer.TimeFormat(totTime)})";
            newTimerText += $" - TOT{totTimeFormatted}";
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


        var speedrunTimerText = tracker.TotalFreeTimeSpan.GetIGTFormatConditionalMs();

        var additionalTimersShown = 0;


        if (ModOptions.ShowOldTimer.Value)
        {
            if (package?.saveState != null)
            {
                var oldTimeAlive = package.saveState.totTime;
                var oldTimeLost = package.saveState.deathPersistentSaveData.deathTime;

                var oldTimerTimeSpan = TimeSpan.FromSeconds(oldTimeAlive + oldTimeLost);

                speedrunTimerText += $"\nOLD ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";

                additionalTimersShown++;
            }
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            speedrunTimerText += $"\nLAG ({tracker.TotalFixedTimeSpan.GetIGTFormatConditionalMs()})";

            additionalTimersShown++;
        }

        if (ModOptions.ShowTotTime.Value)
        {
            if (package?.saveState != null)
            {
                var totTime = package.saveState.totTime;

                var totTimeSpan = TimeSpan.FromSeconds(totTime);

                speedrunTimerText += $"\nTOT ({SpeedRunTimer.TimeFormat(totTimeSpan)})";

                additionalTimersShown++;
            }
        }

        var yOffset = additionalTimersShown > 1 ? -15.0f * (additionalTimersShown - 1) : 0.0f;

        var timerPos = new Vector2(0.0f, 700.0f + yOffset);
        var timerSize = new Vector2(1366.0f, 20.0f);

        var speedrunTimer = new MenuLabel(self, self.pages[0], speedrunTimerText, timerPos, timerSize, true);

        self.pages[0].subObjects.Add(speedrunTimer);
    }
}
