using IL.Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;


namespace SpeedrunTimerFix
{
    internal static class Hooks
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


        private static readonly Dictionary<int, Dictionary<SlugcatStats.Name, SaveTracker>> savesTracker = new();
        
        private class SaveTracker
        {
            public WeakReference<SaveState>? saveState;

            public int startTime = 0;
            public int starveTime = 0;
        }
        
        private static SaveTracker GetSaveTracker(int saveSlot, SlugcatStats.Name saveStateNumber)
        {
            if (!savesTracker.ContainsKey(saveSlot))
                savesTracker[saveSlot] = new Dictionary<SlugcatStats.Name, SaveTracker>();

            if (!savesTracker[saveSlot].ContainsKey(saveStateNumber))
                savesTracker[saveSlot][saveStateNumber] = new SaveTracker();

            return savesTracker[saveSlot][saveStateNumber];
        }

        private static void SaveToDisk(PlayerProgression self, SaveTracker saveTracker)
        {
            if (saveTracker.saveState == null || !saveTracker.saveState.TryGetTarget(out var saveState)) return;
            
            var prevSaveState = self.currentSaveState;


            self.currentSaveState = saveState;
            self.SaveToDisk(true, false, false);
           

            self.currentSaveState = prevSaveState;
        }

        private static void ConvertStarveTimeToDeathTime(PlayerProgression self)
        {
            foreach (Dictionary<SlugcatStats.Name, SaveTracker> saveStateNumberTrackerPair in savesTracker.Values)
            {
                foreach (SaveTracker saveTracker in saveStateNumberTrackerPair.Values)
                {
                    if (saveTracker.saveState == null) continue;

                    if (!saveTracker.saveState.TryGetTarget(out var saveState)) continue;

                    saveState.totTime -= saveTracker.starveTime;
                    saveState.deathPersistentSaveData.deathTime += saveTracker.starveTime;

                    saveTracker.starveTime = 0;

                    SaveToDisk(self, saveTracker);
                }
            }
        }




        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            if (!Options.extraTimers.Value) return;
            
            if (self.saveGameData.shelterName == null || self.saveGameData.shelterName.Length <= 2) return;


            string completedCycleTIme = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive)) : self.saveGameData.gameTimeAlive + "s";
            string lostCycleTime = Options.formatExtraTimers.Value ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeDead)) : self.saveGameData.gameTimeDead + "s";

            self.regionLabel.text += $"\n(Completed: {completedCycleTIme} - Lost: {lostCycleTime})";
        }



        private static void MainMenu_ExitButtonPressed(On.Menu.MainMenu.orig_ExitButtonPressed orig, Menu.MainMenu self)
        {
            ConvertStarveTimeToDeathTime(self.manager.rainWorld.progression);

            orig(self);
        }

        private static void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "RESTART")
                ConvertStarveTimeToDeathTime(self.manager.rainWorld.progression);

            orig(self, sender, message);
        }

        private static void StoryGameSession_AppendTimeOnCycleEnd(On.StoryGameSession.orig_AppendTimeOnCycleEnd orig, StoryGameSession self, bool deathOrGhost)
        {
            orig(self, deathOrGhost);

            if (deathOrGhost)
            {
                ConvertStarveTimeToDeathTime(self.game.rainWorld.progression);
                return;
            }

            //PlayerProgression progression = self.game.rainWorld.progression;

            //SlugcatStats.Name? saveStateNumber = progression.currentSaveState != null ? progression.currentSaveState.saveStateNumber : progression.starvedSaveState != null ? progression.starvedSaveState.saveStateNumber : null;
            //if (saveStateNumber == null) return;

            //SaveTracker saveTracker = GetSaveTracker(self.game.rainWorld.options.saveSlot, saveStateNumber);
            //saveTracker.starveTime = 0;
        }



        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            Plugin.Logger.LogWarning("Tracking save...");
            var result = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            SaveTracker saveTracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveStateNumber);

            if (saveTracker.saveState == null)
                saveTracker.saveState = new WeakReference<SaveState>(result);
            
            else
                saveTracker.saveState.SetTarget(result);


            if (!saveTracker.saveState.TryGetTarget(out SaveState saveState)) return result;
            saveTracker.startTime = saveState.totTime;


            return result;
        }

        private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
        {
            bool result = orig(self, malnourished);

            SlugcatStats.Name? saveStateNumber = self.currentSaveState != null ? self.currentSaveState.saveStateNumber : self.starvedSaveState != null ? self.starvedSaveState.saveStateNumber : null;
            if (saveStateNumber == null) return result;

            SaveTracker saveTracker = GetSaveTracker(self.rainWorld.options.saveSlot, saveStateNumber);
            saveTracker.starveTime = 0;


            if (!malnourished) return result;
            
            if (self.starvedSaveState == null || saveTracker.saveState == null) return result;

            if (!saveTracker.saveState.TryGetTarget(out var saveState)) return result;

       
            Plugin.Logger.LogWarning("Saving starve time...");

            saveTracker.starveTime = self.starvedSaveState.totTime - saveTracker.startTime;
            saveState.totTime = self.starvedSaveState.totTime;

            SaveToDisk(self, saveTracker);

            return result;
        }
    }
}


// Notes on the timer...

//TimeSpan.FromSeconds(
//    // DEATH PERSISTENT
//    player.abstractCreature.world.game.GetStorySession.saveState.totTime // Last 2 are added on cycle end, if player is alive
//    + player.abstractCreature.world.game.GetStorySession.saveState.deathPersistentSaveData.deathTime // Last 2 are added on cycle end, if the player is dead

//    // NOT DEATH PERSISTENT
//    + player.abstractCreature.world.game.GetStorySession.playerSessionRecords[0].time / 40 // Begins on cycle start, stops when played is dead, or grabbed by enemy (after first UI sound)
//    + player.abstractCreature.world.game.GetStorySession.playerSessionRecords[0].playerGrabbedTime / 40); // Begins when played is grabbed by enemy and is not dead (after first UI sound)



//// DEAD OR ECHOED
//if (deathOrGhost)
//{
//    storyGameSession.saveState.deathPersistentSaveData.deathTime += storyGameSession.playerSessionRecords[0].time / 40 + storyGameSession.playerSessionRecords[0].playerGrabbedTime / 40;
//    storyGameSession.playerSessionRecords[0].playerGrabbedTime = 0;
//}

//// HIBERNATED
//else
//{
//    storyGameSession.saveState.totTime += storyGameSession.playerSessionRecords[0].time / 40;
//    storyGameSession.saveState.deathPersistentSaveData.deathTime += storyGameSession.playerSessionRecords[0].playerGrabbedTime / 40;
//    storyGameSession.playerSessionRecords[0].playerGrabbedTime = 0;
//}