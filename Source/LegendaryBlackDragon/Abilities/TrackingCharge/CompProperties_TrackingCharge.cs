using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_TrackingCharge : CompProperties_AbilityEffect
    {
        public float homingSpeed = 1.0f;
        public float initialDamage = 10f;
        public float damagePerTile = 2f;
        public float inertiaDistance = 3f;
        public DamageDef collisionDamageDef;
        public ThingDef flyerDef;
        public float collisionRadius = 1.5f;
        public SoundDef impactSound;
        public bool damageHostileOnly = true;

        public CompProperties_TrackingCharge()
        {
            this.compClass = typeof(CompAbilityEffect_TrackingCharge);
        }
    }
}