using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RealisticTrade
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
    [HotSwappable]
    public class TradingTracker : MapComponent
    {
        public Dictionary<Faction, bool> factionsCanArrive = new Dictionary<Faction, bool>();
        public Dictionary<Faction, float> factionSelectionWeight = new Dictionary<Faction, float>();

        private List<(Settlement settlement, float daysToArrive)> friendlySettlementsNearby = new List<(Settlement settlement, float daysToArrive)>();
        private int lastNearbySettlementCheckTick;
        public TradingTracker(Map map) : base(map)
        {

        }

        public async override void MapComponentTick()
        {
            base.MapComponentTick();
            if (map.IsPlayerHome && Find.TickManager.TicksGame % 60000 == 0)
            {
                Core.Log($"TradingTracker: Recalculating friendly settlements for map {map}");
                var result = await Task.Run(() =>
                {
                    return CalculateFriendlySettlementsNearby();
                });
                friendlySettlementsNearby = result;
                lastNearbySettlementCheckTick = Find.TickManager.TicksGame;
                Core.Log($"TradingTracker: Found {result.Count} friendly settlements for map {map}");
            }
        }

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
            if (Core.debug)
            {
                Core.Log($"FINAL_TRADER_PER_YEAR map wealth in {this.map} is {mapWealth}, factionWeight: {RealisticTradeMod.settings.colonyWealthAttractionBonusCurve.Evaluate(mapWealth)}");
                Core.Log($"FINAL_TRADER_PER_YEAR Count of neutral/ally bases (faction relatinship is >=0) around {this.map} is {count}, factionWeight: {RealisticTradeMod.settings.totalSettlementCountBonusCurve.Evaluate(count)}");
                Core.Log($"FINAL_TRADER_PER_YEAR Season is {season}, factionWeight: {RealisticTradeMod.settings.seasonImpactBonusCurve.Evaluate((int)season)}");
            }
            return modifier;
        }
        public List<(Settlement settlement, float daysToArrive)> FriendlySettlementsNearby()
        {
            if (friendlySettlementsNearby != null && lastNearbySettlementCheckTick > 0)
            {
                return friendlySettlementsNearby;
            }
            friendlySettlementsNearby = CalculateFriendlySettlementsNearby();
            lastNearbySettlementCheckTick = Find.TickManager.TicksGame;
            return friendlySettlementsNearby;
        }

        private List<(Settlement settlement, float daysToArrive)> CalculateFriendlySettlementsNearby()
        {
            var result = new List<(Settlement settlement, float daysToArrive)>();
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
                return true;
            };

            var settlements = Find.World.worldObjects.SettlementBases.Where(x => validator(x)).ToList();
            int maxDist = Mathf.CeilToInt(RealisticTradeMod.settings.maxTravelDistancePeriodForTrading * 60000f / 3300f);
            Core.Log($"CalculateFriendlySettlementsNearby: Found {settlements.Count} potential settlements, max distance: {maxDist} tiles");

            foreach (var settlement in settlements)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(map.Tile, settlement.Tile) > maxDist)
                {
                    continue;
                }

                int traversalDistance = Find.WorldGrid.TraversalDistanceBetween(settlement.Tile, map.Tile, passImpassable: true, maxDist: maxDist);
                if (traversalDistance != int.MaxValue && traversalDistance > 0)
                {
                    float daysToArrive = traversalDistance * 3300f / 60000f;
                    if (daysToArrive > 0 && daysToArrive <= RealisticTradeMod.settings.maxTravelDistancePeriodForTrading)
                    {
                        result.Add((settlement, daysToArrive));
                    }
                }
            }
            Core.Log($"CalculateFriendlySettlementsNearby: Found {result.Count} settlements within travel range for map {map}");
            return result;
        }
    }
}
