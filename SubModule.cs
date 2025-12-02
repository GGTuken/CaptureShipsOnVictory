using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.MapEvents;
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
            // try
            // {
            //     string logPath = Path.Combine(BasePath.Name, "Modules", "CaptureShipsOnVictory", "mod_log.txt");
            //     File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            // }
            // catch { }
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GameComponents.NavalDLCBattleRewardModel), "DistributeDefeatedPartyShipsAmongWinners")]
    public class ShipLootChancePatch
    {
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

        static void Prefix(MBReadOnlyList<Ship> shipsToLoot)
        {
            SubModule.LogMessage($"DistributeDefeatedPartyShipsAmongWinners called: {shipsToLoot.Count} ships to loot");
            for (int i = 0; i < shipsToLoot.Count; i++)
            {
                var ship = shipsToLoot[i];
                SubModule.LogMessage($"  Ship {i}: HP {ship.HitPoints}/{ship.MaxHitPoints}");
            }
        }

        static void Postfix(ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            var resultList = new List<KeyValuePair<Ship, MapEventParty>>(__result);
            var playerParty = winnerParties.FirstOrDefault(x => x.Party == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty);

            if (playerParty != null)
            {
                int playerShipsBefore = resultList.Count(x => x.Value != null && x.Value.Party == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty);

                foreach (var ship in shipsToLoot)
                {
                    if (!resultList.Any(x => x.Key == ship && x.Value != null && x.Value.Party == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty))
                    {
                        var existing = resultList.FirstOrDefault(x => x.Key == ship);
                        if (existing.Key != null)
                        {
                            resultList.Remove(existing);
                        }
                        resultList.Add(new KeyValuePair<Ship, MapEventParty>(ship, playerParty));
                        SubModule.LogMessage($"  Postfix: Added ship to player (HP: {ship.HitPoints}/{ship.MaxHitPoints})");
                    }
                }

                int playerShipsAfter = resultList.Count(x => x.Value != null && x.Value.Party == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty);
                SubModule.LogMessage($"Result: player gets {playerShipsAfter} ships (was {playerShipsBefore})");
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
}

