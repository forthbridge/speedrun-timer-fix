using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using MonoMod.Cil;
using System.Runtime.CompilerServices;

namespace SpeedrunTimerFix
{
    internal static partial class Hooks
    {
        public static void ApplyHooks()
        {
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

            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            On.PlayerProgression.WipeAll += PlayerProgression_WipeAll;



            On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;
            On.RainWorldGame.Update += RainWorldGame_Update;
        }



        private static bool isInit = false;

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            try
            {
                if (isInit) return;
                isInit = true;


                MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Options.instance);

                LoadTrackersFromDisk();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
            finally
            {
                orig(self);
            }
        }




        private const int FIXED_FRAMERATE = 40;
        
        // Time Tick is the traditional way of handling incrementing the timer, the built-in timer uses this
        private static void StoryGameSession_TimeTick(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
        {
            orig(self, dt);

            if (Options.fixedUpdateTimer.Value) return;



            if (RainWorld.lockGameTimer) return;

            SaveTimeTracker? tracker = GetTrackerFromProgression(self.game.rainWorld.progression);
            if (tracker == null) return;

            if (self.game.cameras[0].hud == null) return;



            if (self.game.cameras[0].hud.textPrompt.gameOverMode)
            {
                if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.game.AlivePlayers.Count > 0))
                    tracker.undeterminedTime += dt * 1000;

            }
            else if (!self.game.cameras[0].voidSeaMode)
                tracker.undeterminedTime += dt * 1000;
        }

        // RainWorldGame.Update handles the time in a fixed physics step. This reduces the precision, but will account for lag, so is preferred
        private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);

            if (!Options.fixedUpdateTimer.Value) return;



            if (self.GamePaused || !self.IsStorySession) return;
            
            if (ModManager.MSC && (self.rainWorld.safariMode || self.manager.artificerDreamNumber != -1)) return;

            if (RainWorld.lockGameTimer) return;



            SaveTimeTracker? tracker = GetTrackerFromProgression(self.rainWorld.progression);
            if (tracker == null) return;

            if (self.cameras[0].hud == null) return;



            const float dt = 1.0f / FIXED_FRAMERATE;

            if (self.cameras[0].hud.textPrompt.gameOverMode)
            {
                if (!self.Players[0].state.dead || (ModManager.CoopAvailable && self.AlivePlayers.Count > 0))
                    tracker.undeterminedTime += dt * 1000;

            }
            else if (!self.cameras[0].voidSeaMode)
                tracker.undeterminedTime += dt * 1000;
        }



        private static void ProcessManager_CreateValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
        {
            orig(self);

            SlugcatStats.Name slugcat = self.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;
            Menu.SlugcatSelectMenu.SaveGameData saveGameData = Menu.SlugcatSelectMenu.MineForSaveData(self, slugcat);
            if (saveGameData == null) return;

            TimeSpan timeSpan = TimeSpan.FromSeconds(saveGameData.gameTimeAlive + saveGameData.gameTimeDead);
            string textToReplace = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";


            SaveTimeTracker tracker = GetTracker(self.rainWorld.options.saveSlot, slugcat.value);
            string textToAdd = " (" + tracker.GetFormattedTime(tracker.TotalTimeSpan) + ")";

            self.validationLabel.text = self.validationLabel.text.Replace(textToReplace, textToAdd);
        }





        // Reset tracker timings when the relevant saves are wiped
        private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
        {
            orig(self, saveStateNumber);

            SaveTimeTracker tracker = GetTracker(self.rainWorld.options.saveSlot, saveStateNumber.value);
            tracker.WipeTimes();

            SaveTrackersToDisk();
        }

        private static void PlayerProgression_WipeAll(On.PlayerProgression.orig_WipeAll orig, PlayerProgression self)
        {
            orig(self);

            foreach (Dictionary<string, SaveTimeTracker> nameTrackerPair in saveTimeTrackers.Values)
                foreach (SaveTimeTracker tracker in nameTrackerPair.Values)
                    tracker.WipeTimes();

            SaveTrackersToDisk();
        }





        // Replace in-game display
        private static void SpeedRunTimer_Draw(On.MoreSlugcats.SpeedRunTimer.orig_Draw orig, MoreSlugcats.SpeedRunTimer self, float timeStacker)
        {
            orig(self, timeStacker);

            // Stops the timer jittering around due to the rapid text changes associated with displaying milliseconds
            if (Options.includeMilliseconds.Value || !Options.formatTimers.Value)
                self.timeLabel.alignment = FLabelAlignment.Left;
        }

        private static void SpeedRunTimer_Update(On.MoreSlugcats.SpeedRunTimer.orig_Update orig, MoreSlugcats.SpeedRunTimer self)
        {
            // Last fade is a hack to get the timer to display in the fully faded position whilst being fully visible
            float lastPosX = self.pos.x;
            float lastFade = self.fade;

            if (Options.dontFade.Value)
                self.fade = 0.0f;


            orig(self);


            SaveTimeTracker? tracker = GetTrackerFromProgression(self.hud.rainWorld.progression);
            if (tracker == null) return;


            self.timeLabel.text = tracker.GetFormattedTime(tracker.TotalTimeSpan);


            if (Options.includeMilliseconds.Value || !Options.formatTimers.Value)
            {
                self.lastPos.x = lastPosX;
                self.pos.x = (int)(self.hud.rainWorld.options.ScreenSize.x / 2.0f) + 0.2f - (Options.formatTimers.Value ? 100.0f : 35.0f); 
            }

            if (Options.dontFade.Value)
            {
                self.lastFade = lastFade;
                self.fade = 1.0f;
            }

            self.timeLabel.color = Options.timerColor.Value;
        }





        // Save Tracker
        private static Dictionary<int, Dictionary<string, SaveTimeTracker>> saveTimeTrackers = new();
        
        private class SaveTimeTracker
        {
            public TimeSpan TotalTimeSpan => TimeSpan.FromMilliseconds(TotalTime);
            public double TotalTime => completedTime + deathTime + undeterminedTime;


            public double completedTime = 0.0f;
            public double deathTime = 0.0f;

            public double undeterminedTime = 0.0f;


            public string GetFormattedTime(TimeSpan timeSpan)
            {
                if (!Options.formatTimers.Value)
                    return ((Options.formatTimers.Value ? timeSpan.TotalSeconds : (int)(timeSpan.TotalSeconds) * FIXED_FRAMERATE)).ToString().PadLeft(7, '0');



                string formattedTime = string.Format("{0:D3}h:{1:D2}m:{2:D2}s", new object[3]
                {
                    timeSpan.Hours + (timeSpan.Days * 24),
                    timeSpan.Minutes,
                    timeSpan.Seconds
                });



                if (!Options.includeMilliseconds.Value) return formattedTime;

                return formattedTime + $":{timeSpan.Milliseconds.ToString().PadLeft(3, '0')}ms";
            }

            public void WipeTimes()
            {
                completedTime = 0.0f;
                deathTime = 0.0f;
                undeterminedTime = 0.0f;
            }
        }
        
        private static SaveTimeTracker GetTracker(int saveSlot, string slugcat)
        {
            if (!saveTimeTrackers.ContainsKey(saveSlot))
                saveTimeTrackers[saveSlot] = new();

            if (!saveTimeTrackers[saveSlot].ContainsKey(slugcat))
                saveTimeTrackers[saveSlot][slugcat] = new SaveTimeTracker();

            return saveTimeTrackers[saveSlot][slugcat];
        }

        private static SaveTimeTracker? GetTrackerFromProgression(PlayerProgression progression)
        {
            SlugcatStats.Name? saveStateNumber = progression.currentSaveState != null ? progression.currentSaveState.saveStateNumber : progression.starvedSaveState?.saveStateNumber;
            
            if (saveStateNumber == null)
            {
                Plugin.Logger.LogError("Unable to get saveStateNumber from player progression!");
                return null;
            }

            return GetTracker(progression.rainWorld.options.saveSlot, saveStateNumber.value);
        }

        
        


        // Save & Load
        private static string SaveTrackerFilePath => Path.Combine(Custom.LegacyRootFolderDirectory(), Plugin.MOD_ID + "_savetimetrackers.json");

        private static void SaveTrackersToDisk()
        {
            Dictionary<int, Dictionary<string, Dictionary<string, double>>> toSave = new();

            foreach (var saveSlot in saveTimeTrackers.Keys)
            {
                toSave[saveSlot] = new();

                foreach (var slugcat in saveTimeTrackers[saveSlot].Keys)
                {
                    toSave[saveSlot][slugcat] = new();
                    var tracker = GetTracker(saveSlot, slugcat);


                    toSave[saveSlot][slugcat][nameof(tracker.completedTime)] = tracker.completedTime;
                    toSave[saveSlot][slugcat][nameof(tracker.deathTime)] = tracker.deathTime;
                }
            }

            File.WriteAllText(SaveTrackerFilePath, JsonConvert.SerializeObject(toSave, Formatting.Indented));
        }

        private static void LoadTrackersFromDisk()
        {
            if (!File.Exists(SaveTrackerFilePath)) return;

            string text = File.ReadAllText(SaveTrackerFilePath);

            try
            {
                var toLoad = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, Dictionary<string, double>>>>(text);
                if (toLoad == null) return;

                foreach (var saveSlot in toLoad.Keys)
                {
                    foreach (var slugcat in toLoad[saveSlot].Keys)
                    {
                        var tracker = GetTracker(saveSlot, slugcat);

                        tracker.completedTime = toLoad[saveSlot][slugcat][nameof(tracker.completedTime)];
                        tracker.deathTime = toLoad[saveSlot][slugcat][nameof(tracker.deathTime)];
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error deserializing and or loading saved time trackers:\n" + ex);
            }
        }





        // Attach the tracker when a save is initialized, update the start time when it is loaded
        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            SaveState saveState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            Plugin.Logger.LogWarning($"Tracking save (Slot {self.rainWorld.options.saveSlot} - {saveStateNumber.value})");
            SaveTimeTracker tracker = GetTracker(self.rainWorld.options.saveSlot, saveStateNumber.value);

            // Transfer existing save info to freshly generated time trackers
            if (tracker.TotalTime == 0.0f)
            {
                tracker.completedTime = saveState.totTime * 1000.0f;
                tracker.deathTime = saveState.deathPersistentSaveData.deathTime * 1000.0f;
            }

            return saveState;
        }

        private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
        {
            bool result = orig(self, malnourished);

            SaveState? saveState = self.currentSaveState ?? self.starvedSaveState;
            if (saveState == null) return result;

            SaveTimeTracker tracker = GetTracker(self.rainWorld.options.saveSlot, saveState.saveStateNumber.value);
            
            // Time is only able to be determined if we are not starving
            if (!malnourished)
            {
                tracker.completedTime += tracker.undeterminedTime;
                tracker.undeterminedTime = 0.0f;
            }

            SaveTrackersToDisk();

            return result;
        }





        // We know for sure if a save is dead or alive at this point, convert the time accordingly
        private static void MainMenu_ExitButtonPressed(On.Menu.MainMenu.orig_ExitButtonPressed orig, Menu.MainMenu self)
        {
            ConvertAllUndeterminedToDeathTime();

            orig(self);
        }

        private static void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "RESTART")
                ConvertAllUndeterminedToDeathTime();

            orig(self, sender, message);
        }

        private static void ConvertAllUndeterminedToDeathTime()
        {
            foreach (Dictionary<string, SaveTimeTracker> nameTrackerPair in saveTimeTrackers.Values)
            {
                foreach (SaveTimeTracker tracker in nameTrackerPair.Values)
                {
                    tracker.deathTime += tracker.undeterminedTime;
                    tracker.undeterminedTime = 0.0f;
                }
            }

            SaveTrackersToDisk();
        }

        private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
        {
            orig(self, deathOrGhost);

            SaveTimeTracker? tracker = GetTrackerFromProgression(self.game.rainWorld.progression);
            if (tracker == null) return;


            if (deathOrGhost)
            {
                tracker.deathTime += tracker.undeterminedTime;
                tracker.undeterminedTime = 0.0f;
            }


            SaveTrackersToDisk();
        }
        
        



        // Replace timers on the slugcat select menu
        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;

            PlayerProgression progression = menu.manager.rainWorld.progression;
            SaveTimeTracker tracker = GetTracker(progression.rainWorld.options.saveSlot, slugcatNumber.value);

            if (tracker.TotalTime == 0.0f)
            {
                self.regionLabel.text = self.regionLabel.text.Remove(self.regionLabel.text.Length - 1);
                self.regionLabel.text += "???)\n(Save must be loaded at least once for custom time tracking)";
                return;
            }

            
            TimeSpan timeSpan = TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
            string textToReplace = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";

            string textToAdd = " (" + tracker.GetFormattedTime(tracker.TotalTimeSpan) + ")";
            self.regionLabel.text = self.regionLabel.text.Replace(textToReplace, textToAdd);


            if (Options.extraTimers.Value)
            {
                self.regionLabel.text += $"\n(Completed: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.completedTime))} - Lost: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.deathTime))}";
            
                if (tracker.undeterminedTime > 0.0f)
                    self.regionLabel.text += $" - Undetermined: {tracker.GetFormattedTime(TimeSpan.FromMilliseconds(tracker.undeterminedTime))})";

                self.regionLabel.text += ")";
            }
        }
    }
}