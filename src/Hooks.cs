using Kittehface.Framework20;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

            SaveTracker tracker = GetSaveTracker(progression.rainWorld.options.saveSlot, saveStateNumber);


            if (Options.includeMilliseconds.Value)
            {
                if (!RainWorld.lockGameTimer)
                {
                    int totTime = self.ThePlayer().abstractCreature.world.game.GetStorySession.playerSessionRecords[0].time;
                    tracker.totalTimeMilliseconds = ((totTime % 40) / 40.0f) * 1000;

                    int deathTime = self.ThePlayer().abstractCreature.world.game.GetStorySession.playerSessionRecords[0].playerGrabbedTime;
                    tracker.deathTimeMilliseconds = ((deathTime % 40) / 40.0f) * 1000;

                    self.timeLabel.text += $":{(tracker.totalTimeMilliseconds + tracker.deathTimeMilliseconds).ToString().PadLeft(3, '0')}ms";
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
        private static Dictionary<int, Dictionary<SlugcatStats.Name, SaveTracker>> saveTrackers = new();
        
        private class SaveTracker
        {
            public int startTime = 0;

            public float totalTimeMilliseconds = 0.0f;
            public float deathTimeMilliseconds = 0.0f;

            public int totalTimeCorrection = 0;
            public int deathTimeCorrection = 0;
        }
        
        private static SaveTracker GetSaveTracker(int saveSlot, SlugcatStats.Name saveStateNumber)
        {
            if (!saveTrackers.ContainsKey(saveSlot))
                saveTrackers[saveSlot] = new();

            if (!saveTrackers[saveSlot].ContainsKey(saveStateNumber))
                saveTrackers[saveSlot][saveStateNumber] = new SaveTracker();

            return saveTrackers[saveSlot][saveStateNumber];
        }

        

        // Attach the tracker when a save is initialized, update the start time when it is loaded
        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            SaveState result = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            Plugin.Logger.LogWarning("Tracking save...");
            SaveTracker tracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveStateNumber);

            tracker.startTime = result.totTime;

            return result;
        }

        // Track starving saves
        private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
        {
            bool result = orig(self, malnourished);


            SaveState? saveState = self.currentSaveState ?? self.starvedSaveState;
            if (saveState == null) return result;


            SaveTracker tracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveState.saveStateNumber);

            if (!malnourished) return result;
            

            Plugin.Logger.LogWarning("Saving time in starve cycle!");
            tracker.totalTimeCorrection = saveState.totTime - tracker.startTime;

            return result;
        }

        private static void ApplyTimeCorrection(SaveTracker tracker, SaveState saveState)
        {
            saveState.totTime = saveState.totTime - tracker.totalTimeCorrection;
        }



        private static string SaveTrackerFilePath => Path.Combine(Custom.LegacyRootFolderDirectory(), Plugin.MOD_ID + "_saveTrackers.txt");

        private static void SaveTrackerToDisk()
        {
            string text = "HELLO WORLD";

            // TODO: Serialize saves tracker

            File.WriteAllText(SaveTrackerFilePath, text);
        }

        private static void LoadTrackerFromDisk()
        {
            string text = File.ReadAllText(SaveTrackerFilePath);

            // TODO: Deserialize data into saves tracker
        }

        private static void ConvertSurvivedTimeToDeathTime()
        {
            foreach (var nameTrackerPair in saveTrackers.Values)
            {
                foreach (SaveTracker tracker in nameTrackerPair.Values)
                {
                    tracker.deathTimeMilliseconds = tracker.totalTimeCorrection;
                    tracker.totalTimeCorrection = 0;
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


            ConvertSurvivedTimeToDeathTime();
        }
        
        

        // Adds extra timing information to the slugcat select menu, helps a lot with debugging
        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);


            PlayerProgression progression = self.menu.manager.rainWorld.progression;
            SaveTracker tracker = GetSaveTracker(progression.rainWorld.options.saveSlot, slugcatNumber);

            if (Options.includeMilliseconds.Value)
                self.regionLabel.text = self.regionLabel.text.Insert(self.regionLabel.text.Length - 1, $":{(tracker.totalTimeMilliseconds + tracker.deathTimeMilliseconds).ToString().PadLeft(3, '0')}ms");



            if (!Options.extraTimers.Value) return;
            
            if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;


            string completedCycleTime = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive + tracker.totalTimeCorrection)) : self.saveGameData.gameTimeAlive + "s";
            string lostCycleTime = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeDead + tracker.deathTimeCorrection)) : self.saveGameData.gameTimeDead + "s";

            if (Options.formatExtraTimers.Value && Options.includeMilliseconds.Value)
            {
                completedCycleTime += $":{tracker.totalTimeMilliseconds.ToString().PadLeft(3, '0')}ms";
                lostCycleTime += $":{tracker.deathTimeMilliseconds.ToString().PadLeft(3, '0')}ms";
            }


            self.regionLabel.text += $"\n(Completed: {completedCycleTime} - Lost: {lostCycleTime})";
        }

    }
}