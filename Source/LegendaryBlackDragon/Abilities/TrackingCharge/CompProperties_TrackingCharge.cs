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
        
        // 新增：是否将初始伤害乘以近战伤害系数
        public bool multiplyInitialDamageByMeleeFactor = false;
        
        // 新增：是否将每格伤害乘以近战伤害系数
        public bool multiplyPerTileDamageByMeleeFactor = false;

        public CompProperties_TrackingCharge()
        {
            this.compClass = typeof(CompAbilityEffect_TrackingCharge);
        }
    }
}
