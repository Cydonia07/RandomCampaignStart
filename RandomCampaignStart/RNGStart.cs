﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BattleTech;
using Harmony;
using HBS.Logging;
using Newtonsoft.Json;

namespace RandomCampaignStart
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        // from https://stackoverflow.com/questions/273313/randomize-a-listt
        private static readonly Random rng = new Random();
        private static void RNGShuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void Postfix(SimGameState __instance)
        {
            // clear roster
            if (RngStart.Settings.NumberRandomRonin + RngStart.Settings.NumberProceduralPilots > 0)
            {
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }

                // pilotgenerator seems to give me the same exact results for ronin
                // every time, and can push out duplicates, which is odd?
                // just do our own thing
                var pilots = new List<PilotDef>();

                if (RngStart.Settings.NumberRandomRonin > 0)
                {
                    var roninPilots = new List<PilotDef>(__instance.RoninPilots);
                    roninPilots.RNGShuffle();

                    for (int i = 0; i < RngStart.Settings.NumberRandomRonin; i++)
                    {
                        pilots.Add(roninPilots[i]);
                    }
                }

                // pilot generator works fine for non-ronin =/
                if (RngStart.Settings.NumberProceduralPilots > 0)
                {
                    var randomPilots = __instance.PilotGenerator.GeneratePilots(RngStart.Settings.NumberProceduralPilots, 1, 0, out var notUsed);
                    pilots.AddRange(randomPilots);
                }

                foreach (var pilotDef in pilots)
                {
                    __instance.AddPilotToRoster(pilotDef, true);
                }
            }
            
            // mechs
            if (RngStart.Settings.NumberLightMechs + RngStart.Settings.NumberMediumMechs > 0)
            {
                int baySlot = 1;
                var mechIds = new List<string>();
                
                // remove the mechs added by the startinglance
                for (int i = 1; i < __instance.Constants.Story.StartingLance.Length + 1; i++)
                {
                    __instance.ActiveMechs.Remove(i);
                }

                // remove ancestral mech if specified
                if (RngStart.Settings.RemoveAncestralMech)
                {
                    __instance.ActiveMechs.Remove(0);
                    baySlot = 0;
                }

                // add the random medium mechs
                var mediumMechIds = new List<string>(RngStart.Settings.MediumMechsPossible);
                while (mediumMechIds.Count < RngStart.Settings.NumberMediumMechs)
                {
                    mediumMechIds.AddRange(RngStart.Settings.MediumMechsPossible);
                }

                mediumMechIds.RNGShuffle();
                for (int i = 0; i < RngStart.Settings.NumberMediumMechs; i++)
                {
                    mechIds.Add(mediumMechIds[i]);
                }

                // add the random light mechs
                var lightMechIds = new List<string>(RngStart.Settings.LightMechsPossible);
                while (lightMechIds.Count < RngStart.Settings.NumberLightMechs)
                {
                    lightMechIds.AddRange(RngStart.Settings.LightMechsPossible);
                }

                lightMechIds.RNGShuffle();
                for (int i = 0; i < RngStart.Settings.NumberLightMechs; i++)
                {
                    mechIds.Add(lightMechIds[i]);
                }

                // actually add the mechs to the game
                foreach (var mechId in mechIds)
                {
                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechId), __instance.GenerateSimGameUID());
                    __instance.AddMech(baySlot, mechDef, true, true, false);
                    baySlot++;
                }
            }
        }
    }

    internal class ModSettings
    {
        public bool RemoveAncestralMech = false;
        public int NumberRandomRonin = 4;
        public int NumberProceduralPilots = 0;
        public int NumberLightMechs = 3;
        public int NumberMediumMechs = 1;

        public List<string> LightMechsPossible = new List<string>();
        public List<string> MediumMechsPossible = new List<string>();
    }

    public static class RngStart
    {
        internal static ILog Logger = HBS.Logging.Logger.GetLogger("RandomCampaignStart");
        internal static ModSettings Settings;

        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // read settings
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }
        }
    }
}