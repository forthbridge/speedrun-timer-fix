using IL.Menu;
using Kittehface.Framework20;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using UnityEngine;

namespace SpeedrunTimerFix
{
    internal static partial class Hooks
    {
        public static void ApplyHooks() => On.RainWorld.OnModsInit += RainWorld_OnModsInit;

        private static bool isInit = false;

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            try
            {
                if (isInit) return;
                isInit = true;


                MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Options.instance);


                On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
                On.StoryGameSession.AppendTimeOnCycleEnd += StoryGameSession_AppendTimeOnCycleEnd;


                On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
                On.PlayerProgression.SaveWorldStateAndProgression += PlayerProgression_SaveWorldStateAndProgression;

                On.Menu.MainMenu.ExitButtonPressed += MainMenu_ExitButtonPressed;
                On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;

                On.MoreSlugcats.SpeedRunTimer.Update += SpeedRunTimer_Update;
                On.MoreSlugcats.SpeedRunTimer.Draw += SpeedRunTimer_Draw;

                LoadTrackerFromDisk();
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



        private static void SpeedRunTimer_Draw(On.MoreSlugcats.SpeedRunTimer.orig_Draw orig, MoreSlugcats.SpeedRunTimer self, float timeStacker)
        {
            orig(self, timeStacker);

            if (!Options.includeMilliseconds.Value) return;

            // Stops the timer jittering around due to the rapid text changes
            self.timeLabel.alignment = FLabelAlignment.Left;
        }

        private static void SpeedRunTimer_Update(On.MoreSlugcats.SpeedRunTimer.orig_Update orig, MoreSlugcats.SpeedRunTimer self)
        {
            float lastPosX = self.pos.x;
            float lastFade = self.fade;

            if (Options.dontFade.Value)
                self.fade = 0.0f;

            orig(self);


            PlayerProgression progression = self.hud.rainWorld.progression;

            SlugcatStats.Name? saveStateNumber = progression.currentSaveState != null ? progression.currentSaveState.saveStateNumber : progression.starvedSaveState?.saveStateNumber;
            if (saveStateNumber == null) return;

            SaveTracker tracker = GetSaveTracker(progression.rainWorld.options.saveSlot, saveStateNumber.value);


            if (Options.includeMilliseconds.Value)
            {
                if (!RainWorld.lockGameTimer)
                {
                    int totalTime = self.ThePlayer().abstractCreature.world.game.GetStorySession.playerSessionRecords[0].time + self.ThePlayer().abstractCreature.world.game.GetStorySession.playerSessionRecords[0].playerGrabbedTime;

                    tracker.undeterminedMilliseconds = (int)((totalTime % 40 / 40.0f) * 1000.0f);

                    self.timeLabel.text += $":{(tracker.undeterminedMilliseconds).ToString().PadLeft(3, '0')}ms";
                }

                self.lastPos.x = lastPosX;
                self.pos.x = (int)(self.hud.rainWorld.options.ScreenSize.x / 2.0f) + 0.2f - 100.0f;
            }


            if (Options.dontFade.Value)
            {
                self.lastFade = lastFade;
                self.fade = 1.0f;
            }


            self.timeLabel.color = Options.timerColor.Value;
        }



        // Save Tracker
        private static Dictionary<int, Dictionary<string, SaveTracker>> saveTrackers = new();
        
        private class SaveTracker
        {
            public int startTime = 0;

            public int aliveTimeCorrection = 0;
            public int deathTimeCorrection = 0;

            public int undeterminedMilliseconds = 0;

            public int aliveTimeMilliseconds = 0;
            public int deathTimeMilliseconds = 0;
        }
        
        private static SaveTracker GetSaveTracker(int saveSlot, string slugcat)
        {
            if (!saveTrackers.ContainsKey(saveSlot))
                saveTrackers[saveSlot] = new();

            if (!saveTrackers[saveSlot].ContainsKey(slugcat))
                saveTrackers[saveSlot][slugcat] = new SaveTracker();

            return saveTrackers[saveSlot][slugcat];
        }

        

        // Attach the tracker when a save is initialized, update the start time when it is loaded
        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            SaveState saveState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            Plugin.Logger.LogWarning("Tracking save...");
            SaveTracker tracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveStateNumber.value);
            tracker.startTime = saveState.totTime;

            saveState.totTime += tracker.aliveTimeCorrection;
            saveState.deathPersistentSaveData.deathTime += tracker.deathTimeCorrection;

            tracker.aliveTimeCorrection = 0;
            tracker.deathTimeCorrection = 0;

            tracker.undeterminedMilliseconds = tracker.aliveTimeMilliseconds + tracker.deathTimeMilliseconds;

            return saveState;
        }

        // Track starving saves
        private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
        {
            bool result = orig(self, malnourished);

            SaveState? saveState = self.currentSaveState ?? self.starvedSaveState;
            if (saveState == null) return result;

            LoadTrackerFromDisk();
            SaveTracker tracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveState.saveStateNumber.value);

            if (malnourished)
            {
                Plugin.Logger.LogWarning("Saving Starve Cycle!");
                tracker.aliveTimeCorrection = saveState.totTime - tracker.startTime;
            }
            
            SaveTrackerToDisk();

            return result;
        }


        private static string SaveTrackerFilePath => Path.Combine(Custom.LegacyRootFolderDirectory(), Plugin.MOD_ID + "_savetrackers.txt");

        private const char SAVE_SLOT_SEPARATOR = ';';
        private const char NAME_SEPARATOR = '.';
        private const char ARG_SEPARATOR = '.';

        private const char SAVE_SLOT_EQUALITY = '-';
        private const char SLUGCAT_EQUALITY = ':';
        private const char ARG_EQUALITY = '=';


        private static void SaveTrackerToDisk()
        {
            try
            {
                string text = "";

                foreach (int saveSlot in saveTrackers.Keys)
                {
                    text += saveSlot;
                    text += SAVE_SLOT_EQUALITY;

                    foreach (string slugcat in saveTrackers[saveSlot].Keys)
                    {
                        text += slugcat;
                        text += SLUGCAT_EQUALITY;

                        SaveTracker tracker = saveTrackers[saveSlot][slugcat];


                        text += nameof(tracker.aliveTimeCorrection) + ARG_EQUALITY + tracker.aliveTimeCorrection;
                        text += ARG_SEPARATOR;

                        text += nameof(tracker.deathTimeCorrection) + ARG_EQUALITY + tracker.deathTimeCorrection;
                        text += ARG_SEPARATOR;

                        text += nameof(tracker.aliveTimeMilliseconds) + ARG_EQUALITY + tracker.aliveTimeMilliseconds;
                        text += ARG_SEPARATOR;

                        text += nameof(tracker.deathTimeMilliseconds) + ARG_EQUALITY + tracker.deathTimeMilliseconds;


                        text += NAME_SEPARATOR;
                    }

                    text += SAVE_SLOT_SEPARATOR;
                }

                File.WriteAllText(SaveTrackerFilePath, text);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error saving tracker to disk!\n" + ex);
            }
        }

        private static void LoadTrackerFromDisk()
        {
            try
            {
                if (!File.Exists(SaveTrackerFilePath)) return;

                string text = File.ReadAllText(SaveTrackerFilePath);

                string[] saveSlots = text.Split(SAVE_SLOT_SEPARATOR);
            
                foreach (string saveSlotString in saveSlots)
                {
                    string[] saveSlotSlugcatPair = saveSlotString.Split(SAVE_SLOT_EQUALITY);
                    int.TryParse(saveSlotSlugcatPair[0], out int saveSlot);
                
                    string[] slugcats = saveSlotString.Split(NAME_SEPARATOR);

                    foreach (string slugcatString in slugcats)
                    {
                        string[] slugcatTrackerPair = slugcatString.Split(SLUGCAT_EQUALITY);
                        string slugcat = slugcatTrackerPair[0];

                        SaveTracker tracker = GetSaveTracker(saveSlot, slugcat);
                    
                    
                        string[] trackerVariables = slugcatString.Split(ARG_SEPARATOR);

                        foreach (string variable in trackerVariables)
                        {
                            string[] keyValue = variable.Split(ARG_EQUALITY);

                            switch (keyValue[0])
                            {
                                case nameof(tracker.aliveTimeCorrection):
                                    tracker.aliveTimeCorrection = int.Parse(keyValue[1]);
                                    break;

                                case nameof(tracker.deathTimeCorrection):
                                    tracker.deathTimeCorrection = int.Parse(keyValue[1]);
                                    break;

                                case nameof(tracker.aliveTimeMilliseconds):
                                    tracker.aliveTimeMilliseconds = int.Parse(keyValue[1]);
                                    break;

                                case nameof(tracker.deathTimeMilliseconds):
                                    tracker.deathTimeMilliseconds = int.Parse(keyValue[1]);
                                    break;
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error loading tracker from disk!\n" + ex);
            }
        }

        private static void ConvertSurvivedTimeToDeathTime()
        {
            foreach (var nameTrackerPair in saveTrackers.Values)
            {
                foreach (SaveTracker tracker in nameTrackerPair.Values)
                {
                    tracker.deathTimeCorrection = tracker.aliveTimeCorrection;
                    tracker.aliveTimeCorrection = 0;
                }
            }

            SaveTrackerToDisk();
        }



        // Starved saves are considered dead at these points, have them be converted
        private static void MainMenu_ExitButtonPressed(On.Menu.MainMenu.orig_ExitButtonPressed orig, Menu.MainMenu self)
        {
            ConvertSurvivedTimeToDeathTime();

            orig(self);
        }

        private static void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "RESTART")
                ConvertSurvivedTimeToDeathTime();

            orig(self, sender, message);
        }

        private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
        {
            orig(self, deathOrGhost);
            
            PlayerProgression progression = self.game.rainWorld.progression;
            
            SlugcatStats.Name? saveStateNumber = progression.currentSaveState != null ? progression.currentSaveState.saveStateNumber : progression.starvedSaveState?.saveStateNumber;
            if (saveStateNumber == null) return;

            SaveTracker tracker = GetSaveTracker(progression.rainWorld.options.saveSlot, saveStateNumber.value);


            if (deathOrGhost)
            {
                tracker.deathTimeCorrection = tracker.aliveTimeCorrection;
                tracker.aliveTimeCorrection = 0;

                tracker.deathTimeMilliseconds = tracker.undeterminedMilliseconds;
            }
            else
            {
                tracker.aliveTimeMilliseconds = tracker.undeterminedMilliseconds;
            }

            tracker.undeterminedMilliseconds = 0;

             SaveTrackerToDisk();
        }
        
        

        // Adds extra timing information to the slugcat select menu, helps a lot with debugging
        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            PlayerProgression progression = menu.manager.rainWorld.progression;
            SaveTracker tracker = GetSaveTracker(progression.rainWorld.options.saveSlot, slugcatNumber.value);

            // Absolute mess...
            Menu.SlugcatSelectMenu.SaveGameData saveGameData = ((Menu.SlugcatSelectMenu)menu).GetSaveGameData(pageIndex - 1); ;
            int wasGameTimeAlive = saveGameData.gameTimeAlive;
            
            if (Options.includeMilliseconds.Value)
                saveGameData.gameTimeAlive += (tracker.aliveTimeMilliseconds + tracker.deathTimeMilliseconds) / 1000;


            orig(self, menu, owner, pageIndex, slugcatNumber);


            self.saveGameData.gameTimeAlive = wasGameTimeAlive;


            if (Options.includeMilliseconds.Value)
                self.regionLabel.text = self.regionLabel.text.Insert(self.regionLabel.text.Length - 1, $":{((tracker.aliveTimeMilliseconds + tracker.deathTimeMilliseconds) % 1000).ToString().PadLeft(3, '0')}ms");



            if (!Options.extraTimers.Value) return;
            
            if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;


            string completedCycleTime = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + tracker.aliveTimeCorrection)) : self.saveGameData.gameTimeAlive + "s";
            string lostCycleTime = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeDead + tracker.deathTimeCorrection)) : self.saveGameData.gameTimeDead + "s";

            if (Options.formatExtraTimers.Value && Options.includeMilliseconds.Value)
            {
                completedCycleTime += $":{tracker.aliveTimeMilliseconds.ToString().PadLeft(3, '0')}ms";
                lostCycleTime += $":{tracker.deathTimeMilliseconds.ToString().PadLeft(3, '0')}ms";
            }


            self.regionLabel.text += $"\n(Completed: {completedCycleTime} - Lost: {lostCycleTime})";
        }

    }
}