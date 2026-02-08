using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_AbilityDragonSkyNuke : CompProperties_AbilityEffect
    {
        public ThingDef strikeControllerDef;

        public IntRange phase1DelayTicksRange = new IntRange(180, 300);
        public int phase1DurationTicks = 300;
        public int phase1WaveIntervalTicks = 30;

        public int phase2IntervalTicks = 60;
        public int phase2PulseCount = 20;
        public int phase2IgniteCellsPerPulse = -1;
        public float phase2IgniteFireSize = 0.16f;
        public DamageDef damageDef;
        public int damageAmount = 16;
        public float armorPenetration;

        public EffecterDef phase1ItemEffecterDef;
        public int phase1EffecterMaintainTicks = 20;
        public float phase1IgniteFireSize = 0.2f;

        public int incomingWaveCells = 10;
        public int incomingWaveWidth = 2;

        public int skyFadeInTicks = 20;
        public int skyFadeOutTicks = 40;

        public CompProperties_AbilityDragonSkyNuke()
        {
            compClass = typeof(CompAbilityEffect_AbilityDragonSkyNuke);
        }
    }
}
