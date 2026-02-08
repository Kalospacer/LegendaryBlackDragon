using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    [DefOf]
    public static class LBD_DefOf
    {
        public static ThingDef LBD_DragonSkyNukeStrikeController;
        public static ThingDef LBD_DragonSkyNukeFlyerLeaving;
        public static ThingDef LBD_DragonSkyNukeFlyerArrival;

        static LBD_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LBD_DefOf));
        }
    }
}
