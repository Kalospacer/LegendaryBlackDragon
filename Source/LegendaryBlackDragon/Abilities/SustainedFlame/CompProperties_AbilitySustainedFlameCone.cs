using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_AbilitySustainedFlameCone : CompProperties_AbilityEffect
    {
        public float range = 11.9f;
        public float lineWidthEnd = 5f;
        public DamageDef damageDef;
        public ThingDef filthDef;
        public int damageAmount = 12;
        public float armorPenetration = 0f;
        public int startDamageTick = 10;
        public int damageIntervalTicks = 10;
        public int visualIntervalTicks = 3;
        public int maxSustainTicks = 180;
        public EffecterDef effecterDef;
        public bool canHitFilledCells = true;
        public bool affectCaster = false;
        public int numStreams = 15;
        public float coneSizeDegrees = 12f;
        public float visualRangeOffset = 1.1f;
        public float rangeNoise = 0.4f;
        public float barrelOffsetDistance = 6f;
        public float sizeReductionDistanceThreshold = 8f;
        public int lifespanNoise = 40;

        public CompProperties_AbilitySustainedFlameCone()
        {
            compClass = typeof(CompAbilityEffect_SustainedFlameCone);
        }
    }
}
