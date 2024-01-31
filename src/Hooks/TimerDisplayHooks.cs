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
        float lastPosX = self.pos.x;
        float lastFade = self.fade;

        if (ModOptions.PreventTimerFading.Value)
        {
            self.fade = 0.0f;
        }

        orig(self);


        var tracker = self.ThePlayer().abstractCreature.world.game.GetCampaignTimeTracker();

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
    private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
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

        var newTimerText = $" ({Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan)})";

        var oldTimerTimeSpan = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
        var oldTimerText = $" ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";


        self.regionLabel.text = self.regionLabel.text.Replace(oldTimerText, newTimerText);

        if (ModOptions.ShowOldTimer.Value)
        {
            self.regionLabel.text += $" - OLD{oldTimerText}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.regionLabel.text += $" - FIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
        }


        if (ModOptions.ShowCompletedAndLost.Value)
        {
            self.regionLabel.text += $"\n(Completed: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.CompletedFreeTime))} - Lost: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.LostFreeTime))}";
        
            if (tracker.UndeterminedFreeTime != 0.0f)
            {
                self.regionLabel.text += $" - Undetermined: {Utils.GetIGTFormattedTime(TimeSpan.FromMilliseconds(tracker.UndeterminedFreeTime))})";
            }

            self.regionLabel.text += ")";
        }
    }

    
    // Replace the timer on the validation label
    private static void ProcessManager_CreateValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
    {
        orig(self);

        var slugcat = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;
        var saveGameData = Menu.SlugcatSelectMenu.MineForSaveData(self, slugcat);

        if (saveGameData == null) return;


        var oldTimerTimeSpan = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
        var oldTimerText = $" ({SpeedRunTimer.TimeFormat(oldTimerTimeSpan)})";

        var tracker = (self.currentMainLoop as RainWorldGame).GetCampaignTimeTracker();

        if (tracker == null) return;


        var newTimerText = $" ({Utils.GetIGTFormattedTime(tracker.TotalFreeTimeSpan)})";

        self.validationLabel.text = self.validationLabel.text.Replace(oldTimerText, newTimerText);


        if (ModOptions.ShowOldTimer.Value)
        {
            self.validationLabel.text += $" - OLD{oldTimerText}";
        }

        if (ModOptions.ShowFixedUpdateTimer.Value)
        {
            self.validationLabel.text += $" - FIXED ({Utils.GetIGTFormattedTime(tracker.TotalFixedTimeSpan)})";
        }
    }
}