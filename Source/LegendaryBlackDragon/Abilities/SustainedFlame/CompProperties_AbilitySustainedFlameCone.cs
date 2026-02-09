using RimWorld;
using System.Collections.Generic;
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
        public ThingDef moteDef;
        public bool canHitFilledCells = true;
        public bool affectCaster = false;
        public int numStreams = 15;
        public float coneSizeDegrees = 12f;
        public float visualRangeOffset = 1.1f;
        public float visualLengthMultiplier = 1f;
        public float rangeNoise = 0.4f;
        public float barrelOffsetDistance = 6f;
        public float sizeReductionDistanceThreshold = 8f;
        public int lifespanNoise = 40;

        // 状态定义：hediff -> 参数映射
        public List<FlameState> states;

        public CompProperties_AbilitySustainedFlameCone()
        {
            compClass = typeof(CompAbilityEffect_SustainedFlameCone);
        }
    }

    // 状态参数结构
    public class FlameState
    {
        public HediffDef hediffDef;          // 触发的hediff
        public DamageDef damageDef;          // 伤害类型（可选）
        public int damageAmount = 12;        // 伤害值（可选）
        public float armorPenetration = 0f;  // 护甲穿透（可选）
        public EffecterDef effecterDef;      // 特效（可选）
        public ThingDef moteDef;             // 粒子效果（可选）
    }
}
