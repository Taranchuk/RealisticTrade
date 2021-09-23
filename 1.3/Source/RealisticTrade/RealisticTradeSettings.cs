using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RealisticTrade
{
    class RealisticTradeSettings : ModSettings
    {
        public int maxTravelDistancePeriodForTrading = 7;

        private Dictionary<float, float> relationBonus = new Dictionary<float, float>();
        public SimpleCurve relationBonusCurve;

        private Dictionary<int, float> factionBaseCountBonus = new Dictionary<int, float>();
        public SimpleCurve factionBaseCountBonusCurve;

        private Dictionary<float, float> dayTravelBonus = new Dictionary<float, float>();
        public SimpleCurve dayTravelBonusCurve;

        private Dictionary<int, float> seasonImpactBonus = new Dictionary<int, float>();
        public SimpleCurve seasonImpactBonusCurve;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxTravelDistancePeriodForTrading, "maxTravelDistancePeriodForTrading", 7);
            Scribe_Collections.Look(ref relationBonus, "relationBonus");
            Scribe_Collections.Look(ref factionBaseCountBonus, "factionBaseCountBonus");
            Scribe_Collections.Look(ref dayTravelBonus, "dayTravelBonus");
            Scribe_Collections.Look(ref seasonImpactBonus, "seasonImpactBonus");
            RebuildRelationBonusCurve();
            RebuildFactionBaseCountBonusCurve();
            RebuildDayTravelBonusCurve();
            RebuildSeasonImpactBonusCurve();
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect rect2 = new Rect(0f, 0f, inRect.width - 30f, 1050);
            Widgets.BeginScrollView(rect, ref scrollPosition, rect2, true);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(rect2);
            listingStandard.SliderLabeled("RT.MaxTravelPeriodForNPCCaravans".Translate(maxTravelDistancePeriodForTrading), ref maxTravelDistancePeriodForTrading,
                "PeriodDays".Translate(maxTravelDistancePeriodForTrading), 1, 60);
            listingStandard.GapLine();

            listingStandard.Label("RT.FactionTraderSpawnChanceWeightsPerRelation".Translate());
            foreach (var dictKey in relationBonus.Keys.ToList())
            {
                var value = relationBonus[dictKey];
                listingStandard.SliderLabeled("RT.FactionSpawnChancePerRelation".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                relationBonus[dictKey] = value;
            }
            listingStandard.GapLine();
            listingStandard.Label("RT.FactionTraderSpawnChanceWeightsPerBaseCount".Translate(maxTravelDistancePeriodForTrading));
            foreach (var dictKey in factionBaseCountBonus.Keys.ToList())
            {
                var value = factionBaseCountBonus[dictKey];
                listingStandard.SliderLabeled("RT.FactionSpawnChancePerBaseCount".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                factionBaseCountBonus[dictKey] = value;
            }
            listingStandard.GapLine();
            listingStandard.Label("RT.FactionTraderSpawnChanceWeightsPerTravelDayCount".Translate());
            foreach (var dictKey in dayTravelBonus.Keys.ToList())
            {
                var value = dayTravelBonus[dictKey];
                listingStandard.SliderLabeled("RT.FactionSpawnChancePerTravelDayCount".Translate("PeriodDays".Translate(dictKey)), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                dayTravelBonus[dictKey] = value;
            }

            listingStandard.GapLine();
            listingStandard.Label("RT.FactionTraderSpawnChanceWeightsPerSeasonImpact".Translate());
            foreach (var dictKey in seasonImpactBonus.Keys.ToList())
            {
                var value = seasonImpactBonus[dictKey];
                listingStandard.SliderLabeled("RT.FactionSpawnChancePerSeasonImpact".Translate(((Season)dictKey).Label()), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                seasonImpactBonus[dictKey] = value;
            }
            listingStandard.End();
            Widgets.EndScrollView();
            RebuildRelationBonusCurve();
            RebuildFactionBaseCountBonusCurve();
            RebuildDayTravelBonusCurve();
            RebuildSeasonImpactBonusCurve();
            base.Write();
        }

        public void InitRelationBonus()
        {
            relationBonus = new Dictionary<float, float>()
                {
                    {0f, 1f},
                    {10f, 1.1f},
                    {20f, 1.2f},
                    {30f, 1.3f},
                    {40f, 1.4f},
                    {50f, 1.6f},
                    {60f, 1.8f},
                    {70f, 2f},
                    {80f, 2.4f},
                    {90f, 2.8f},
                    {100f, 3f},
                };
        }
        public void RebuildRelationBonusCurve()
        {
            if (relationBonus is null || relationBonus.Count == 0)
            {
                InitRelationBonus();
            }
            relationBonusCurve = new SimpleCurve();
            foreach (var dictKey in relationBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = relationBonus[dictKey];
                relationBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }
        public void InitFactionBaseCountBonus()
        {
            factionBaseCountBonus = new Dictionary<int, float>()
                {
                    {0, 0.1f},
                    {1, 0.25f},
                    {5, 1f},
                    {10, 2f},
                    {20, 3f},
                    {50, 5f},
                };
        }
        public void RebuildFactionBaseCountBonusCurve()
        {
            if (factionBaseCountBonus is null || factionBaseCountBonus.Count == 0)
            {
                InitFactionBaseCountBonus();
            }
            factionBaseCountBonusCurve = new SimpleCurve();
            foreach (var dictKey in factionBaseCountBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = factionBaseCountBonus[dictKey];
                factionBaseCountBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }

        public void InitDayTravelBonus()
        {
            dayTravelBonus = new Dictionary<float, float>()
                {
                    {1f, 1.5f},
                    {2f, 1.2f},
                    {3f, 1f},
                    {4f, 0.8f},
                    {6f, 0.6f},
                    {7f, 0.4f},
                };
        }
        public void RebuildDayTravelBonusCurve()
        {
            if (dayTravelBonus is null || dayTravelBonus.Count == 0)
            {
                InitDayTravelBonus();
            }
            dayTravelBonusCurve = new SimpleCurve();
            foreach (var dictKey in dayTravelBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = dayTravelBonus[dictKey];
                dayTravelBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }
        public void InitSeasonImpactBonus()
        {
            seasonImpactBonus = new Dictionary<int, float>()
                {
                    {1, 0.5f},
                    {2, 1f},
                    {3, 0.5f},
                    {4, 0.25f},
                    {5, 1f},
                    {6, 0.25f},
                };
        }
        public void RebuildSeasonImpactBonusCurve()
        {
            if (seasonImpactBonus is null || seasonImpactBonus.Count == 0)
            {
                InitSeasonImpactBonus();
            }
            seasonImpactBonusCurve = new SimpleCurve();
            foreach (var dictKey in seasonImpactBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = seasonImpactBonus[dictKey];
                seasonImpactBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }

        private static Vector2 scrollPosition = Vector2.zero;
    }
}

