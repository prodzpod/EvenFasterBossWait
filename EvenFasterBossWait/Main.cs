using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API.Utils;
using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace EvenFasterBossWait
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "FasterBossWait2";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public static ConfigEntry<bool> UseFixedTime;
        public static ConfigEntry<float> Value;
        public static ConfigEntry<float> ValuePerHP;
        public static ConfigEntry<float> EliteBonus;
        public static ConfigEntry<float> EliteT2Bonus;
        public static ConfigEntry<float> MinibossBonus;
        public static ConfigEntry<float> BossBonus;
        public static ConfigEntry<float> PreBossKillPenalty;
        public static ConfigEntry<float> OutsideRangePenalty;
        public static ConfigEntry<bool> UnlockInteractables;
        public static ConfigEntry<bool> UnlockInteractablesPostBoss;
        public static ConfigEntry<bool> UnlockVoidSeeds;
        public static ConfigEntry<StackingMode> ModePerPerson;
        public static ConfigEntry<float> ValuePerPerson;
        public static ConfigEntry<StackingMode> ModePerPersonArea;
        public static ConfigEntry<float> ValuePerPersonArea;
        public static ConfigEntry<StackingMode> ModePerStage;
        public static ConfigEntry<float> ValuePerStage;
        public static ConfigEntry<StackingMode> ModePerLoop;
        public static ConfigEntry<float> ValuePerLoop;
        public static ConfigEntry<int> FocusedConvergenceRateLimit;
        public static ConfigEntry<int> FocusedConvergenceRangeLimit;
        public static ConfigEntry<float> TeleporterTime;
        public static ConfigEntry<float> TeleporterMult;
        public static ConfigEntry<float> TeleporterKill;
        public static ConfigEntry<float> TeleporterArea;
        public static ConfigEntry<float> TeleporterMultBoss;
        public static ConfigEntry<float> TeleporterAreaBoss;
        public static ConfigEntry<float> PrimordialTime;
        public static ConfigEntry<float> PrimordialMult;
        public static ConfigEntry<float> PrimordialKill;
        public static ConfigEntry<float> PrimordialArea;
        public static ConfigEntry<float> PrimordialMultBoss;
        public static ConfigEntry<float> PrimordialAreaBoss;
        public static ConfigEntry<float> PillarMassTime;
        public static ConfigEntry<float> PillarMassMult;
        public static ConfigEntry<float> PillarMassKill;
        public static ConfigEntry<float> PillarMassArea;
        public static ConfigEntry<float> PillarDesignTime;
        public static ConfigEntry<float> PillarDesignMult;
        public static ConfigEntry<float> PillarDesignKill;
        public static ConfigEntry<float> PillarDesignArea;
        public static ConfigEntry<float> PillarBloodTime;
        public static ConfigEntry<float> PillarBloodMult;
        public static ConfigEntry<float> PillarBloodKill;
        public static ConfigEntry<float> PillarBloodArea;
        public static ConfigEntry<float> PillarSoulTime;
        public static ConfigEntry<float> PillarSoulMult;
        public static ConfigEntry<float> PillarSoulKill;
        public static ConfigEntry<float> PillarSoulArea;
        public static ConfigEntry<float> EndingTime;
        public static ConfigEntry<float> EndingMult;
        public static ConfigEntry<float> EndingKill;
        public static ConfigEntry<float> EndingArea;
        public static ConfigEntry<float> FieldTime;
        public static ConfigEntry<float> FieldMult;
        public static ConfigEntry<float> FieldKill;
        public static ConfigEntry<float> FieldArea;
        public static ConfigEntry<float> LocusTime;
        public static ConfigEntry<float> LocusMult;
        public static ConfigEntry<float> LocusKill;
        public static ConfigEntry<float> LocusArea;

        public static List<HoldoutMultiplierInfo> HoldoutMultipliers; // for interop
        public struct HoldoutMultiplierInfo
        {
            public string name;
            public float time;
            public float mult;
            public float multBoss;
            public float kill;
            public float area;
            public float areaBoss;
        }
        public static HoldoutMultiplierInfo DefaultHoldoutInfo = new()
        {
            name = "",
            time = -1,
            mult = 1,
            multBoss = -1,
            kill = 1,
            area = -1,
            areaBoss = -1
        };

        public enum StackingMode
        {
            Linear,
            Exponential,
            Hyperbolic,
            Asymptotic
        };

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);

            UseFixedTime = Config.Bind("Kills to Time", "Use Fixed Time", true, "If true, value will be seconds instead of percent.");
            Value = Config.Bind("Kills to Time", "Base Value", 1f, "Charge value per kill.");
            ValuePerHP = Config.Bind("Kills to Time", "Additional Value Per HP", 0f, "Charge value per base HP of the enemy, takes elites into account but not ambient level.");
            EliteBonus = Config.Bind("Kills to Time", "Elite Bonus Value", 1f, "Extra charge given for defeating an elite.");
            EliteT2Bonus = Config.Bind("Kills to Time", "Elite T2 Bonus Value", 10f, "Extra charge given for defeating a tier 2 elite. stacks with general elite bonus.");
            MinibossBonus = Config.Bind("Kills to Time", "Miniboss Bonus Value", 2f, "Extra charge given for defeating a miniboss.");
            BossBonus = Config.Bind("Kills to Time", "Boss Bonus Value", 9f, "Extra charge given for defeating a champion.");
            PreBossKillPenalty = Config.Bind("Kills to Time", "Pre Bosskill Penalty", 0f, "Every kill % before the boss is defeated will be multiplied by this amount.");
            OutsideRangePenalty = Config.Bind("Kills to Time", "Outside Range Penalty", 1f, "Every kill % while you're outside the teleporter range will be multiplied by this amount.");

            UnlockInteractables = Config.Bind("Unlock Interactables", "Unlock Interactables", false, "Unlock interactables with a teleporter enabled.");
            UnlockInteractablesPostBoss = Config.Bind("Unlock Interactables", "Unlock Interactables Post Boss", false, "Unlock interactables with a teleporter enabled.");
            UnlockVoidSeeds = Config.Bind("Unlock Interactables", "Unlock Void Seeds", false, "Unlock interactables with void seed enabled.");

            ModePerPerson = Config.Bind("General Scaling", "Per Person Multiplier Mode", StackingMode.Exponential, "Linear: Value*Count, Exponential: Value^Count, Hyperbolic: Value*(Count/(Count+1)), Asymptotic: Value - (Value-1)*(1-0.5^Count)");
            ValuePerPerson = Config.Bind("General Scaling", "Multiplier Per Person", 1f, "For people who are well coordinated?? idk");
            ModePerPersonArea = Config.Bind("General Scaling", "Per Person Area Multiplier Mode", StackingMode.Exponential, "Linear: Value*Count, Exponential: Value^Count, Hyperbolic: Value*(Count/(Count+1)), Asymptotic: Value - (Value-1)*(1-0.5^Count)");
            ValuePerPersonArea = Config.Bind("General Scaling", "Area Multiplier Per Person", 1f, "For people who are well coordinated?? idk");
            ModePerStage = Config.Bind("General Scaling", "Per Stage Multiplier Mode", StackingMode.Exponential, "Linear: Value*Count, Exponential: Value^Count, Hyperbolic: Value*(Count/(Count+1)), Asymptotic: Value - (Value-1)*(1-0.5^Count)");
            ValuePerStage = Config.Bind("General Scaling", "Multiplier Per Stage", 1f, "multiplier per stage.");
            ModePerLoop = Config.Bind("General Scaling", "Per Loop Multiplier Mode", StackingMode.Linear, "Linear: Value*Count, Exponential: Value^Count, Hyperbolic: Value*(Count/(Count+1)), Asymptotic: Value - (Value-1)*(1-0.5^Count)");
            ValuePerLoop = Config.Bind("General Scaling", "Multiplier Per Loop", 1f, "This is also a ChargeInHalf continuation");
            FocusedConvergenceRateLimit = Config.Bind("General Scaling", "Focused Convergence Rate Max Stack", -1, "Set to -1 for infinite.");
            FocusedConvergenceRangeLimit = Config.Bind("General Scaling", "Focused Convergence Range Max Stack", 3, "Set to -1 for infinite.");

            TeleporterTime = Config.Bind("Zones", "Teleporter Base Charge Time", 90f, "Good ol' Teleporter.");
            TeleporterMult = Config.Bind("Zones", "Teleporter Charge Multiplier", 1f, "General Scaling's effect on Teleporters. (except focused convergence)");
            TeleporterKill = Config.Bind("Zones", "Teleporter Kills Multiplier", 1f, "Kills to Time's effect on Teleporters.");
            TeleporterArea = Config.Bind("Zones", "Teleporter Base Area", 60f, "");
            TeleporterMultBoss = Config.Bind("Zones", "Teleporter Charge Rate post Bosskill", 1f, "");
            TeleporterAreaBoss = Config.Bind("Zones", "Teleporter Base Area post Bosskill", 60f, "");

            PrimordialTime = Config.Bind("Zones", "Primordial Teleporter Base Charge Time", 90f, "Primordial Teleporter, in case you want Stage 5 to be special.");
            PrimordialMult = Config.Bind("Zones", "Primordial Teleporter Charge Multiplier", 1f, "General Scaling's effect on Primordial Teleporters. (except focused convergence)");
            PrimordialKill = Config.Bind("Zones", "Primordial Teleporter Kills Multiplier", 0f, "Kills to Time's effect on Primordial Teleporters.");
            PrimordialArea = Config.Bind("Zones", "Primordial Teleporter Base Area", 60f, "");
            PrimordialMultBoss = Config.Bind("Zones", "Primordial Teleporter Charge Rate post Bosskill", 1f, "");
            PrimordialAreaBoss = Config.Bind("Zones", "Primordial Teleporter Base Area post Bosskill", 60f, "");

            PillarMassTime = Config.Bind("Zones", "Pillar of Mass Base Charge Time", 60f, "Who doesn't like Moon Pillars?");
            PillarMassMult = Config.Bind("Zones", "Pillar of Mass Charge Multiplier", 0f, "General Scaling's effect on Moon Pillars. (except focused convergence)");
            PillarMassKill = Config.Bind("Zones", "Pillar of Mass Kills Multiplier", 0f, "Kills to Time's effect on Moon Pillars.");
            PillarMassArea = Config.Bind("Zones", "Pillar of Mass Base Area", 20f, "");

            PillarDesignTime = Config.Bind("Zones", "Pillar of Design Base Charge Time", 30f, "Who doesn't like Moon Pillars?");
            PillarDesignMult = Config.Bind("Zones", "Pillar of Design Charge Multiplier", 0f, "General Scaling's effect on Moon Pillars. (except focused convergence)");
            PillarDesignKill = Config.Bind("Zones", "Pillar of Design Kills Multiplier", 0f, "Kills to Time's effect on Moon Pillars.");
            PillarDesignArea = Config.Bind("Zones", "Pillar of Design Base Area", 20f, "");

            PillarSoulTime = Config.Bind("Zones", "Pillar of Soul Base Charge Time", 30f, "Who doesn't like Moon Pillars?");
            PillarSoulMult = Config.Bind("Zones", "Pillar of Soul Charge Multiplier", 0f, "General Scaling's effect on Moon Pillars. (except focused convergence)");
            PillarSoulKill = Config.Bind("Zones", "Pillar of Soul Kills Multiplier", 0f, "Kills to Time's effect on Moon Pillars.");
            PillarSoulArea = Config.Bind("Zones", "Pillar of Soul Base Area", 20f, "");

            PillarBloodTime = Config.Bind("Zones", "Pillar of Blood Base Charge Time", 10f, "Who doesn't like Moon Pillars?");
            PillarBloodMult = Config.Bind("Zones", "Pillar of Blood Charge Multiplier", 0f, "General Scaling's effect on Moon Pillars. (except focused convergence)");
            PillarBloodKill = Config.Bind("Zones", "Pillar of Blood Kills Multiplier", 0f, "Kills to Time's effect on Moon Pillars.");
            PillarBloodArea = Config.Bind("Zones", "Pillar of Blood Base Area", 20f, "");

            EndingTime = Config.Bind("Zones", "The Final Spacecraft Base Charge Time", 120f, "And so they left...");
            EndingMult = Config.Bind("Zones", "The Final Spacecraft Charge Multiplier", 0f, "General Scaling's effect on The Final Spacecraft. (except focused convergence)");
            EndingKill = Config.Bind("Zones", "The Final Spacecraft Kills Multiplier", 2f, "Kills to Time's effect on The Final Spacecraft.");
            EndingArea = Config.Bind("Zones", "The Final Spacecraft Base Area", 40f, "");

            FieldTime = Config.Bind("Zones", "Void Field Base Charge Time", 60f, "Void Fields");
            FieldMult = Config.Bind("Zones", "Void Field Charge Multiplier", 1f, "General Scaling's effect on Void Fields. (except focused convergence)");
            FieldKill = Config.Bind("Zones", "Void Field Kills Multiplier", 1f, "Kills to Time's effect on Void Fields.");
            FieldArea = Config.Bind("Zones", "Void Field Base Area", 15f, "");

            LocusTime = Config.Bind("Zones", "Deep Void Beacon Base Charge Time", 90f, "Deep Void Beacons");
            LocusMult = Config.Bind("Zones", "Deep Void Beacon Charge Multiplier", 1f, "General Scaling's effect on Deep Void Beacons. (except focused convergence)");
            LocusKill = Config.Bind("Zones", "Deep Void Beacon Kills Multiplier", 1f, "Kills to Time's effect on Deep Void Beacons.");
            LocusArea = Config.Bind("Zones", "Deep Void Beacon Base Area", 20f, "");

            // thank god FH teleporters aren't a holdout. Based and plasmapilled!

            HoldoutMultipliers = new()
            {
                new HoldoutMultiplierInfo()
                {
                    name = "Teleporter1",
                    time = TeleporterTime.Value,
                    mult = TeleporterMult.Value,
                    multBoss = TeleporterMultBoss.Value,
                    kill = TeleporterKill.Value,
                    area = TeleporterArea.Value,
                    areaBoss = TeleporterAreaBoss.Value
                },
                new HoldoutMultiplierInfo()
                {
                    name = "LunarTeleporter",
                    time = PrimordialTime.Value,
                    mult = PrimordialMult.Value,
                    multBoss = PrimordialMultBoss.Value,
                    kill = PrimordialKill.Value,
                    area = PrimordialArea.Value,
                    areaBoss = PrimordialAreaBoss.Value
                },
                new HoldoutMultiplierInfo()
                {
                    name = "MoonBatteryMass",
                    time = PillarMassTime.Value,
                    mult = PillarMassMult.Value,
                    multBoss = -1,
                    kill = PillarMassKill.Value,
                    area = PillarMassArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "MoonBatteryDesign",
                    time = PillarDesignTime.Value,
                    mult = PillarDesignMult.Value,
                    multBoss = -1,
                    kill = PillarDesignKill.Value,
                    area = PillarDesignArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "MoonBatteryBlood",
                    time = PillarBloodTime.Value,
                    mult = PillarBloodMult.Value,
                    multBoss = -1,
                    kill = PillarBloodKill.Value,
                    area = PillarBloodArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "MoonBatterySoul",
                    time = PillarSoulTime.Value,
                    mult = PillarSoulMult.Value,
                    multBoss = -1,
                    kill = PillarSoulKill.Value,
                    area = PillarSoulArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "HoldoutZone", // why is it just called HoldoutZone what
                    time = EndingTime.Value,
                    mult = EndingMult.Value,
                    multBoss = -1,
                    kill = EndingKill.Value,
                    area = EndingArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "NullSafeZone",
                    time = FieldTime.Value,
                    mult = FieldMult.Value,
                    multBoss = -1,
                    kill = FieldKill.Value,
                    area = FieldArea.Value,
                    areaBoss = -1
                },
                new HoldoutMultiplierInfo()
                {
                    name = "DeepVoidPortalBattery",
                    time = LocusTime.Value,
                    mult = LocusMult.Value,
                    multBoss = -1,
                    kill = LocusKill.Value,
                    area = LocusArea.Value,
                    areaBoss = -1
                }
            };
            Hooks.Patch();
        }

        public static float Calc(StackingMode mode, float value, float count)
        {
            switch (mode)
            {
                case StackingMode.Linear:
                    return value * count;
                case StackingMode.Exponential:
                    return Mathf.Pow(value, count);
                case StackingMode.Hyperbolic:
                    return value * count / (count + 1);
                case StackingMode.Asymptotic:
                    return (1 - value) * (1 - Mathf.Pow(2, -count)) + value;
            }
            Log.LogError("Invalid Mode??");
            return 0;
        }
    }
}
