using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_AbilityFanShapedStunKnockback : CompProperties_AbilityEffect
    {
        // 扇形参数
        public float range = 5f;                 // 扇形半径
        public float coneSizeDegrees = 45f;      // 扇形角度（总角度）
        public float lineWidthEnd = 3f;          // 扇形末端宽度
        
        // 伤害参数
        public DamageDef damageDef = DamageDefOf.Blunt;
        public float damageAmount = 15f;
        public float armorPenetration = 0f;
        
        // 眩晕参数
        public int stunTicks = 180; // 3秒眩晕 (60 ticks = 1秒)
        
        // 击退参数
        public int maxKnockbackDistance = 3;     // 最大击退距离
        public bool canKnockbackIntoWalls = false; // 是否可以击退到墙上
        public bool requireLineOfSight = true;   // 击退路径是否需要视线
        
        // 视觉效果
        public EffecterDef impactEffecter;       // 命中效果
        public SoundDef impactSound;             // 命中音效
        
        // 飞行效果设置
        public ThingDef knockbackFlyerDef;       // 击退飞行器定义
        public EffecterDef flightEffecterDef;    // 飞行效果
        public SoundDef landingSound;            // 落地音效
        
        // 过滤设置
        public bool affectCaster = false;        // 是否影响施法者
        public bool canHitFilledCells = true;    // 是否可以击中已填充的单元格
        public bool onlyAffectEnemies = true;    // 只影响敌人
        public bool requireLineOfSightToTarget = true; // 是否需要视线到目标

        public CompProperties_AbilityFanShapedStunKnockback()
        {
            compClass = typeof(CompAbilityEffect_FanShapedStunKnockback);
        }
    }
}
