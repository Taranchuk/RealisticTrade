using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RealisticTrade
{
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryResolveParmsGeneral")]
    public static class IncidentWorker_TraderCaravanArrival_TryResolveParmsGeneral_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms, ref bool __result)
        {
            Core.Log($"TryResolveParmsGeneral: Attempting to resolve trader caravan arrival for map {parms.target}");
            __result = TryResolveParmsGeneral(__instance, parms);
            Core.Log($"TryResolveParmsGeneral: Resolution result: {__result}");
            return false;
        }
        private static bool TryResolveParmsGeneral(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!parms.spawnCenter.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, map, CellFinder.EdgeRoadChance_Neutral))
            {
                return false;
            }
            if (parms.faction == null && !__instance.CandidateFactions(parms).TryRandomElementByWeight(x => GetWeight(map, x), out parms.faction)
                && !__instance.CandidateFactions(parms, desperate: true).TryRandomElementByWeight(x => GetWeight(map, x), out parms.faction))
            {
                return false;
            }
            if (parms.traderKind == null)
            {
                if (!parms.faction.def.caravanTraderKinds.TryRandomElementByWeight((TraderKindDef traderDef) => __instance.TraderKindCommonality(traderDef, map, parms.faction), out parms.traderKind))
                {
                    return false;
                }
            }
            return true;
        }

        public static Dictionary<Faction, Dictionary<Map, FactionWeight>> cachedData = new Dictionary<Faction, Dictionary<Map, FactionWeight>>();
        public class FactionWeight
        {
            public float weight;
            public int updatedTick;
        }

        public static float GetWeight(Map map, Faction faction)
        {
            if (!cachedData.TryGetValue(faction, out var data))
            {
                cachedData[faction] = data = new Dictionary<Map, FactionWeight>();
            }
            if (!data.TryGetValue(map, out var factionWeight))
            {
                data[map] = factionWeight = new FactionWeight();
            }
            if (factionWeight.updatedTick <= 0 || Find.TickManager.TicksGame - factionWeight.updatedTick >= GenDate.TicksPerDay)
            {
                factionWeight.weight = GetWeightInt(map, faction);
                factionWeight.updatedTick = Find.TickManager.TicksGame;
            }
            return factionWeight.weight;
        }

        private static float GetWeightInt(Map map, Faction faction)
        {
            float weight = 1f;
            var settlementsOfFaction = map.GetTradingTracker().FriendlySettlementsNearby().Where(x => x.settlement.Faction == faction).ToList();
            var factionBaseCount = settlementsOfFaction.Count;
            var goodwill = faction.GoodwillWith(map.ParentFaction);
            Core.Log("Checking map " + map + " for faction " + faction);
            Core.Log($"{faction} - Amount of nearby settlements of {faction} in {RealisticTradeMod.settings.maxTravelDistancePeriodForTrading} travel days range is {factionBaseCount}");
            Core.Log($"{faction} - Goodwill of {faction} with {map.ParentFaction} is {goodwill}");

            var factionBaseCountWeight = RealisticTradeMod.settings.factionBaseDensityBonusCurve.Evaluate(factionBaseCount);
            if (RealisticTradeMod.settings.scaleValuesByWorldSize)
            {
                factionBaseCountWeight *= RealisticTradeMod.settings.worldSizeModifiersCurve.Evaluate(Find.World.PlanetCoverage);
            }
            var relationsCountWeight = RealisticTradeMod.settings.relationBonusCurve.Evaluate(goodwill);
            weight *= factionBaseCountWeight * relationsCountWeight;
            string extraMess = "";
            if (settlementsOfFaction.Any())
            {
                var nearestDayTravelDuration = settlementsOfFaction.Select(x => x.daysToArrive).OrderBy(x => x).First();
                Core.Log($"Faction: {faction} - Travel time days from the nearest settlement is {nearestDayTravelDuration}");
                var travelDayWeight = RealisticTradeMod.settings.dayTravelBonusCurve.Evaluate(nearestDayTravelDuration);
                if (RealisticTradeMod.settings.scaleValuesByWorldSize)
                {
                    travelDayWeight *= RealisticTradeMod.settings.worldSizeModifiersCurve.Evaluate(Find.World.PlanetCoverage);
                }
                extraMess += $", travel day factionWeight: {travelDayWeight}";
                weight *= travelDayWeight;
            }
            else
            {
                var travelDayWeight = RealisticTradeMod.settings.dayTravelBonusCurve.Evaluate(RealisticTradeMod.settings.maxTravelDistancePeriodForTrading);
                if (RealisticTradeMod.settings.scaleValuesByWorldSize)
                {
                    travelDayWeight *= RealisticTradeMod.settings.worldSizeModifiersCurve.Evaluate(Find.World.PlanetCoverage);
                }
                extraMess += $"{faction} has no settlement bases around {map}, setting travel day lowest value: {travelDayWeight}";
                weight *= travelDayWeight;
            }
            string logMessage = $"Faction: {faction} - Final Faction trade incident commonality for {faction} is {weight}. Calculated from - faction base count factionWeight: {factionBaseCountWeight}, relation count factionWeight: {relationsCountWeight}";
            Core.Log(logMessage + extraMess);
            return weight;
        }
    }
}
