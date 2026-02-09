using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_AbilitySustainedFlameAura : CompProperties_AbilityEffect
    {
        public float radius = 5.9f;
        public ThingDef filthDef;
        public int damAmount = -1;
        public float armorPenetration = -1f;
        public DamageDef damageDef;
        public EffecterDef effecterDef;
        public int startDamageTick = 10;
        public int damageIntervalTicks = 10;
        public int maxSustainTicks = 180;
        public int effecterIntervalTicks = 5;
        public int effecterDurationTicks = 17;
        public int effecterRingPoints = 12;
        public float effecterRingRadius = 1.5f;
        public float fuelSpawnChancePerTick = 0.15f;
        public bool affectCaster = false;
        public bool canHitFilledCells = true;

        // 状态定义
        public List<FlameAuraState> states;

        public CompProperties_AbilitySustainedFlameAura()
        {
            compClass = typeof(CompAbilityEffect_SustainedFlameAura);
        }
    }

    // 火焰光环状态参数
    public class FlameAuraState
    {
        public HediffDef hediffDef;          // 触发的hediff
        public DamageDef damageDef;          // 伤害类型（可选）
        public int damAmount = -1;           // 伤害值（可选）
        public float armorPenetration = -1f; // 护甲穿透（可选）
        public EffecterDef effecterDef;      // 特效（可选）
        public float fuelSpawnChancePerTick = 0.15f; // 燃料生成概率（可选）
    }
}
