using Menu;
using MoreSlugcats;
using System;
using UnityEngine;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyTimerDisplayHooks()
    {
        On.MoreSlugcats.SpeedRunTimer.Update += SpeedRunTimer_Update;
        On.MoreSlugcats.SpeedRunTimer.Draw += SpeedRunTimer_Draw;
        
        On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;

        On.ProcessManager.CreateValidationLabel += ProcessManager_CreateValidationLabel;

        On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;
    }


    // Replace in-game display
    private static void SpeedRunTimer_Draw(On.MoreSlugcats.SpeedRunTimer.orig_Draw orig, SpeedRunTimer self, float timeStacker)
    {
        orig(self, timeStacker);

        // Stops the timer jittering around due to the rapid text changes associated with displaying milliseconds
        if (ModOptions.ShowMilliseconds.Value)
        {
            self.timeLabel.alignment = FLabelAlignment.Left;
        }
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


        var tracker = self.ThePlayer()?.abstractCreature?.world?.game?.GetCampaignTimeTracker();

        if (tracker == null) return;


        self.timeLabel.text = Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan);


        if (ModOptions.ShowOldTimer.Value)
        {
            self.timeLabel.text += $"\nOLD ({SpeedRunTimer.TimeFormat(self.timing)})";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.timeLabel.text += $"\nFIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
        }


        if (ModOptions.ShowMilliseconds.Value)
        {
            self.lastPos.x = lastPosX;
            self.pos.x -= 95.0f; 
        }


        if (ModOptions.ShowOldTimer.Value && ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.pos.y -= 15.0f;
        }

        if ((ModOptions.ShowOldTimer.Value || ModOptions.ShowFixedUpdateTimer.Value) && self.ThePlayer().abstractCreature.world.game.devToolsActive)
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

        // If the tracker has a void time, then we should load the old timer into the tracker as fallback
        if (tracker.TotalFreeTime == 0.0f || tracker.TotalFixedTime == 0.0f)
        {
            tracker.LoadOldTimings(self.saveGameData.gameTimeAlive, self.saveGameData.gameTimeDead);
        }


        var oldTimerTimeSpan = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
        var oldTimerText = $" ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";

        var newTimerText = $" ({Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan)})";

        if (ModOptions.ShowOldTimer.Value)
        {
            newTimerText += $" - OLD{oldTimerText}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - FIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
        }

        if (ModOptions.ShowCompletedAndLost.Value)
        {
            newTimerText += $"\n(Completed: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.CompletedFreeTime))} - Lost: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.LostFreeTime))}";
        
            if (tracker.UndeterminedFreeTime != 0.0f)
            {
                newTimerText += $" - Undetermined: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.UndeterminedFreeTime))})";
            }

            newTimerText += ")";
        }

        self.regionLabel.text = self.regionLabel.text.Replace(oldTimerText, newTimerText);
    }

    
    // Replace the timer on the validation label
    private static void ProcessManager_CreateValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
    {
        orig(self);

        var slugcat = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;
        var saveGameData = SlugcatSelectMenu.MineForSaveData(self, slugcat);

        if (saveGameData == null) return;


        var oldTimerTimeSpan = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
        var oldTimerText = $" ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";

        var tracker = slugcat.GetCampaignTimeTracker();

        if (tracker == null) return;


        var newTimerText = $" ({Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan)})";

        if (ModOptions.ShowOldTimer.Value)
        {
            newTimerText += $" - OLD{oldTimerText}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            newTimerText += $" - FIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
        }

        self.validationLabel.text = self.validationLabel.text.Replace(oldTimerText, newTimerText);
    }


    // Optionally add the timer to the sleep & death screen 
    private static void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
    {
        orig(self, package);

        if (!ModManager.MMF || !MMF.cfgSpeedrunTimer.Value) return;

        if (!ModOptions.ShowTimerInSleepScreen.Value) return;


        var tracker = package?.characterStats?.name?.GetCampaignTimeTracker();

        if (tracker == null) return;


        var speedrunTimerText = Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan);

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
            speedrunTimerText += $"\nFIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
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