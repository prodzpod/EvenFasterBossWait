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
        public static List<MasterIndex> Minibosses = new();
        public static Dictionary<HoldoutZoneController, Main.HoldoutMultiplierInfo> ActiveZones = new();
        public static Dictionary<HoldoutZoneController, bool> BossKilled = new(); // API: set to true when "boss" is "killed", will auto trigger for teleporters
        public static Dictionary<HoldoutZoneController, float> ChargeCredit = new(); // API: simply add to chargecredit to make time pass
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
                if (ActiveZones[self].time >= 0) self.baseChargeDuration = ActiveZones[self].time * (1 + (ActiveZones[self].mult * (
                      Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount)
                    * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount + 1)
                    * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount))));
                if (ActiveZones[self].area >= 0) self.baseRadius = ActiveZones[self].area
                    * (1 + (ActiveZones[self].mult * (Main.Calc(Main.ModePerPersonArea.Value, Main.ValuePerPersonArea.Value, Run.instance.participatingPlayerCount) - 1)));
                if (ActiveZones[self].areaBoss >= 0) { self.calcRadius += (ref float radius) => { if (BossKilled[self]) radius *= ActiveZones[self].areaBoss / ActiveZones[self].area; }; }
                if (ActiveZones[self].multBoss >= 0) { self.calcChargeRate += (ref float rate) => { if (BossKilled[self]) rate *= ActiveZones[self].multBoss; }; }
                self.calcAccumulatedCharge += (ref float charge) => { if (ChargeCredit[self] > 0) { charge += ChargeCredit[self]; ChargeCredit[self] = 0; } };
                orig(self);
            };
            GlobalEventManager.onCharacterDeathGlobal += (report) =>
            {
                if (report?.victimBody?.teamComponent?.teamIndex == null || report?.attackerBody?.teamComponent?.teamIndex == null) return;
                if (!TeamManager.IsTeamEnemy(TeamIndex.Player, report.victimBody.teamComponent.teamIndex) || report.attackerBody.teamComponent.teamIndex != TeamIndex.Player) return;
                foreach (var zone in ActiveZones.Keys)
                {
                    if (ActiveZones[zone].kill == 0 || report.victimIsBoss || zone.baseChargeDuration <= 0) continue;
                    float charge = Main.Value.Value + (report.victimBody.baseMaxHealth * Main.ValuePerHP.Value);
                    if (report.victimIsElite) charge += Main.EliteBonus.Value;
                    if (report.victimBody.affixes().Any(x => x.isT2())) charge += Main.EliteT2Bonus.Value;
                    if (report.victimIsMiniboss()) charge += Main.MinibossBonus.Value;
                    if (report.victimIsChampion) charge += Main.BossBonus.Value;
                    charge *= ActiveZones[zone].kill;
                    if (!BossKilled[zone]) charge *= Main.PreBossKillPenalty.Value;
                    if (!zone.IsInBounds(report.attackerBody.footPosition)) charge *= Main.OutsideRangePenalty.Value;
                    if (charge == 0) return;
                    ChargeCredit[zone] += charge * (Main.UseFixedTime.Value ? 1f / zone.baseChargeDuration : 0.01f);
                    if (Main.CompensateKills.Value != 0)
                    {
                        if (!Main.UseFixedTime.Value)
                        {
                            float rate = 1f / zone.baseChargeDuration;
                            HoldoutZoneController.CalcChargeRateDelegate calcChargeRate = AccessTools.FieldRefAccess<HoldoutZoneController.CalcChargeRateDelegate>(typeof(HoldoutZoneController), nameof(HoldoutZoneController.calcChargeRate))(zone);
                            calcChargeRate?.Invoke(ref rate);
                            charge *= 0.01f / rate;
                        }
                        if (Main.GeneralScalingCompensation.Value != 0) charge /= Mathf.Lerp(1 + (ActiveZones[zone].mult * (
                              Main.Calc(Main.ModePerPerson.Value, Main.ValuePerPerson.Value, Run.instance.participatingPlayerCount)
                            * Main.Calc(Main.ModePerStage.Value, Main.ValuePerStage.Value, Run.instance.stageClearCount + 1)
                            * Main.Calc(Main.ModePerLoop.Value, Main.ValuePerLoop.Value, Run.instance.loopClearCount))), 1, Main.GeneralScalingCompensation.Value);
                        Run.instance.SetRunStopwatch(Run.instance.GetRunStopwatch() + (charge * Main.CompensateKills.Value));
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
        }
    }
}
