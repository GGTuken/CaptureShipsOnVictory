using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace CaptureShipsOnVictory
{
    public class SubModule : MBSubModuleBase
    {
        private static Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _harmony = new Harmony("com.captureshipsonvictory.patch");
            LogMessage("Mod loaded");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            try
            {
                _harmony.PatchAll();
                LogMessage("Patches applied successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying patches: {ex.Message}");
            }
        }

        public static void LogMessage(string message)
        {
            try
            {
                string logPath = Path.Combine(BasePath.Name, "Modules", "CaptureShipsOnVictory", "mod_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GameComponents.NavalDLCBattleRewardModel), "DistributeDefeatedPartyShipsAmongWinners")]
    public class ShipLootChancePatch
    {
        // Handle null MapEvent for surrender cases
        static bool Prefix(MapEvent mapEvent, ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result,
            MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            // If mapEvent is null (surrender without battle), handle it separately
            if (mapEvent == null)
            {
                // Use the same distribution logic but without MapEvent
                var result = DistributeShipsWithoutMapEvent(shipsToLoot, winnerParties);
                __result = result;
                return false; // Skip original method
            }
            return true; // Continue with original method
        }

        private static MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeShipsWithoutMapEvent(
            MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            // Simplified distribution logic (same as original but without storyline check)
            var dictionary = new Dictionary<Ship, MapEventParty>();
            var mBList = new MBList<Ship>();

            foreach (Ship item in shipsToLoot)
            {
                dictionary.Add(item, null);
                // Use 1.0f instead of 0.5f (already patched in transpiler for normal battles)
                if (MBRandom.RandomFloat < 1.0f)
                {
                    if (item.CanEquipFigurehead)
                    {
                        item.ChangeFigurehead(null);
                    }
                    mBList.Add(item);
                }
            }

            // Use existing distribution logic from base class or simplified version
            var source = winnerParties.Where(x => x.Party.IsMobile &&
                x.Party.MobileParty.PartyComponent.CanHaveNavalNavigationCapability &&
                !x.Party.MobileParty.IsPatrolParty);

            if (source.Any())
            {
                // Simple distribution: give ships to player party if available
                var playerParty = winnerParties.FirstOrDefault(x => x.Party == PartyBase.MainParty);
                if (playerParty != null)
                {
                    int limit = Settings.Instance?.MaxShipsLootPerBattle ?? 25;
                    int shipsGiven = 0;
                    foreach (var ship in mBList)
                    {
                        if (shipsGiven >= limit)
                            break;
                        dictionary[ship] = playerParty;
                        shipsGiven++;
                    }
                }
            }

            return new MBReadOnlyList<KeyValuePair<Ship, MapEventParty>>(dictionary);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int replaced = 0;

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand is float f && f == 0.5f)
                {
                    if (i > 0 && codes[i - 1].opcode == OpCodes.Call)
                    {
                        var method = codes[i - 1].operand as MethodInfo;
                        if (method != null && method.Name == "get_RandomFloat")
                        {
                            codes[i].operand = 1f;
                            replaced++;
                            SubModule.LogMessage($"Transpiler: replaced constant 0.5f with 1f at position {i}");
                        }
                    }
                }
            }

            SubModule.LogMessage($"ShipLootChancePatch Transpiler: replaced {replaced} constants");
            return codes;
        }


        static void Postfix(ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result, MapEvent mapEvent, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            var resultList = new List<KeyValuePair<Ship, MapEventParty>>(__result);
            var playerParty = winnerParties.FirstOrDefault(x => x.Party == PartyBase.MainParty);

            if (playerParty != null)
            {
                int playerShipsBefore = resultList.Count(x => x.Value != null && x.Value.Party == PartyBase.MainParty);
                int playerShipsAdded = 0;
                int limit = Settings.Instance?.MaxShipsLootPerBattle ?? 25;

                foreach (var ship in shipsToLoot)
                {
                    if (playerShipsBefore + playerShipsAdded >= limit)
                        break;

                    if (!resultList.Any(x => x.Key == ship && x.Value != null && x.Value.Party == PartyBase.MainParty))
                    {
                        var existing = resultList.FirstOrDefault(x => x.Key == ship);
                        if (existing.Key != null)
                        {
                            resultList.Remove(existing);
                        }
                        resultList.Add(new KeyValuePair<Ship, MapEventParty>(ship, playerParty));
                        playerShipsAdded++;
                        string context = mapEvent == null ? "Surrender" : "Battle";
                        SubModule.LogMessage($"  Postfix ({context}): Added ship to player (HP: {ship.HitPoints}/{ship.MaxHitPoints})");
                    }
                }

                int playerShipsAfter = resultList.Count(x => x.Value != null && x.Value.Party == PartyBase.MainParty);
                string context2 = mapEvent == null ? "Surrender" : "Battle";
                SubModule.LogMessage($"Result ({context2}): player gets {playerShipsAfter} ships (limit {limit}, was {playerShipsBefore})");
            }

            __result = new MBReadOnlyList<KeyValuePair<Ship, MapEventParty>>(resultList);
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GameComponents.NavalDLCBattleRewardModel), "CalculateShipDamageAfterDefeat")]
    public class ShipDamagePatch
    {
        static bool Prefix(Ship ship, ref float __result)
        {
            float originalHealth = ship.HitPoints;
            __result = 0f;
            SubModule.LogMessage($"CalculateShipDamageAfterDefeat: ship health {originalHealth}/{ship.MaxHitPoints}, damage set to 0");
            return false;
        }
    }

    // Helper class for ship distribution during surrender
    internal static class SurrenderShipDistributor
    {
        public static void DistributeShipsOnSurrender(MobileParty defeatedParty)
        {
            SubModule.LogMessage($"DistributeShipsOnSurrender called for party: {(defeatedParty?.Name?.ToString() ?? "null")}");

            if (defeatedParty?.Ships == null || defeatedParty.Ships.Count == 0)
            {
                SubModule.LogMessage("  No ships to distribute (Ships is null or empty)");
                return;
            }

            SubModule.LogMessage($"  Party has {defeatedParty.Ships.Count} ships");

            if (PartyBase.MainParty == null || !PartyBase.MainParty.IsActive)
            {
                SubModule.LogMessage("  MainParty is null or not active");
                return;
            }

            try
            {
                // Collect ships and apply damage (same as in battle)
                var shipsToDistribute = new MBList<Ship>();
                SubModule.LogMessage($"  Processing {defeatedParty.Ships.Count} ships...");

                foreach (Ship ship in defeatedParty.Ships.ToList())
                {
                    SubModule.LogMessage($"    Ship: {(ship.ShipHull?.Name?.ToString() ?? "Unknown")}, HP: {ship.HitPoints}/{ship.MaxHitPoints}");
                    float damage = Campaign.Current.Models.BattleRewardModel.CalculateShipDamageAfterDefeat(ship);
                    SubModule.LogMessage($"    Damage calculated: {damage}");

                    if (damage > 0)
                    {
                        float modifiedDamage;
                        ship.OnShipDamaged(damage, null, out modifiedDamage);
                        SubModule.LogMessage($"    Ship damaged, new HP: {ship.HitPoints}/{ship.MaxHitPoints}");
                    }

                    if (ship.HitPoints > 0f)
                    {
                        shipsToDistribute.Add(ship);
                        SubModule.LogMessage($"    Ship added to distribution list");
                    }
                    else
                    {
                        SubModule.LogMessage($"    Ship destroyed (HP <= 0)");
                    }
                }

                SubModule.LogMessage($"  Ships to distribute: {shipsToDistribute.Count}");

                if (shipsToDistribute.Count == 0)
                {
                    SubModule.LogMessage("  No ships survived damage, exiting");
                    return;
                }

                // Create MapEventParty for player using reflection
                SubModule.LogMessage("  Creating MapEventParty for player...");
                MapEventParty playerMapEventParty = CreateMapEventParty(PartyBase.MainParty);
                if (playerMapEventParty == null)
                {
                    SubModule.LogMessage("  Failed to create MapEventParty for player, using direct transfer");
                    TransferShipsDirectly(shipsToDistribute);
                    return;
                }

                SubModule.LogMessage("  MapEventParty created successfully");

                // Use the same distribution method as in battle
                var winnerParties = new MBList<MapEventParty> { playerMapEventParty };

                SubModule.LogMessage("  Calling DistributeDefeatedPartyShipsAmongWinners...");
                // Call distribution method (null MapEvent for surrender)
                var distribution = Campaign.Current.Models.BattleRewardModel.DistributeDefeatedPartyShipsAmongWinners(
                    null,
                    shipsToDistribute,
                    winnerParties
                );

                SubModule.LogMessage($"  Distribution result: {distribution.Count} ships");

                // Apply distribution (same as in MapEvent.LootDefeatedPartyShips)
                int shipsGiven = 0;
                foreach (var kvp in distribution)
                {
                    if (kvp.Value != null && kvp.Value.Party == PartyBase.MainParty)
                    {
                        ChangeShipOwnerAction.ApplyByLooting(PartyBase.MainParty, kvp.Key);
                        shipsGiven++;
                        SubModule.LogMessage($"  Ship {shipsGiven} distributed to player (HP: {kvp.Key.HitPoints}/{kvp.Key.MaxHitPoints})");
                    }
                    else if (kvp.Value == null)
                    {
                        SubModule.LogMessage($"  Ship not distributed, destroying (HP: {kvp.Key.HitPoints}/{kvp.Key.MaxHitPoints})");
                        DestroyShipAction.Apply(kvp.Key);
                    }
                }

                SubModule.LogMessage($"  Distribution complete: {shipsGiven} ships given to player");
            }
            catch (Exception ex)
            {
                SubModule.LogMessage($"ERROR in DistributeShipsOnSurrender: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static MapEventParty CreateMapEventParty(PartyBase party)
        {
            try
            {
                var mapEventPartyType = typeof(MapEventParty);
                var constructor = mapEventPartyType.GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[] { typeof(PartyBase) },
                    null
                );

                if (constructor != null)
                {
                    var mapEventParty = (MapEventParty)constructor.Invoke(new object[] { party });
                    return mapEventParty;
                }
            }
            catch (Exception ex)
            {
                SubModule.LogMessage($"Failed to create MapEventParty: {ex.Message}");
            }

            return null;
        }

        private static void TransferShipsDirectly(MBList<Ship> ships)
        {
            int limit = Settings.Instance?.MaxShipsLootPerBattle ?? 25;
            int playerShipCount = PartyBase.MainParty.Ships.Count;
            int shipsToGive = Math.Min(ships.Count, Math.Max(0, limit - playerShipCount));

            for (int i = 0; i < shipsToGive; i++)
            {
                ChangeShipOwnerAction.ApplyByLooting(PartyBase.MainParty, ships[i]);
                SubModule.LogMessage($"Surrender: Ship {i+1}/{shipsToGive} transferred directly to player");
            }

            for (int i = shipsToGive; i < ships.Count; i++)
            {
                DestroyShipAction.Apply(ships[i]);
            }
        }
    }

    // Patch for caravan surrender (without taking prisoners)
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior), "conversation_caravan_surrender_leave_on_consequence")]
    public class CaravanSurrenderPatch
    {
        static void Prefix()
        {
            SubModule.LogMessage("CaravanSurrenderPatch.Prefix called (before method execution)");
        }

        static void Postfix()
        {
            SubModule.LogMessage("CaravanSurrenderPatch.Postfix called");
            if (MobileParty.ConversationParty != null)
            {
                SubModule.LogMessage($"  ConversationParty: {MobileParty.ConversationParty.Name?.ToString() ?? "null"}, IsCaravan: {MobileParty.ConversationParty.IsCaravan}, HasNavalNavigationCapability: {MobileParty.ConversationParty.HasNavalNavigationCapability}, Ships: {MobileParty.ConversationParty.Ships?.Count ?? 0}");

                if (MobileParty.ConversationParty.IsCaravan &&
                    MobileParty.ConversationParty.HasNavalNavigationCapability)
                {
                    SubModule.LogMessage("  Calling DistributeShipsOnSurrender for caravan");
                    SurrenderShipDistributor.DistributeShipsOnSurrender(MobileParty.ConversationParty);
                }
                else
                {
                    SubModule.LogMessage($"  Conditions not met: IsCaravan={MobileParty.ConversationParty.IsCaravan}, HasNavalNavigationCapability={MobileParty.ConversationParty.HasNavalNavigationCapability}");
                }
            }
            else
            {
                SubModule.LogMessage("  ConversationParty is null!");
            }
        }
    }

    // Patch for caravan when player takes prisoners (party is destroyed here)
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior), "conversation_caravan_took_prisoner_on_consequence")]
    public class CaravanPrisonerPatch
    {
        static void Prefix()
        {
            SubModule.LogMessage("CaravanPrisonerPatch.Prefix called (before party destruction)");
            if (PlayerEncounter.EncounteredMobileParty != null)
            {
                var party = PlayerEncounter.EncounteredMobileParty;
                SubModule.LogMessage($"  EncounteredMobileParty: {party.Name?.ToString() ?? "null"}, IsCaravan: {party.IsCaravan}, HasNavalNavigationCapability: {party.HasNavalNavigationCapability}, Ships: {party.Ships?.Count ?? 0}");

                if (party.IsCaravan &&
                    party.HasNavalNavigationCapability)
                {
                    SubModule.LogMessage("  Calling DistributeShipsOnSurrender for caravan (before destruction)");
                    SurrenderShipDistributor.DistributeShipsOnSurrender(party);
                }
            }
            else
            {
                SubModule.LogMessage("  EncounteredMobileParty is null!");
            }
        }
    }

    // Patch for villager/fisherman surrender (without taking prisoners)
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.VillagerCampaignBehavior), "conversation_village_farmer_surrender_leave_on_consequence")]
    public class VillagerSurrenderPatch
    {
        static void Postfix()
        {
            SubModule.LogMessage("VillagerSurrenderPatch.Postfix called");
            if (MobileParty.ConversationParty != null)
            {
                SubModule.LogMessage($"  ConversationParty: {MobileParty.ConversationParty.Name?.ToString() ?? "null"}, IsVillager: {MobileParty.ConversationParty.IsVillager}, HasNavalNavigationCapability: {MobileParty.ConversationParty.HasNavalNavigationCapability}, Ships: {MobileParty.ConversationParty.Ships?.Count ?? 0}");

                if (MobileParty.ConversationParty.IsVillager &&
                    MobileParty.ConversationParty.HasNavalNavigationCapability)
                {
                    SubModule.LogMessage("  Calling DistributeShipsOnSurrender for villager");
                    SurrenderShipDistributor.DistributeShipsOnSurrender(MobileParty.ConversationParty);
                }
            }
            else
            {
                SubModule.LogMessage("  ConversationParty is null!");
            }
        }
    }

    // Patch for villager/fisherman when player takes prisoners (party is destroyed here)
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.VillagerCampaignBehavior), "conversation_village_farmer_took_prisoner_on_consequence")]
    public class VillagerPrisonerPatch
    {
        static void Prefix()
        {
            SubModule.LogMessage("VillagerPrisonerPatch.Prefix called (before party destruction)");
            if (PlayerEncounter.EncounteredParty?.MobileParty != null)
            {
                var party = PlayerEncounter.EncounteredParty.MobileParty;
                SubModule.LogMessage($"  EncounteredParty: {party.Name?.ToString() ?? "null"}, IsVillager: {party.IsVillager}, HasNavalNavigationCapability: {party.HasNavalNavigationCapability}, Ships: {party.Ships?.Count ?? 0}");

                if (party.IsVillager &&
                    party.HasNavalNavigationCapability)
                {
                    SubModule.LogMessage("  Calling DistributeShipsOnSurrender for villager (before destruction)");
                    SurrenderShipDistributor.DistributeShipsOnSurrender(party);
                }
            }
            else
            {
                SubModule.LogMessage("  EncounteredParty is null!");
            }
        }
    }
}

