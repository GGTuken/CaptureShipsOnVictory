using HarmonyLib;
using TaleWorlds.MountAndBlade;
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
        private const float NEW_SHIP_LOOT_CHANCE = 1f;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand is float f && f == 0.5f)
                {
                    codes[i].operand = NEW_SHIP_LOOT_CHANCE;
                }
            }

            return codes;
        }
    }
}

