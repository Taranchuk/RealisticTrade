using HarmonyLib;
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
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var baseIncidentsPerYearField = AccessTools.Field(typeof(StorytellerCompProperties_FactionInteraction), "baseIncidentsPerYear");
            foreach (CodeInstruction code in instructions)
            {
                yield return code;
                if (code.LoadsField(baseIncidentsPerYearField))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StorytellerComp_FactionInteraction_Patch), "GetIncidentCountPerYearModifier"));
                    yield return new CodeInstruction(OpCodes.Mul);
                }
            }
        }

        public static float GetIncidentCountPerYearModifier(StorytellerComp_FactionInteraction instance, Map target)
        {
            if (target != null && instance.Props.incident == IncidentDefOf.TraderCaravanArrival)
            {
                var count = target.GetTradingTracker().FriendlySettlementsNearby().Count;
                var modifier = RealisticTradeMod.settings.factionBaseCountBonusCurve.Evaluate(count);
                Log.Message($"Count of neutral/ally bases around {target} is {count}");
                Log.Message($"Base incident count per year is {instance.Props.baseIncidentsPerYear}, now it's {instance.Props.baseIncidentsPerYear * modifier}");
                return modifier;
            }
            return 1f; // we keep it as is so we don't touch the base value
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TraderKindCommonality")]
    public static class TraderKindCommonality_Patch
    {
        public static void Postfix(TraderKindDef traderKind, Map map, Faction faction, ref float __result)
        {
            var settlements = map.GetTradingTracker().FriendlySettlementsNearby();
            var factionBaseCount = settlements.Count(x => x.Faction == faction);
            var goodwill = faction.GoodwillWith(map.ParentFaction);
            Log.Message($"Base trader kind commonality is {__result} for {faction}");
            Log.Message($"Amount of nearby settlement of {faction} is {factionBaseCount}");
            Log.Message($"Goodwill of {faction} with {map.ParentFaction} is {goodwill}");
            var factionBaseCountWeight = RealisticTradeMod.settings.factionBaseCountBonusCurve.Evaluate(factionBaseCount);
            var relationsCountWeight = RealisticTradeMod.settings.relationBonusCurve.Evaluate(goodwill);
            __result *= factionBaseCountWeight * relationsCountWeight;
            Log.Message($"Modified trader kind commonality for {faction} is {__result} now. Faction base count weight: {factionBaseCountWeight}, relation count weight: {relationsCountWeight}");
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
        public List<Settlement> FriendlySettlementsNearby()
        {
            if (lastNearbySettlementCheckTick + 15000 > Find.TickManager.TicksGame || lastNearbySettlementCheckTick <= 0)
            {
                friendlySettlementsNearby = new List<Settlement>();
                Predicate<Settlement> validator = delegate (Settlement x)
                {
                    Log.ResetMessageCount();
                    if (x.Faction.HostileTo(map.ParentFaction))
                    {
                        Log.Message($"{x} is hostile, can't send traders");
                        return false;
                    }
                    var daysToArrive = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(x.Tile, map.Tile, null) / 60000f;
                    Log.Message($"Estimated days to arrive from {x} to {map} is {daysToArrive}");
                    if (daysToArrive > RealisticTradeMod.settings.maxTravelDistancePeriodForTrading)
                    {
                        Log.Message($"{x} can't send traders, too far");
                        return false;
                    }
                    Log.Message($"{x} can send traders, is close");
                    return true;
                };
                friendlySettlementsNearby = Find.World.worldObjects.SettlementBases.Where(x => validator(x)).ToList();
                lastNearbySettlementCheckTick = Find.TickManager.TicksGame;
            }
            return friendlySettlementsNearby;
        }
    }
}
