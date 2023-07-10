using System;
using System.Linq;
using UnityEngine;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyInit() => On.RainWorld.OnModsInit += RainWorld_OnModsInit;

    public static bool IsInit { get; set; } = false;
    private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        try
        {
            if (IsInit) return;
            IsInit = true;

            ApplyHooks();


            var mod = ModManager.ActiveMods.FirstOrDefault(mod => mod.id == Plugin.MOD_ID);

            Plugin.MOD_NAME = mod.name;
            Plugin.VERSION = mod.version;
            Plugin.AUTHORS = mod.authors;

            MachineConnector.SetRegisteredOI(Plugin.MOD_ID, ModOptions.Instance);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("OnModsInit:\n" + e.Message);
        }
        finally
        {
            orig(self);
        }
    }

    private static void ApplyHooks()
    {
        ApplySaveDataHooks();

        On.Menu.MainMenu.Update += MainMenu_Update;

        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.RainWorld.Update += RainWorld_Update;

        On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
        On.Menu.SlugcatSelectMenu.Update += SlugcatSelectMenu_Update;

        On.PlayerProgression.SaveWorldStateAndProgression += PlayerProgression_SaveWorldStateAndProgression;
        On.StoryGameSession.AppendTimeOnCycleEnd += StoryGameSession_AppendTimeOnCycleEnd;

        On.MoreSlugcats.SpeedRunTimer.Update += SpeedRunTimer_Update;
        On.MoreSlugcats.SpeedRunTimer.Draw += SpeedRunTimer_Draw;

        On.ProcessManager.CreateValidationLabel += ProcessManager_CreateValidationLabel;

        On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;
        On.RainWorldGame.Update += RainWorldGame_Update;

        On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
    }


    // Public API, easy to read for the autosplitter
    public static TimeSpan SpeedrunTimerFix_CurrentSaveTimeSpan { get; set; } = TimeSpan.Zero;

    public const int FIXED_FRAMERATE = 40;
    public const int LAG_INTENSITY = 10000000;

    public static bool IsFirstLoadFinished { get; set; } = false;
    private static void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, Menu.MainMenu self)
    {
        orig(self);

        if (IsFirstLoadFinished) return;
        IsFirstLoadFinished = true;

        self.manager.rainWorld.GetMiscProgression().ConvertUndeterminedToDeathTime();
    }

    private static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        orig(self);

        if (ModOptions.LagSimulation.Value)
        {
            // Simulate Lag
            if (Input.GetKeyDown(KeyCode.L))
                for (int i = 0; i < LAG_INTENSITY; i++)
                    _ = new Vector3(Mathf.Sin(i), Mathf.Cos(i), Mathf.Tan(i));
        }
    }

    // Time Tick is the traditional way of handling incrementing the timer, the built-in timer uses this
    private static void StoryGameSession_TimeTick(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
    {
        orig(self, dt);

        if (ModOptions.FixedUpdateTimer.Value) return;

        if (RainWorld.lockGameTimer) return;

        var tracker = self.game.GetSaveTimeTracker();
        

        if (self.game.cameras[0].hud == null) return;

        if (self.game.cameras[0].hud.textPrompt.gameOverMode)
        {
            if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.game.AlivePlayers.Count > 0))
            {
                tracker.UndeterminedTime += dt * 1000;
            }
        }
        else if (!self.game.cameras[0].voidSeaMode)
        {
            tracker.UndeterminedTime += dt * 1000;
        }
    }

    // RainWorldGame.Update handles the time in a fixed physics step. This reduces the precision, but will account for lag, so is preferred
    private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);

        var tracker = self.GetSaveTimeTracker();
        SpeedrunTimerFix_CurrentSaveTimeSpan = tracker.TotalTimeSpan;

        if (!ModOptions.FixedUpdateTimer.Value) return;

        if (self.GamePaused || !self.IsStorySession) return;
        
        if (ModManager.MSC && (self.rainWorld.safariMode || self.manager.artificerDreamNumber != -1)) return;

        if (RainWorld.lockGameTimer) return;

        if (self.cameras[0].hud == null) return;

        float dt = 1.0f / (ModOptions.CompensateFixedFramerate.Value ? self.framesPerSecond : FIXED_FRAMERATE);

        if (self.cameras[0].hud.textPrompt.gameOverMode)
        {
            if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.AlivePlayers.Count > 0))
                tracker.UndeterminedTime += dt * 1000;

        }
        else if (!self.cameras[0].voidSeaMode)
            tracker.UndeterminedTime += dt * 1000;
    }


    // Replace in-game display
    private static void SpeedRunTimer_Draw(On.MoreSlugcats.SpeedRunTimer.orig_Draw orig, MoreSlugcats.SpeedRunTimer self, float timeStacker)
    {
        orig(self, timeStacker);

        // Stops the timer jittering around due to the rapid text changes associated with displaying milliseconds
        if (ModOptions.IncludeMilliseconds.Value || !ModOptions.FormatTimers.Value)
            self.timeLabel.alignment = FLabelAlignment.Left;
    }

    private static void SpeedRunTimer_Update(On.MoreSlugcats.SpeedRunTimer.orig_Update orig, MoreSlugcats.SpeedRunTimer self)
    {
        // Last fade is a hack to get the timer to display in the fully faded position whilst being fully visible
        float lastPosX = self.pos.x;
        float lastFade = self.fade;

        if (ModOptions.DontFade.Value)
            self.fade = 0.0f;

        orig(self);

        var tracker = self.hud.rainWorld.GetSaveTimeTracker();

        self.timeLabel.text = tracker.GetFormattedTime(tracker.TotalTimeSpan) + (ModOptions.ShowOriginalTimer.Value ? "\nOLD (" + MoreSlugcats.SpeedRunTimer.TimeFormat(self.timing) + ")" : "");

        if (ModOptions.IncludeMilliseconds.Value || !ModOptions.FormatTimers.Value)
        {
            self.lastPos.x = lastPosX;
            self.pos.x = (int)(self.hud.rainWorld.options.ScreenSize.x / 2.0f) + 0.2f - (ModOptions.FormatTimers.Value ? 95.0f : 35.0f); 
        }

        if (ModOptions.DontFade.Value)
        {
            self.lastFade = lastFade;
            self.fade = 1.0f;
        }

        self.timeLabel.color = ModOptions.TimerColor.Value;
    }


    private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
    {
        var tracker = self.rainWorld.GetSaveTimeTracker();

        // Time is only able to be determined if we are not starving
        if (!malnourished)
        {
            tracker.CompletedTime += tracker.UndeterminedTime;
            tracker.UndeterminedTime = 0.0f;
        }

        return orig(self, malnourished);
    }

    private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
    {
        var tracker = self.game.GetSaveTimeTracker();

        if (deathOrGhost)
        {
            tracker.DeathTime += tracker.UndeterminedTime;
            tracker.UndeterminedTime = 0.0f;
        }
     
        orig(self, deathOrGhost);
    }
    

    // Replace timers on the slugcat select menu
    private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
    {
        orig(self, menu, owner, pageIndex, slugcatNumber);

        if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;

        var timeSpan = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
        var oldTimerText = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";

        var tracker = menu.manager.rainWorld.GetSaveTimeTracker();

        // transfer existing time over
        if (tracker.TotalTime == 0.0f)
        {
            tracker.CompletedTime = self.saveGameData.gameTimeAlive * 1000.0f;
            tracker.DeathTime = self.saveGameData.gameTimeDead * 1000.0f;
        }

        var newTimerText = " (" + tracker.GetFormattedTime(tracker.TotalTimeSpan) + ")";

        self.regionLabel.text = self.regionLabel.text.Replace(oldTimerText, newTimerText) + (ModOptions.ShowOriginalTimer.Value ? " - OLD" + oldTimerText : "");


        if (ModOptions.ExtraTimers.Value)
        {
            self.regionLabel.text += $"\n(Completed: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.CompletedTime))} - Lost: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.DeathTime))}";
        
            if (tracker.UndeterminedTime > 0.0f)
                self.regionLabel.text += $" - Undetermined: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.UndeterminedTime))})";

            self.regionLabel.text += ")";
        }
    }

    private static void SlugcatSelectMenu_Update(On.Menu.SlugcatSelectMenu.orig_Update orig, Menu.SlugcatSelectMenu self)
    {
        orig(self);

        if (self.restartChecked && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.R))
        {
            self.restartChecked = false;
            self.manager.rainWorld.GetSaveTimeTracker().WipeTimes();
            Plugin.Logger.LogWarning("WIPED TIME FOR SAVE FILE: " + self.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat);
            
            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlugcatSelect);
        }
    }


    // Reset tracker timings when the relevant saves are wiped
    private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
    {
        orig(self, saveStateNumber);

        var tracker = self.rainWorld.GetSaveTimeTracker();
        tracker.WipeTimes();

        Plugin.Logger.LogWarning("WIPED SAVE TIMES - " + saveStateNumber.value);
    }
    
    private static void ProcessManager_CreateValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
    {
        orig(self);

        var slugcat = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;

        var saveGameData = Menu.SlugcatSelectMenu.MineForSaveData(self, slugcat);
        if (saveGameData == null) return;

        var timeSpan = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
        var oldTimerText = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";

        var tracker = self.rainWorld.GetSaveTimeTracker();
        var newTimerText = " (" + tracker.GetFormattedTime(tracker.TotalTimeSpan) + ")";

        self.validationLabel.text = self.validationLabel.text.Replace(oldTimerText, newTimerText + (ModOptions.ShowOriginalTimer.Value ? " - OLD" + oldTimerText : ""));
    }
}