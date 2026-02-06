using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 以自身为中心 360° 持续喷射火焰光环 — 基于 FireBurst 扩展为持续引导型
    /// </summary>
    public class CompProperties_AbilitySustainedFlameAura : CompProperties_AbilityEffect
    {
        // === 基于 FireBurst 的基底字段 ===
        public float radius = 5.9f;
        public ThingDef filthDef;
        public int damAmount = -1;
        public float armorPenetration = -1f;
        public DamageDef damageDef;
        public EffecterDef effecterDef;

        // === 持续引导控制 ===
        public int startDamageTick = 10;
        public int damageIntervalTicks = 10;
        public int maxSustainTicks = 180;

        // === 特效控制 ===
        public int effecterIntervalTicks = 5;
        public int effecterDurationTicks = 17;
        /// <summary>环形发射点数量，沿圆周均匀分布</summary>
        public int effecterRingPoints = 12;
        /// <summary>环形发射点距中心的距离（格），0 表示从中心发射</summary>
        public float effecterRingRadius = 1.5f;

        // === 蓄力期间撒燃料 ===
        public float fuelSpawnChancePerTick = 0.15f;

        public bool affectCaster = false;
        public bool canHitFilledCells = true;

        public CompProperties_AbilitySustainedFlameAura()
        {
            compClass = typeof(CompAbilityEffect_SustainedFlameAura);
        }
    }
}
