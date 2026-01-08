using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticTrade
{
    [HarmonyPatch(typeof(StorytellerComp), "IncidentChanceFinal")]
    public static class StorytellerComp_IncidentChanceFinal_Patch
    {
        public static void Postfix(IncidentDef def, ref float __result)
        {
            if (def == IncidentDefOf.TraderCaravanArrival)
            {
                var mainMap = Find.RandomPlayerHomeMap;
                var oldValue = __result;
                __result *= mainMap.GetTradingTracker().GetTradeIncidentSpawnOrCountModifier();
                Core.Log($"FINAL_TRADER_PER_YEAR Base chance of trader arrival is {oldValue} per year, final modified chance is {__result}");
            }
        }
    }
}
