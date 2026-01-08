using UnityEngine;
using Verse;

namespace RealisticTrade
{
    public class RealisticTradeMod : Mod
    {
        public static RealisticTradeSettings settings;
        public RealisticTradeMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<RealisticTradeSettings>();
            settings.ReInitValues();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }
}
