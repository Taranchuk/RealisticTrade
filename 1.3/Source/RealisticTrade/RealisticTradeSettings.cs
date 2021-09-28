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
        public bool scaleValuesByWorldSize;

        private Dictionary<float, float> relationBonus = new Dictionary<float, float>();
        public SimpleCurve relationBonusCurve;

        private Dictionary<int, float> totalSettlementCountBonus = new Dictionary<int, float>();
        public SimpleCurve totalSettlementCountBonusCurve;

        private Dictionary<int, float> factionBaseDensityBonus = new Dictionary<int, float>();
        public SimpleCurve factionBaseDensityBonusCurve;

        private Dictionary<float, float> dayTravelBonus = new Dictionary<float, float>();
        public SimpleCurve dayTravelBonusCurve;

        private Dictionary<int, float> seasonImpactBonus = new Dictionary<int, float>();
        public SimpleCurve seasonImpactBonusCurve;

        private Dictionary<int, float> colonyWealthAttractionBonus = new Dictionary<int, float>();
        public SimpleCurve colonyWealthAttractionBonusCurve;

        private Dictionary<float, float> worldSizeModifiers = new Dictionary<float, float>();
        public SimpleCurve worldSizeModifiersCurve;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxTravelDistancePeriodForTrading, "maxTravelDistancePeriodForTrading", 7);
            Scribe_Values.Look(ref scaleValuesByWorldSize, "scaleValuesByWorldSize");
            Scribe_Collections.Look(ref relationBonus, "relationBonus");
            Scribe_Collections.Look(ref totalSettlementCountBonus, "totalSettlementCountBonus");
            Scribe_Collections.Look(ref factionBaseDensityBonus, "factionBaseDensityBonus");
            Scribe_Collections.Look(ref dayTravelBonus, "dayTravelBonus");
            Scribe_Collections.Look(ref seasonImpactBonus, "seasonImpactBonus");
            Scribe_Collections.Look(ref colonyWealthAttractionBonus, "colonyWealthAttractionBonus");
            ReInitValues();
        }

        public void ReInitValues()
        {
            RebuildRelationBonusCurve();
            RebuildTotalSettlementCountBonusCurve();
            RebuildFactionBaseDensityBonusCurve();
            RebuildDayTravelBonusCurve();
            RebuildSeasonImpactBonusCurve();
            RebuildColonyWealthAttractionBonusCurve();
            RebuildWorldSizeModifiersBonusCurve();
        }

        private const float buttonSize = 24;
        private const float shortGapLineSize = 8;
        private const float gapLineSize = 12;

        private float ButtonsSize(float count) => buttonSize * count;
        private float ShortGapLinesSize(float count) => shortGapLineSize * count;
        private float GapLinesSize(float count) => gapLineSize * count;
        public float GetTotalHeightAndSizes(out float generalSectionSize, out float tradeIncidentSpawnChanceSectionSize, out float tradeFactionSpawnChanceModifiersSectionSize)
        {
            generalSectionSize = ShortGapLinesSize(1) + ButtonsSize(4) + GapLinesSize(1);
            if (scaleValuesByWorldSize)
            {
                generalSectionSize += GapLinesSize(1) + ButtonsSize(worldSizeModifiers.Keys.Count);
            }

            tradeIncidentSpawnChanceSectionSize = ShortGapLinesSize(1);
            if (Find.World != null)
            {
                tradeIncidentSpawnChanceSectionSize += ButtonsSize(2);
            }
            tradeIncidentSpawnChanceSectionSize += ButtonsSize(4) + GapLinesSize(2)
                + ButtonsSize(colonyWealthAttractionBonus.Keys.Count) 
                + ButtonsSize(seasonImpactBonus.Keys.Count) 
                + ButtonsSize(totalSettlementCountBonus.Keys.Count);

            tradeFactionSpawnChanceModifiersSectionSize = ShortGapLinesSize(1) + ButtonsSize(4) + GapLinesSize(2)
                + ButtonsSize(relationBonus.Keys.Count)
                + ButtonsSize(factionBaseDensityBonus.Keys.Count)
                + ButtonsSize(dayTravelBonus.Keys.Count);

            return generalSectionSize + gapLineSize + tradeIncidentSpawnChanceSectionSize + gapLineSize + tradeFactionSpawnChanceModifiersSectionSize + 30;
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            ReInitValues();
            var totalHeight = GetTotalHeightAndSizes(out float generalSectionSize, out float tradeIncidentSpawnChanceSectionSize, out float tradeFactionSpawnChanceModifiersSectionSize);
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect rect2 = new Rect(0f, 0f, inRect.width - 30f, totalHeight);
            Widgets.BeginScrollView(rect, ref scrollPosition, rect2, true);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(rect2);

            var generalSection = listingStandard.BeginSection(generalSectionSize);
            generalSection.Label("RT.GeneralSettings".Translate());
            generalSection.GapLine(8);
            if (generalSection.ButtonTextLabeled("RT.ResetValues".Translate(), "Default".Translate()))
            {
                maxTravelDistancePeriodForTrading = 7;
                scaleValuesByWorldSize = false;
                relationBonus = null;
                totalSettlementCountBonus = null;
                factionBaseDensityBonus = null;
                dayTravelBonus = null;
                seasonImpactBonus = null;
                colonyWealthAttractionBonus = null;
                worldSizeModifiers = null;
                ReInitValues();
            }
            generalSection.SliderLabeled("RT.MaxTravelPeriodForNPCCaravans".Translate(maxTravelDistancePeriodForTrading), ref maxTravelDistancePeriodForTrading,
            "PeriodDays".Translate(maxTravelDistancePeriodForTrading), 1, 60);
            generalSection.CheckboxLabeled("RT.ScaleValuesByWorldCoverage".Translate(), ref scaleValuesByWorldSize);
            if (scaleValuesByWorldSize)
            {
                generalSection.GapLine();
                foreach (var dictKey in worldSizeModifiers.Keys.ToList())
                {
                    var value = worldSizeModifiers[dictKey];
                    generalSection.SliderLabeled("RT.WorldSize".Translate((dictKey * 100f).ToStringDecimalIfSmall() + "%"), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                    worldSizeModifiers[dictKey] = value;
                }
            }
            listingStandard.EndSection(generalSection);
            listingStandard.Gap();
            var tradeIncidentSpawnChanceSection = listingStandard.BeginSection(tradeIncidentSpawnChanceSectionSize);
            if (Find.World != null)
            {
                var incidentCount = GetBaseTradeIncidentCount();
                if (incidentCount.HasValue)
                {
                    tradeIncidentSpawnChanceSection.Label("RT.BaseTradeIncidentCountPerYear".Translate(Find.Storyteller.def.LabelCap, incidentCount.Value));
                    tradeIncidentSpawnChanceSection.Label("RT.FinalTradeIncidentCountPerYear".Translate(Find.Storyteller.def.LabelCap, GetFinalTradeIncidentCount().Value));
                }
                else
                {
                    var incidentChance = GetBaseTradeIncidentChance();
                    tradeIncidentSpawnChanceSection.Label("RT.BaseTradeIncidentSpawnChance".Translate(Find.Storyteller.def.LabelCap, incidentChance));
                    tradeIncidentSpawnChanceSection.Label("RT.FinalTradeIncidentSpawnChance".Translate(Find.Storyteller.def.LabelCap, GetFinalTradeIncidentChance()));
                }
            }
            tradeIncidentSpawnChanceSection.Label("RT.TradeIncidentSpawnChanceModifiers".Translate());
            tradeIncidentSpawnChanceSection.GapLine(8);
            
            tradeIncidentSpawnChanceSection.Label("RT.FactionTraderSpawnChanceWeightsPerColonyWealth".Translate());
            foreach (var dictKey in colonyWealthAttractionBonus.Keys.ToList())
            {
                var value = colonyWealthAttractionBonus[dictKey];
                tradeIncidentSpawnChanceSection.SliderLabeled("RT.FactionSpawnChancePerColonyWealth".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                colonyWealthAttractionBonus[dictKey] = value;
            }
            
            tradeIncidentSpawnChanceSection.GapLine();
            
            tradeIncidentSpawnChanceSection.Label("RT.FactionTraderSpawnChanceWeightsPerSeasonImpact".Translate());
            foreach (var dictKey in seasonImpactBonus.Keys.ToList())
            {
                var value = seasonImpactBonus[dictKey];
                tradeIncidentSpawnChanceSection.SliderLabeled("RT.FactionSpawnChancePerSeasonImpact".Translate(((Season)dictKey).Label()), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                seasonImpactBonus[dictKey] = value;
            }
            tradeIncidentSpawnChanceSection.GapLine();
            tradeIncidentSpawnChanceSection.Label("RT.FactionTraderSpawnChanceWeightsPerBaseCount".Translate(maxTravelDistancePeriodForTrading));
            foreach (var dictKey in totalSettlementCountBonus.Keys.ToList())
            {
                var value = totalSettlementCountBonus[dictKey];
                tradeIncidentSpawnChanceSection.SliderLabeled("RT.FactionSpawnChancePerBaseCount".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                totalSettlementCountBonus[dictKey] = value;
            }
            listingStandard.EndSection(tradeIncidentSpawnChanceSection);
            
            listingStandard.Gap();
            
            var tradeFactionSpawnChanceModifiersSection = listingStandard.BeginSection(tradeFactionSpawnChanceModifiersSectionSize);
            tradeFactionSpawnChanceModifiersSection.Label("RT.TradeFactionSpawnChanceModifiers".Translate());

            tradeFactionSpawnChanceModifiersSection.GapLine(8);
            tradeFactionSpawnChanceModifiersSection.Label("RT.FactionTraderSpawnChanceWeightsPerRelation".Translate());
            foreach (var dictKey in relationBonus.Keys.ToList())
            {
                var value = relationBonus[dictKey];
                tradeFactionSpawnChanceModifiersSection.SliderLabeled("RT.FactionSpawnChancePerRelation".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                relationBonus[dictKey] = value;
            }
            
            tradeFactionSpawnChanceModifiersSection.GapLine();
            tradeFactionSpawnChanceModifiersSection.Label("RT.FactionTraderSpawnChanceWeightsPerFactionBaseDensity".Translate(maxTravelDistancePeriodForTrading));
            foreach (var dictKey in factionBaseDensityBonus.Keys.ToList())
            {
                var value = factionBaseDensityBonus[dictKey];
                tradeFactionSpawnChanceModifiersSection.SliderLabeled("RT.FactionSpawnChancePerFactionBaseDensity".Translate(dictKey), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                factionBaseDensityBonus[dictKey] = value;
            }
            
            tradeFactionSpawnChanceModifiersSection.GapLine();
            tradeFactionSpawnChanceModifiersSection.Label("RT.FactionTraderSpawnChanceWeightsPerTravelDayCount".Translate());
            foreach (var dictKey in dayTravelBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = dayTravelBonus[dictKey];
                tradeFactionSpawnChanceModifiersSection.SliderLabeled("RT.FactionSpawnChancePerTravelDayCount".Translate("PeriodDays".Translate(dictKey)), ref value, (value * 100f).ToStringDecimalIfSmall() + "%", 0f, 5f);
                dayTravelBonus[dictKey] = value;
            }
            listingStandard.EndSection(tradeFactionSpawnChanceModifiersSection);

            listingStandard.End();
            Widgets.EndScrollView();
            base.Write();
        }
        private float? GetBaseTradeIncidentCount()
        {
            var comp = Find.Storyteller.storytellerComps.FirstOrDefault(x => x is StorytellerComp_FactionInteraction sc && sc.Props.incident == IncidentDefOf.TraderCaravanArrival) as StorytellerComp_FactionInteraction;
            return comp?.Props?.baseIncidentsPerYear;
        }
        private float? GetFinalTradeIncidentCount()
        {
            var comp = Find.Storyteller.storytellerComps.FirstOrDefault(x => x is StorytellerComp_FactionInteraction sc && sc.Props.incident == IncidentDefOf.TraderCaravanArrival) as StorytellerComp_FactionInteraction;
            return comp?.Props?.baseIncidentsPerYear * StorytellerComp_FactionInteraction_Patch.GetIncidentCountPerYearModifier(comp, Find.AnyPlayerHomeMap);
        }
        private float GetBaseTradeIncidentChance()
        {
            return IncidentDefOf.TraderCaravanArrival.baseChance;
        }
        private float GetFinalTradeIncidentChance()
        {
            return Find.Storyteller.storytellerComps.OfType<StorytellerComp_RandomMain>().First().IncidentChanceFinal(IncidentDefOf.TraderCaravanArrival);
        }

        public void InitRelationBonus()
        {
            relationBonus = new Dictionary<float, float>()
                {
                    {-100f, 0f},
                    {-74f, 0.01f},
                    {-50f, 0.25f},
                    {-25f, 0.5f},
                    {0f, 1f},
                    {25f, 1.25f},
                    {50f, 1.5f},
                    {75f, 2f},
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
        public void InitTotalSettlementCountBonus()
        {
            totalSettlementCountBonus = new Dictionary<int, float>()
                {
                    {0, 0.1f},
                    {1, 0.25f},
                    {5, 1f},
                    {10, 2f},
                    {20, 3f},
                    {50, 5f},
                };
        }
        public void RebuildTotalSettlementCountBonusCurve()
        {
            if (totalSettlementCountBonus is null || totalSettlementCountBonus.Count == 0)
            {
                InitTotalSettlementCountBonus();
            }
            totalSettlementCountBonusCurve = new SimpleCurve();
            foreach (var dictKey in totalSettlementCountBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = totalSettlementCountBonus[dictKey];
                totalSettlementCountBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }

        public void InitFactionBaseDensityBonus()
        {
            factionBaseDensityBonus = new Dictionary<int, float>()
                {
                    {0, 0.1f},
                    {1, 0.5f},
                    {2, 1f},
                    {5, 2f},
                    {10, 3f},
                    {20, 5f},
                };
        }
        public void RebuildFactionBaseDensityBonusCurve()
        {
            if (factionBaseDensityBonus is null || factionBaseDensityBonus.Count == 0)
            {
                InitFactionBaseDensityBonus();
            }
            factionBaseDensityBonusCurve = new SimpleCurve();
            foreach (var dictKey in factionBaseDensityBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = factionBaseDensityBonus[dictKey];
                factionBaseDensityBonusCurve.Add(new CurvePoint(dictKey, value));
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

            if (maxTravelDistancePeriodForTrading > dayTravelBonus.MaxBy(x => x.Key).Key)
            {
                var toAdd = maxTravelDistancePeriodForTrading - dayTravelBonus.Count;
                for (var i = 0; i < toAdd; i++)
                {
                    dayTravelBonus[i + maxTravelDistancePeriodForTrading] = 0.4f;
                }
            }
            if (dayTravelBonus.MaxBy(x => x.Key).Key > maxTravelDistancePeriodForTrading)
            {
                dayTravelBonus.RemoveAll(x => x.Key > maxTravelDistancePeriodForTrading);
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

        public void InitColonyWealthAttractionBonus()
        {
            colonyWealthAttractionBonus = new Dictionary<int, float>()
            {
                {0, 0.1f},
                {25000, 0.25f},
                {50000, 0.5f},
                {100000, 1f},
                {300000, 2f},
                {500000, 3f},
                {1000000, 4f},
                {2000000, 5f},
            };
        }
        public void RebuildColonyWealthAttractionBonusCurve()
        {
            if (colonyWealthAttractionBonus is null || colonyWealthAttractionBonus.Count == 0)
            {
                InitColonyWealthAttractionBonus();
            }
            colonyWealthAttractionBonusCurve = new SimpleCurve();
            foreach (var dictKey in colonyWealthAttractionBonus.Keys.ToList().OrderBy(x => x))
            {
                var value = colonyWealthAttractionBonus[dictKey];
                colonyWealthAttractionBonusCurve.Add(new CurvePoint(dictKey, value));
            }
        }
        public void InitWorldSizeModifiersBonus()
        {
            worldSizeModifiers = new Dictionary<float, float>()
            {
                {0.05f, 3f},
                {0.15f, 2f},
                {0.3f, 1f},
                {0.5f, 0.7f},
                {0.7f, 0.5f},
                {1f, 0.3f},
            };
        }
        public void RebuildWorldSizeModifiersBonusCurve()
        {
            if (worldSizeModifiers is null || worldSizeModifiers.Count == 0)
            {
                InitWorldSizeModifiersBonus();
            }
            worldSizeModifiersCurve = new SimpleCurve();
            foreach (var dictKey in worldSizeModifiers.Keys.ToList().OrderBy(x => x))
            {
                var value = worldSizeModifiers[dictKey];
                worldSizeModifiersCurve.Add(new CurvePoint(dictKey, value));
            }
        }

        private static Vector2 scrollPosition = Vector2.zero;
    }
}

