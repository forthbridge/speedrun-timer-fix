using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeedrunTimerFix;

public static partial class Hooks
{
    public static void ApplyInit() => On.RainWorld.OnModsInit += RainWorld_OnModsInit;

    public static bool isInit = false;

    private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        try
        {
            if (isInit) return;
            isInit = true;

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

        On.RainWorld.OnModsInit += RainWorld_OnModsInit;

        On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
        On.StoryGameSession.AppendTimeOnCycleEnd += StoryGameSession_AppendTimeOnCycleEnd;


        On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
        On.PlayerProgression.SaveWorldStateAndProgression += PlayerProgression_SaveWorldStateAndProgression;

        On.Menu.MainMenu.ExitButtonPressed += MainMenu_ExitButtonPressed;
        On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;

        On.MoreSlugcats.SpeedRunTimer.Update += SpeedRunTimer_Update;
        On.MoreSlugcats.SpeedRunTimer.Draw += SpeedRunTimer_Draw;

        On.ProcessManager.CreateValidationLabel += ProcessManager_CreateValidationLabel;

        On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;
        On.RainWorldGame.Update += RainWorldGame_Update;
    }


    public const int FIXED_FRAMERATE = 40;
    
    // Public API, easy to read for the autosplitter
    public static TimeSpan SpeedrunTimerFix_CurrentSaveTimeSpan = TimeSpan.Zero;

    
    // Time Tick is the traditional way of handling incrementing the timer, the built-in timer uses this
    private static void StoryGameSession_TimeTick(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
    {
        orig(self, dt);

        if (ModOptions.fixedUpdateTimer.Value) return;

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

        if (!ModOptions.fixedUpdateTimer.Value) return;

        if (self.GamePaused || !self.IsStorySession) return;
        
        if (ModManager.MSC && (self.rainWorld.safariMode || self.manager.artificerDreamNumber != -1)) return;

        if (RainWorld.lockGameTimer) return;

        var tracker = self.GetSaveTimeTracker();


        if (self.cameras[0].hud == null) return;

        float dt = 1.0f / (ModOptions.compensateFixedFramerate.Value ? self.framesPerSecond : FIXED_FRAMERATE);

        if (self.cameras[0].hud.textPrompt.gameOverMode)
        {
            if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.AlivePlayers.Count > 0))
                tracker.UndeterminedTime += dt * 1000;

        }
        else if (!self.cameras[0].voidSeaMode)
            tracker.UndeterminedTime += dt * 1000;
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

        self.validationLabel.text = self.validationLabel.text.Replace(oldTimerText, newTimerText + (ModOptions.showOriginalTimer.Value ? " - OLD" + oldTimerText : ""));
    }


    // Replace in-game display
    private static void SpeedRunTimer_Draw(On.MoreSlugcats.SpeedRunTimer.orig_Draw orig, MoreSlugcats.SpeedRunTimer self, float timeStacker)
    {
        orig(self, timeStacker);

        // Stops the timer jittering around due to the rapid text changes associated with displaying milliseconds
        if (ModOptions.includeMilliseconds.Value || !ModOptions.formatTimers.Value)
            self.timeLabel.alignment = FLabelAlignment.Left;
    }

    private static void SpeedRunTimer_Update(On.MoreSlugcats.SpeedRunTimer.orig_Update orig, MoreSlugcats.SpeedRunTimer self)
    {
        // Last fade is a hack to get the timer to display in the fully faded position whilst being fully visible
        float lastPosX = self.pos.x;
        float lastFade = self.fade;

        if (ModOptions.dontFade.Value)
            self.fade = 0.0f;

        orig(self);

        var tracker = self.hud.rainWorld.GetSaveTimeTracker();

        self.timeLabel.text = tracker.GetFormattedTime(tracker.TotalTimeSpan) + (ModOptions.showOriginalTimer.Value ? "\nOLD (" + MoreSlugcats.SpeedRunTimer.TimeFormat(self.timing) + ")" : "");

        if (ModOptions.includeMilliseconds.Value || !ModOptions.formatTimers.Value)
        {
            self.lastPos.x = lastPosX;
            self.pos.x = (int)(self.hud.rainWorld.options.ScreenSize.x / 2.0f) + 0.2f - (ModOptions.formatTimers.Value ? 95.0f : 35.0f); 
        }

        if (ModOptions.dontFade.Value)
        {
            self.lastFade = lastFade;
            self.fade = 1.0f;
        }

        self.timeLabel.color = ModOptions.timerColor.Value;
    } 


    // Attach the tracker when a save is initialized, update the start time when it is loaded
    private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
    {
        var saveState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

        Plugin.Logger.LogWarning($"Tracking save (Slot {self.rainWorld.options.saveSlot} - {saveStateNumber.value})");
        var tracker = self.rainWorld.GetSaveTimeTracker();

        // Transfer existing save info to freshly generated time trackers
        if (tracker.TotalTime == 0.0f)
        {
            tracker.CompletedTime = saveState.totTime * 1000.0f;
            tracker.DeathTime = saveState.deathPersistentSaveData.deathTime * 1000.0f;
        }

        return saveState;
    }

    private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
    {
        var result = orig(self, malnourished);

        var saveState = self.currentSaveState ?? self.starvedSaveState;
        
        if (saveState == null)
            return result;

        var tracker = self.rainWorld.GetSaveTimeTracker();
        
        // Time is only able to be determined if we are not starving
        if (!malnourished)
        {
            tracker.CompletedTime += tracker.UndeterminedTime;
            tracker.UndeterminedTime = 0.0f;
        }

        return result;
    }


    // We know for sure if a save is dead or alive at this point, convert the time accordingly
    private static void MainMenu_ExitButtonPressed(On.Menu.MainMenu.orig_ExitButtonPressed orig, Menu.MainMenu self)
    {
        self.manager.rainWorld.GetMiscProgression().ConvertUndeterminedToDeathTime();
        orig(self);
    }

    private static void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
    {
        if (message == "RESTART")
            self.manager.rainWorld.GetMiscProgression().ConvertUndeterminedToDeathTime();

        orig(self, sender, message);
    }

    private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
    {
        orig(self, deathOrGhost);

        var tracker = self.game.GetSaveTimeTracker();
        if (tracker == null) return;

        if (deathOrGhost)
        {
            tracker.DeathTime += tracker.UndeterminedTime;
            tracker.UndeterminedTime = 0.0f;
        }
    }
    
    // Replace timers on the slugcat select menu
    private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
    {
        orig(self, menu, owner, pageIndex, slugcatNumber);

        if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;

        var tracker = menu.manager.rainWorld.GetSaveTimeTracker();

        if (tracker.TotalTime == 0.0f)
        {
            self.regionLabel.text = self.regionLabel.text.Remove(self.regionLabel.text.Length - 1);
            self.regionLabel.text += "???)\n(Save must be loaded at least once for custom time tracking)";
            return;
        }

        
        var timeSpan = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
        string oldTimerText = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";

        string newTimerText = " (" + tracker.GetFormattedTime(tracker.TotalTimeSpan) + ")";
        self.regionLabel.text = self.regionLabel.text.Replace(oldTimerText, newTimerText) + (ModOptions.showOriginalTimer.Value ? " - OLD" + oldTimerText : "");


        if (ModOptions.extraTimers.Value)
        {
            self.regionLabel.text += $"\n(Completed: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.CompletedTime))} - Lost: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.DeathTime))}";
        
            if (tracker.UndeterminedTime > 0.0f)
                self.regionLabel.text += $" - Undetermined: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.UndeterminedTime))})";

            self.regionLabel.text += ")";
        }
    }
}