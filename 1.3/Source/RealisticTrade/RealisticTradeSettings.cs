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
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxTravelDistancePeriodForTrading, "maxTravelDistancePeriodForTrading", 7);
            Scribe_Collections.Look(ref relationBonus, "relationBonus");
            Scribe_Collections.Look(ref factionBaseCountBonus, "factionBaseCountBonus");
            RebuildRelationBonusCurve();
            RebuildFactionBaseCountBonusCurve();
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
            listingStandard.End();
            Widgets.EndScrollView();
            RebuildRelationBonusCurve();
            RebuildFactionBaseCountBonusCurve();
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
        private static Vector2 scrollPosition = Vector2.zero;
    }
}

