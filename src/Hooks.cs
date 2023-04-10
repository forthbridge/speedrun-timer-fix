using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
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


                On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
                On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;


                //On.RainWorldGame.Update += RainWorldGame_Update;
                //On.SaveState.LoadGame += SaveState_LoadGame;
                //On.PlayerProgression.SaveWorldStateAndProgression += PlayerProgression_SaveWorldStateAndProgression;


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



        private static Menu.SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_MineForSaveData(On.Menu.SlugcatSelectMenu.orig_MineForSaveData orig, ProcessManager manager, SlugcatStats.Name slugcat)
        {
            var result = orig(manager, slugcat);

            if (result == null) return result!;

            SaveState? starvedSaveState = manager.rainWorld.progression?.starvedSaveState;
            if (starvedSaveState == null) return result;


            result.gameTimeAlive = starvedSaveState.totTime;
            
            if (starvedSaveState.deathPersistentSaveData != null) 
                result.gameTimeDead = starvedSaveState.deathPersistentSaveData.deathTime;

            return result;
        }

        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, Menu.SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, Menu.MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            // Replace existing select menu timer
            TimeSpan timeSpan = TimeSpan.FromSeconds((double)self.saveGameData.gameTimeAlive + self.saveGameData.gameTimeDead);
            string textToRemove = " (" + MoreSlugcats.SpeedRunTimer.TimeFormat(timeSpan) + ")";

            bool formatTime = false;

            string survivedCycleTime = formatTime ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeAlive)) : self.saveGameData.gameTimeAlive + "s";
            string lostCycleTime = formatTime ? MoreSlugcats.SpeedRunTimer.TimeFormat(TimeSpan.FromSeconds(self.saveGameData.gameTimeDead)) : self.saveGameData.gameTimeDead + "s";

            string textToAdd = $"\n(Survived: {survivedCycleTime} - Lost: {lostCycleTime})";


            self.regionLabel.text = self.regionLabel.text.Replace(textToRemove, textToAdd);
        }




        private static bool PlayerProgression_SaveWorldStateAndProgression(On.PlayerProgression.orig_SaveWorldStateAndProgression orig, PlayerProgression self, bool malnourished)
        {
            bool result = orig(self, malnourished);

            if (self.starvedSaveState == null) return result;

            Plugin.Logger.LogWarning("TOTAL TIME: " + self.starvedSaveState.totTime);
            Plugin.Logger.LogWarning("DEATH TIME  " + self.starvedSaveState.deathPersistentSaveData.deathTime);

            return result;
        }

        private static void SaveState_LoadGame(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
        {
            orig(self, str, game);

            Plugin.Logger.LogWarning("TOTAL TIME: " + self.totTime);
        }

        private static int frameCounter = 0;

        private static void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);

            frameCounter++;

            if (frameCounter % 40 != 0) return;


            Plugin.Logger.LogWarning("--------");

            Plugin.Logger.LogWarning($"Total Time: {self.GetStorySession.saveState.totTime}");
            Plugin.Logger.LogWarning($"Death Time: {self.GetStorySession.saveState.deathPersistentSaveData.deathTime}");


            if (self.GetStorySession.playerSessionRecords.Length == 0) return;

            Plugin.Logger.LogWarning($"Session Time: {self.GetStorySession.playerSessionRecords[0].time / 40}");
            Plugin.Logger.LogWarning($"Grabbed Time: {self.GetStorySession.playerSessionRecords[0].playerGrabbedTime / 40}");
        }
    }
}
