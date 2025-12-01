using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
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
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            _harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GameComponents.NavalDLCBattleRewardModel), "DistributeDefeatedPartyShipsAmongWinners")]
    public class ShipLootChancePatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

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
                        }
                    }
                }
            }

            return codes;
        }

        static void Postfix(ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            var resultList = new List<KeyValuePair<Ship, MapEventParty>>(__result);
            var playerParty = winnerParties.FirstOrDefault(x => x.Party == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty);

            if (playerParty != null)
            {
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
                    }
                }
            }

            __result = new MBReadOnlyList<KeyValuePair<Ship, MapEventParty>>(resultList);
        }
    }

    [HarmonyPatch(typeof(NavalDLC.GameComponents.NavalDLCBattleRewardModel), "CalculateShipDamageAfterDefeat")]
    public class ShipDamagePatch
    {
        static bool Prefix(ref float __result)
        {
            __result = 0f;
            return false;
        }
    }
}

