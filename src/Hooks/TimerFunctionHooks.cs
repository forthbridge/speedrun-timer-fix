using MoreSlugcats;
using System;
using UnityEngine;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyTimerFunctionHooks()
    {
        On.Menu.SlugcatSelectMenu.Update += SlugcatSelectMenu_Update;
    }

    // Allow a manual trigger of the new tracker to fallback to the old timer from the slugcat select menu, if SHIFT + R is pressed while the restart checkbox is checked
    private static void SlugcatSelectMenu_Update(On.Menu.SlugcatSelectMenu.orig_Update orig, Menu.SlugcatSelectMenu self)
    {
        orig(self);

        if (self.restartChecked && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.R))
        {
            self.restartChecked = false;
            
            var slugcatPage = self.slugcatPages[self.slugcatPageIndex];
            var tracker = SpeedRunTimer.GetCampaignTimeTracker(slugcatPage.slugcatNumber);

            if (tracker == null) return;

            tracker.WipeTimes();

            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlugcatSelect);
        }
        else if (self.restartChecked && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.F))
        {
            self.restartChecked = false;

            var slugcatPage = self.slugcatPages[self.slugcatPageIndex];
            var tracker = SpeedRunTimer.GetCampaignTimeTracker(slugcatPage.slugcatNumber);

            if (tracker == null) return;

            tracker.CompletedFixedTime = tracker.CompletedFreeTime;
            tracker.LostFixedTime = tracker.LostFreeTime;
            tracker.UndeterminedFixedTime = tracker.UndeterminedFreeTime;

            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlugcatSelect);
        }
    }
}