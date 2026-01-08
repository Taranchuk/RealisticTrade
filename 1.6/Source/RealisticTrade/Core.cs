using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace RealisticTrade
{
    [StaticConstructorOnStartup]
    [HotSwappable]
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

        public static bool debug => true;

        public static void Log(string message)
        {
            if (debug)
            {
                Verse.Log.Message(message);
            }
        }
    }
}
