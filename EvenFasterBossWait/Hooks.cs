using HarmonyLib;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoR2.MasterCatalog;

namespace EvenFasterBossWait
{
    public static class Extensions
    {
        public static bool victimIsMiniboss(this DamageReport report) => report?.victimMaster?.isMiniboss() ?? false;
        public static bool isMiniboss(this CharacterBody body) => body?.master?.isMiniboss() ?? false;
        public static bool isMiniboss(this CharacterMaster master) => Hooks.Minibosses.Contains(master?.masterIndex ?? MasterIndex.none);
        public static EliteDef[] affixes(this CharacterBody body) => body?.activeBuffsList?.ToList()?.FindAll(x => BuffCatalog.GetBuffDef(x)?.isElite ?? false).ConvertAll(x => BuffCatalog.GetBuffDef(x).eliteDef).ToArray() ?? Array.Empty<EliteDef>();
        public static bool isT2(this EliteDef elite) => CombatDirector.eliteTiers?.First(x => x.eliteTypes.Contains(RoR2Content.Elites.Poison))?.eliteTypes?.Contains(elite) ?? false;
    }
    public class Hooks
    {
        public static List<MasterIndex> Minibosses = [];
        public static Dictionary<HoldoutZoneController, Main.HoldoutMultiplierInfo> ActiveZones = [];
        public static Dictionary<HoldoutZoneController, bool> BossKilled = []; // API: set to true when "boss" is "killed", will auto trigger for teleporters
        public static Dictionary<HoldoutZoneController, float> ChargeCredit = []; // API: simply add to chargecredit to make time pass
        public static void Patch()
        {
            Run.onRunStartGlobal += (_) => { ActiveZones.Clear(); BossKilled.Clear(); ChargeCredit.Clear(); };
            On.RoR2.DirectorCardCategorySelection.GenerateDirectorCardWeightedSelection += (orig, self) =>
            {
                DirectorCardCategorySelection.Category cat = self.categories.ToList().Find(x => x.name == DirectorAPI.Helpers.GetVanillaMonsterCategoryName(DirectorAPI.MonsterCategory.Minibosses));
                if (!cat.Equals(default(DirectorCardCategorySelection.Category)))
                {
                    Minibosses.Clear();
                    foreach (var card in cat.cards)
                    {
                        if (!card.IsAvailable()) continue;
                        CharacterMaster master = card?.spawnCard?.prefab?.GetComponent<CharacterMaster>() ?? null;
                        if (master == null) continue;
                        Minibosses.Add(master.masterIndex);
                    }
                    Main.Log.LogDebug("Repopulated miniboss list with " + Minibosses.Count + " element(s).");
                }
                return orig(self);
            };
            On.RoR2.OutsideInteractableLocker.OnEnable += (orig, self) => { if (Main.UnlockInteractables.Value || (Main.UnlockVoidSeeds.Value && self.lockPrefab.name == "PurchaseLockVoid")) return; orig(self); };
            On.RoR2.OutsideInteractableLocker.FixedUpdate += (orig, self) => { if (Main.UnlockInteractables.Value || (Main.UnlockVoidSeeds.Value && self.lockPrefab.name == "PurchaseLockVoid")) return; orig(self); };
            On.RoR2.OutsideInteractableLocker.OnDisable += (orig, self) => { if (Main.UnlockInteractables.Value || (Main.UnlockVoidSeeds.Value && self.lockPrefab.name == "PurchaseLockVoid")) return; orig(self); };
            On.RoR2.HoldoutZoneController.OnEnable += (orig, self) => 
            { 
                if (Run.instance is InfiniteTowerRun) return; // no simulacrum allowed!!!!
                foreach (var info in Main.HoldoutMultipliers) if (self.gameObject.name.Contains(info.name)) ActiveZones.Add(self, info);
                if (!ActiveZones.ContainsKey(self)) ActiveZones.Add(self, Main.DefaultHoldoutInfo);
                BossKilled.Add(self, false);
                ChargeCredit.Add(self, 0);
                if (ActiveZones[self].time >= 0) self.baseChargeDuration = ActiveZones[self].time * Mathf.Lerp(1, 
                      Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount - 1)
                    * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)
                    * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount), ActiveZones[self].mult);
                if (ActiveZones[self].area >= 0) self.baseRadius = ActiveZones[self].area * Mathf.Lerp(1, 
                    Main.Calc(Main.ModePerPersonArea.Value, Main.ValuePerPersonArea.Value, Run.instance.participatingPlayerCount), ActiveZones[self].mult);
                if (self.gameObject.GetComponent<BossGroup>() != null && ActiveZones[self].multBoss >= 0) { self.calcChargeRate += (ref float rate) => { if (BossKilled[self]) rate *= ActiveZones[self].multBoss; }; }
                if (self.gameObject.GetComponent<BossGroup>() != null && ActiveZones[self].areaBoss >= 0) { self.calcRadius += (ref float radius) => { if (BossKilled[self]) radius *= ActiveZones[self].areaBoss / ActiveZones[self].area; }; }
                if (Main.DebugMode.Value)
                {
                    Main.Log.LogInfo("Holdout Zone Begin, boss: " + self.gameObject.GetComponent<BossGroup>() != null);
                    Main.Log.LogInfo($"Zone Info ({ActiveZones[self].name}): {ActiveZones[self].time} time, {ActiveZones[self].mult} mult, {ActiveZones[self].area} area, {ActiveZones[self].multBoss} multBoss, {ActiveZones[self].areaBoss} areaBoss");
                    Main.Log.LogInfo($"Config: {Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount - 1)}({Main.ModePerPerson.Value} {Main.ValuePerPerson.Value}x{Run.instance.participatingPlayerCount - 1}) / {Main.Calc(Main.ModePerPersonArea.Value, Main.ValuePerPersonArea.Value, Run.instance.participatingPlayerCount)}({Main.ModePerPersonArea.Value} {Main.ValuePerPersonArea.Value}x{Run.instance.participatingPlayerCount - 1})(area) per person, {Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)}({Main.ModePerStage.Value} {Main.ValuePerStage.Value}x{Run.instance.stageClearCount}) per stage, {Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount)}({Main.ModePerLoop.Value} {Main.ValuePerLoop.Value}x{Run.instance.loopClearCount}) per loop");
                    Main.Log.LogInfo($"Result: {self.baseChargeDuration}(x{ActiveZones[self].multBoss}b) time, {self.baseRadius}(x{ActiveZones[self].areaBoss / ActiveZones[self].area}b) area");
                }
                self.calcAccumulatedCharge += (ref float charge) => { if (ChargeCredit[self] > 0) { charge += ChargeCredit[self]; ChargeCredit[self] = 0; } };
                orig(self);
            };
            GlobalEventManager.onCharacterDeathGlobal += (report) =>
            {
                if (report?.victimBody?.teamComponent?.teamIndex == null || report?.attackerBody?.teamComponent?.teamIndex == null) return;
                if (!TeamManager.IsTeamEnemy(TeamIndex.Player, report.victimBody.teamComponent.teamIndex) || report.attackerBody.teamComponent.teamIndex != TeamIndex.Player) return;
                foreach (var zone in ActiveZones.Keys)
                {
                    if (report.victimIsBoss || zone.baseChargeDuration <= 0) continue;
                    if (Main.DebugMode.Value) Main.Log.LogInfo("Kill Credited, base: " + ActiveZones[zone].kill);
                    if (ActiveZones[zone].kill == 0) continue;
                    float charge = Main.Value.Value + (report.victimBody.baseMaxHealth * Main.ValuePerHP.Value);
                    if (Main.DebugMode.Value) Main.Log.LogInfo($"base: {Main.Value.Value}({Main.Value.Value}+{report.victimBody.baseMaxHealth}x{Main.ValuePerHP.Value})");
                    if (report.victimIsElite) charge += Main.EliteBonus.Value;
                    if (report.victimBody.affixes().Any(x => x.isT2())) charge += Main.EliteT2Bonus.Value;
                    if (report.victimIsMiniboss()) charge += Main.MinibossBonus.Value;
                    if (report.victimIsChampion) charge += Main.BossBonus.Value;
                    if (Main.DebugMode.Value) Main.Log.LogInfo("bonuses: "
                        + (report.victimIsElite ? $"+{Main.EliteBonus.Value} (elite)" : "")
                        + (report.victimBody.affixes().Any(x => x.isT2()) ? $"+{Main.EliteT2Bonus.Value} (T2)" : "")
                        + (report.victimIsMiniboss() ? $"+{Main.MinibossBonus.Value} (miniboss)" : "")
                        + (report.victimIsChampion ? $"+{Main.BossBonus.Value} (champion)" : ""));
                    charge *= ActiveZones[zone].kill;
                    if (zone.gameObject.GetComponent<BossGroup>() != null && !BossKilled[zone]) charge *= Main.PreBossKillPenalty.Value;
                    if (!zone.IsInBounds(report.attackerBody.footPosition)) charge *= Main.OutsideRangePenalty.Value;
                    if (Main.DebugMode.Value) Main.Log.LogInfo("multipliers: " + ActiveZones[zone].kill
                        + (zone.gameObject.GetComponent<BossGroup>() != null && !BossKilled[zone] ? $"x{Main.PreBossKillPenalty.Value} (prebosskill)" : "")
                        + (!zone.IsInBounds(report.attackerBody.footPosition) ? $"x{Main.OutsideRangePenalty.Value} (outsiderange)" : "")
                        + (Main.UseFixedTime.Value ? $"x1s(out of {zone.baseChargeDuration})" : "x1%"));
                    if (charge == 0) return;
                    ChargeCredit[zone] += charge * (Main.UseFixedTime.Value ? 1f / zone.baseChargeDuration : 0.01f);
                    if (Main.DebugMode.Value) Main.Log.LogInfo("Return:" + charge * (Main.UseFixedTime.Value ? 1f / zone.baseChargeDuration : 0.01f));
                    if (Main.CompensateKills.Value != 0)
                    {
                        if (!Main.UseFixedTime.Value)
                        {
                            float rate = 1f / zone.baseChargeDuration;
                            HoldoutZoneController.CalcChargeRateDelegate calcChargeRate = AccessTools.FieldRefAccess<HoldoutZoneController.CalcChargeRateDelegate>(typeof(HoldoutZoneController), nameof(HoldoutZoneController.calcChargeRate))(zone);
                            calcChargeRate?.Invoke(ref rate);
                            charge *= 0.01f / rate;
                            if (Main.DebugMode.Value) Main.Log.LogInfo("ChargeRate accessed");
                        }
                        if (Main.GeneralScalingCompensation.Value != 0) charge /= Mathf.Lerp(ActiveZones[zone].mult * (
                              Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount + 1)
                            * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)
                            * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount)), 1, Main.GeneralScalingCompensation.Value);
                        Run.instance.SetRunStopwatch(Run.instance.GetRunStopwatch() + (charge * Main.CompensateKills.Value));
                        if (Main.DebugMode.Value)
                        {
                            Main.Log.LogInfo("Time Compensated");
                            Main.Log.LogInfo($"Config: {Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount - 1)}({Main.ModePerPerson.Value} {Main.ValuePerPerson.Value}x{Run.instance.participatingPlayerCount - 1}) / {Main.Calc(Main.ModePerPersonArea.Value, Main.ValuePerPersonArea.Value, Run.instance.participatingPlayerCount)}({Main.ModePerPersonArea.Value} {Main.ValuePerPersonArea.Value}x{Run.instance.participatingPlayerCount - 1})(area) per person, {Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)}({Main.ModePerStage.Value} {Main.ValuePerStage.Value}x{Run.instance.stageClearCount}) per stage, {Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount)}({Main.ModePerLoop.Value} {Main.ValuePerLoop.Value}x{Run.instance.loopClearCount}) per loop");
                            Main.Log.LogInfo($"Result: a{ActiveZones[zone].mult * (
                              Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount + 1)
                            * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)
                            * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount))}, b{Mathf.Lerp(ActiveZones[zone].mult * (
                              Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount + 1)
                            * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount)
                            * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount)), 1, Main.GeneralScalingCompensation.Value)}({ActiveZones[zone].mult} mult), charge{charge}, d{Main.CompensateKills.Value}");
                        }
                    }
                }
            };
            BossGroup.onBossGroupDefeatedServer += (group) => 
            {
                if (TeleporterInteraction.instance?.bossGroup != null && TeleporterInteraction.instance.bossGroup == group && BossKilled.ContainsKey(TeleporterInteraction.instance.holdoutZoneController))
                {
                    BossKilled[TeleporterInteraction.instance.holdoutZoneController] = true;
                    if (Main.UnlockInteractablesPostBoss.Value && (bool)TeleporterInteraction.instance.outsideInteractableLocker) TeleporterInteraction.instance.outsideInteractableLocker.enabled = false;
                }
            };
            On.RoR2.HoldoutZoneController.OnDisable += (orig, self) =>
            {
                if (ActiveZones.ContainsKey(self)) ActiveZones.Remove(self);
                if (BossKilled.ContainsKey(self)) BossKilled.Remove(self);
                if (ChargeCredit.ContainsKey(self)) ChargeCredit.Remove(self);
                orig(self);
            };
            HoldoutZoneController.FocusConvergenceController.cap = int.MaxValue;
            On.RoR2.HoldoutZoneController.FocusConvergenceController.ApplyRate += (On.RoR2.HoldoutZoneController.FocusConvergenceController.orig_ApplyRate orig, MonoBehaviour self, ref float rate) =>
            {
                var count = ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount;
                if (Main.FocusedConvergenceRateLimit.Value >= 0) ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount = (int)MathF.Min(count, Main.FocusedConvergenceRateLimit.Value);
                orig(self, ref rate);
                ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount = count;
            };
            On.RoR2.HoldoutZoneController.FocusConvergenceController.ApplyRadius += (On.RoR2.HoldoutZoneController.FocusConvergenceController.orig_ApplyRadius orig, MonoBehaviour self, ref float radius) =>
            {
                var count = ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount;
                if (Main.FocusedConvergenceRangeLimit.Value >= 0) ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount = (int)MathF.Min(count, Main.FocusedConvergenceRangeLimit.Value);
                orig(self, ref radius);
                ((HoldoutZoneController.FocusConvergenceController)self).currentFocusConvergenceCount = count;
            };
        }
    }
}
