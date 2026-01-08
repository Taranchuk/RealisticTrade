using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace RealisticTrade
{
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
                Core.Log($"FINAL_TRADER_PER_YEAR Base incident count per year is {instance.Props.baseIncidentsPerYear}, now it's {instance.Props.baseIncidentsPerYear * modifier}");
                return modifier;
            }
            return 1f; // we keep it as is so we don't touch the base value
        }

    }
}
