using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 以自身为中心 360° 持续喷射火焰光环 — 基于 FireBurst 模式扩展为持续引导型
    /// </summary>
    public class CompAbilityEffect_SustainedFlameAura : CompAbilityEffect
    {
        private readonly List<IntVec3> tmpAffectedCells = new List<IntVec3>();

        private bool isActive;
        private int startTick;
        private int nextDamageTick;
        private int nextEffecterTick;

        public new CompProperties_AbilitySustainedFlameAura Props => (CompProperties_AbilitySustainedFlameAura)props;

        public bool IsActive => isActive;

        public int ActiveTicks => isActive ? Find.TickManager.TicksGame - startTick : 0;

        private Pawn Caster => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (Caster == null || Caster.MapHeld == null)
            {
                return;
            }

            isActive = true;
            startTick = Find.TickManager.TicksGame;
            nextDamageTick = startTick + Mathf.Max(0, Props.startDamageTick);
            nextEffecterTick = startTick;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(Caster.Position, Props.radius);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!isActive)
            {
                return;
            }

            // 终止条件检查
            if (Caster == null || Caster.Dead || !Caster.Spawned || Caster.MapHeld == null)
            {
                StopFlame();
                return;
            }

            if (!CasterMaintainingAuraJob())
            {
                StopFlame();
                return;
            }

            if (Props.maxSustainTicks > 0 && ActiveTicks >= Props.maxSustainTicks)
            {
                StopFlame();
                return;
            }

            int currentTick = Find.TickManager.TicksGame;

            // 周期性沿圆周多点播放 Effecter，由内向外散发
            if (Props.effecterDef != null && currentTick >= nextEffecterTick)
            {
                SpawnRingEffecters();
                nextEffecterTick = currentTick + Mathf.Max(1, Props.effecterIntervalTicks);
            }

            // 持续期间撒燃料（复用 FireBurst 的逻辑）
            ThrowFuelTick();

            // 周期性伤害脉冲
            if (currentTick < nextDamageTick)
            {
                return;
            }

            int interval = Mathf.Max(1, Props.damageIntervalTicks);
            while (currentTick >= nextDamageTick && isActive)
            {
                DoDamagePulse();
                nextDamageTick += interval;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref isActive, "isActive", false);
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref nextDamageTick, "nextDamageTick", 0);
            Scribe_Values.Look(ref nextEffecterTick, "nextEffecterTick", 0);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            // 和原版 FireBurst 一致：玩家阵营不自动用，敌方仅在被瞄准时用
            if (Caster.Faction == Faction.OfPlayer)
            {
                return false;
            }

            if (target.HasThing && target.Thing is Pawn pawn)
            {
                return pawn.TargetCurrentlyAimingAt == Caster;
            }

            return false;
        }

        private void StopFlame()
        {
            isActive = false;
            startTick = 0;
            nextDamageTick = 0;
            nextEffecterTick = 0;
        }

        private bool CasterMaintainingAuraJob()
        {
            if (Caster?.jobs?.curJob == null)
            {
                return false;
            }

            Job curJob = Caster.jobs.curJob;
            return curJob.ability == parent && curJob.def != null && curJob.def.abilityCasting;
        }

        /// <summary>
        /// 沿圆周均匀分布多个发射点，每个点用 Spawn(posA, posB) 从内向外播放 Effecter
        /// </summary>
        private void SpawnRingEffecters()
        {
            if (Caster?.Map == null || Props.effecterDef == null)
            {
                return;
            }

            Map map = Caster.Map;
            Vector3 center = Caster.DrawPos;
            int points = Mathf.Max(1, Props.effecterRingPoints);
            float innerRadius = Props.effecterRingRadius;
            float outerRadius = Props.radius;

            for (int i = 0; i < points; i++)
            {
                // 均匀分布 + 随机偏移避免机械感
                float baseAngle = (360f / points) * i + Rand.Range(-10f, 10f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                // 内圈发射点
                Vector3 innerPos = center + direction * innerRadius;
                IntVec3 innerCell = innerPos.ToIntVec3();

                // 外圈目标点
                float dist = outerRadius + Rand.Range(-0.5f, 0.5f);
                Vector3 outerPos = center + direction * dist;
                IntVec3 outerCell = outerPos.ToIntVec3();

                if (!innerCell.InBounds(map) || !outerCell.InBounds(map))
                {
                    continue;
                }

                // 从内圈→外圈方向播放 Effecter
                parent.AddEffecterToMaintain(
                    Props.effecterDef.Spawn(innerCell, outerCell, map),
                    innerCell,
                    outerCell,
                    Props.effecterDurationTicks,
                    map);
            }

            // 中心也播放一个，保证中心有火焰
            parent.AddEffecterToMaintain(
                Props.effecterDef.Spawn(Caster.Position, map),
                Caster.Position,
                Props.effecterDurationTicks,
                map);
        }

        /// <summary>
        /// 复用 FireBurstUtility.ThrowFuelTick 的逻辑，持续期间在范围内撒燃料
        /// </summary>
        private void ThrowFuelTick()
        {
            if (Caster?.Map == null || Props.filthDef == null)
            {
                return;
            }

            if (!Rand.Chance(Props.fuelSpawnChancePerTick))
            {
                return;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Caster.Position, Props.radius, useCenter: false).InRandomOrder())
            {
                if (cell.InBounds(Caster.Map)
                    && GenSight.LineOfSight(Caster.Position, cell, Caster.Map, skipFirstCell: true)
                    && FilthMaker.TryMakeFilth(cell, Caster.Map, Props.filthDef))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 以自身为中心的圆形 radius 爆炸伤害脉冲，和 FireBurst 的 Apply 一致
        /// </summary>
        private void DoDamagePulse()
        {
            if (Caster == null || Caster.Map == null)
            {
                StopFlame();
                return;
            }

            DamageDef damageDef = Props.damageDef ?? DamageDefOf.Flame;
            List<IntVec3> affectedCells = BuildAffectedCells();
            if (affectedCells.Count == 0)
            {
                return;
            }

            List<Thing> ignoredThings = null;
            if (!Props.affectCaster)
            {
                ignoredThings = new List<Thing> { Caster };
            }

            GenExplosion.DoExplosion(
                Caster.Position,
                Caster.MapHeld,
                Props.radius,
                damageDef,
                Caster,
                Props.damAmount,
                Props.armorPenetration,
                null, null, null, null,
                Props.filthDef,
                1f, 1,
                null, null, 255,
                applyDamageToExplosionCellsNeighbors: false,
                null, 0f, 1, 1f,
                damageFalloff: false,
                null, ignoredThings, null,
                doVisualEffects: false,
                0.6f, 0f,
                doSoundEffects: false,
                null,
                1f,
                null,
                affectedCells);
        }

        private List<IntVec3> BuildAffectedCells()
        {
            tmpAffectedCells.Clear();

            if (Caster?.Map == null)
            {
                return tmpAffectedCells;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Caster.Position, Props.radius, useCenter: true))
            {
                if (!cell.InBounds(Caster.Map))
                {
                    continue;
                }

                if (!Props.affectCaster && cell == Caster.Position)
                {
                    continue;
                }

                if (!Props.canHitFilledCells && cell.Filled(Caster.Map))
                {
                    continue;
                }

                tmpAffectedCells.Add(cell);
            }

            return tmpAffectedCells;
        }
    }
}
