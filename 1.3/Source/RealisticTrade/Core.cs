﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RealisticTrade
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            var harmony = new Harmony("RealisticTrade.Mod");
            harmony.PatchAll();
        }

        private static Dictionary<Map, TradingTracker> cachedTrackers = new Dictionary<Map, TradingTracker>();

        public static TradingTracker GetTradingTracker(this Map map)
        {
            if (!cachedTrackers.TryGetValue(map, out var tracker))
            {
                cachedTrackers[map] = tracker = map.GetComponent<TradingTracker>();
            }
            return tracker;
        }
    }
    [HarmonyPatch(typeof(StorytellerComp), "IncidentChanceFinal")]
    public static class IncidentChanceFinal_Patch
    {
        public static void Postfix(IncidentDef def, ref float __result)
        {
            if (def == IncidentDefOf.TraderCaravanArrival)
            {
                var mainMap = Find.RandomPlayerHomeMap;
                var oldValue = __result;
                __result *= mainMap.GetTradingTracker().GetTradeIncidentSpawnOrCountModifier();
                Log.Message($"FINAL_TRADER_PER_YEAR Base chance of trader arrival is {oldValue} per year, final modified chance is {__result}");
            }
        }
    }

    [HarmonyPatch]
    public static class StorytellerComp_FactionInteraction_Patch
    {
        public static MethodBase TargetMethod()
        {
            foreach (Type type in typeof(StorytellerComp_FactionInteraction).GetNestedTypes(AccessTools.all))
            {
                if (type.Name.Contains("MakeIntervalIncidents"))
                {
                    return AccessTools.Method(type, "MoveNext", null, null);
                }
            }
            return null;
        }

        public static float storeValue;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var baseIncidentsPerYearField = AccessTools.Field(typeof(StorytellerCompProperties_FactionInteraction), "baseIncidentsPerYear");
            var minSpacingDaysField = AccessTools.Field(typeof(StorytellerCompProperties_FactionInteraction), "minSpacingDays");
            var storeValueField = AccessTools.Field(typeof(StorytellerComp_FactionInteraction_Patch), "storeValue");

            foreach (CodeInstruction code in instructions)
            {
                yield return code;
                if (code.opcode == OpCodes.Stloc_2)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StorytellerComp_FactionInteraction_Patch), "GetIncidentCountPerYearModifier"));
                    yield return new CodeInstruction(OpCodes.Stsfld, storeValueField);
                }
                if (code.LoadsField(baseIncidentsPerYearField))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, storeValueField);
                    yield return new CodeInstruction(OpCodes.Mul);
                }
                if (code.LoadsField(minSpacingDaysField))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, storeValueField);
                    yield return new CodeInstruction(OpCodes.Div);
                }
            }
        }

        public static float GetIncidentCountPerYearModifier(StorytellerComp_FactionInteraction instance, Map target)
        {
            if (target != null && instance.Props.incident == IncidentDefOf.TraderCaravanArrival)
            {
                var modifier = target.GetTradingTracker().GetTradeIncidentSpawnOrCountModifier();
                Log.Message($"FINAL_TRADER_PER_YEAR Base incident count per year is {instance.Props.baseIncidentsPerYear}, now it's {instance.Props.baseIncidentsPerYear * modifier}");
                return modifier;
            }
            return 1f; // we keep it as is so we don't touch the base value
        }

    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryResolveParmsGeneral")]
    public static class TryResolveParmsGeneral_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms, ref bool __result)
        {
            __result = TryResolveParmsGeneral(__instance, parms);
            return false;
        }
        private static bool TryResolveParmsGeneral(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!parms.spawnCenter.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, map, CellFinder.EdgeRoadChance_Neutral))
            {
                return false;
            }
            foreach (var fac in __instance.CandidateFactions(map))
            {
                Log.Message("Candidate: " + fac + " - " + fac.def);
            }
            if (parms.faction == null && !__instance.CandidateFactions(map).TryRandomElementByWeight(x => GetWeight(map, x), out parms.faction) 
                && !__instance.CandidateFactions(map, desperate: true).TryRandomElementByWeight(x => GetWeight(map, x), out parms.faction))
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

        public static float GetWeight(Map map, Faction faction)
        {
            float weight = 1f;
            var settlementsOfFaction = map.GetTradingTracker().FriendlySettlementsNearby().Where(x => x.Faction == faction).ToList();
            var factionBaseCount = settlementsOfFaction.Count;
            var goodwill = faction.GoodwillWith(map.ParentFaction);

            Log.Message($"Faction: {faction} - Amount of nearby settlements of {faction} in {RealisticTradeMod.settings.maxTravelDistancePeriodForTrading} travel days range is {factionBaseCount}");
            Log.Message($"Faction: {faction} - Goodwill of {faction} with {map.ParentFaction} is {goodwill}");

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
                var nearestDayTravelDuration = settlementsOfFaction.Select(x => CaravanArrivalTimeEstimator.EstimatedTicksToArrive(x.Tile, map.Tile, null) / 60000f).OrderBy(x => x).First();
                Log.Message($"Faction: {faction} - Travel time days from the nearest settlement is {nearestDayTravelDuration}");
                var travelDayWeight = RealisticTradeMod.settings.dayTravelBonusCurve.Evaluate(nearestDayTravelDuration);
                if (RealisticTradeMod.settings.scaleValuesByWorldSize)
                {
                    travelDayWeight *= RealisticTradeMod.settings.worldSizeModifiersCurve.Evaluate(Find.World.PlanetCoverage);
                }

                extraMess += $", travel day weight: {travelDayWeight}";
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
            string logMessage = $"Faction: {faction} - Final Faction trade incident commonality for {faction} is {weight}. Calculated from - faction base count weight: {factionBaseCountWeight}, relation count weight: {relationsCountWeight}";
            Log.Message(logMessage + extraMess);
            return weight;
        }
    }

    public class TradingTracker : MapComponent
    {
        public Dictionary<Faction, bool> factionsCanArrive = new Dictionary<Faction, bool>();
        public Dictionary<Faction, float> factionSelectionWeight = new Dictionary<Faction, float>();

        private List<Settlement> friendlySettlementsNearby = new List<Settlement>();
        private int lastNearbySettlementCheckTick;
        public TradingTracker(Map map) : base(map)
        {

        }

        //public override void MapComponentTick()
        //{
        //    base.MapComponentTick();
        //    for (var i = 0; i < 1; i++)
        //    {
        //        Find.Storyteller.incidentQueue.IncidentQueueTick();
        //        foreach (FiringIncident item in Find.Storyteller.MakeIncidentsForInterval())
        //        {
        //            Find.Storyteller.TryFire(item);
        //        }
        //    }
        //}
        public float GetTradeIncidentSpawnOrCountModifier()
        {
            var count = this.FriendlySettlementsNearby().Count;
            var season = GenLocalDate.Season(map.Tile);
            var mapWealth = this.map.wealthWatcher.WealthTotal;

            var modifier = RealisticTradeMod.settings.totalSettlementCountBonusCurve.Evaluate(count);
            if (RealisticTradeMod.settings.scaleValuesByWorldSize)
            {
                modifier *= RealisticTradeMod.settings.worldSizeModifiersCurve.Evaluate(Find.World.PlanetCoverage);
            }
            modifier *= RealisticTradeMod.settings.seasonImpactBonusCurve.Evaluate((int)season);
            modifier *= RealisticTradeMod.settings.colonyWealthAttractionBonusCurve.Evaluate(mapWealth);

            Log.Message($"FINAL_TRADER_PER_YEAR map wealth in {this.map} is {mapWealth}, weight: {RealisticTradeMod.settings.colonyWealthAttractionBonusCurve.Evaluate(mapWealth)}");
            Log.Message($"FINAL_TRADER_PER_YEAR Count of neutral/ally bases (faction relatinship is >=0) around {this.map} is {count}, weight: {RealisticTradeMod.settings.totalSettlementCountBonusCurve.Evaluate(count)}");
            Log.Message($"FINAL_TRADER_PER_YEAR Season is {season}, weight: {RealisticTradeMod.settings.seasonImpactBonusCurve.Evaluate((int)season)}");
            return modifier;
        }
        public List<Settlement> FriendlySettlementsNearby()
        {
            if (Find.TickManager.TicksGame > lastNearbySettlementCheckTick + GenDate.TicksPerDay || lastNearbySettlementCheckTick <= 0)
            {
                friendlySettlementsNearby = new List<Settlement>();
                Predicate<Settlement> validator = delegate (Settlement x)
                {
                    if (x.Faction == map.ParentFaction)
                    {
                        return false;
                    }
                    if (x.Faction.HostileTo(map.ParentFaction))
                    {
                        return false;
                    }
                    var daysToArrive = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(x.Tile, map.Tile, null) / 60000f;
                    if (daysToArrive > RealisticTradeMod.settings.maxTravelDistancePeriodForTrading)
                    {
                        return false;
                    }
                    Log.Message("Settlement: " + x);
                    return true;
                };

                friendlySettlementsNearby = Find.World.worldObjects.SettlementBases.Where(x => validator(x)).ToList();
                lastNearbySettlementCheckTick = Find.TickManager.TicksGame;
            }
            return friendlySettlementsNearby;
        }
    }
}
