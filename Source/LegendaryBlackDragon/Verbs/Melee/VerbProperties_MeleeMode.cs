using RimWorld;
using Verse;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class VerbProperties_MeleeMode : VerbProperties
    {
        public float slamRadius = 1.9f;
        public float slamDamageFactor = 0.65f;
        public int scratchKnockbackCells = 2;
        public int scratchStunTicks = 45;
        public ThingDef scratchKnockbackFlyerDef = ThingDefOf.PawnFlyer;
        public EffecterDef scratchFlightEffecterDef;
        public SoundDef scratchLandingSound;
    }
}
