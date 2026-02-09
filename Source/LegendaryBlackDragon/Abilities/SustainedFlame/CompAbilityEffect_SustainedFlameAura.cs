using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
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
            if (GetCurrentEffecterDef() != null && currentTick >= nextEffecterTick)
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

        private void SpawnRingEffecters()
        {
            if (Caster?.Map == null)
            {
                return;
            }

            EffecterDef currentEffecterDef = GetCurrentEffecterDef();
            if (currentEffecterDef == null)
                return;

            Map map = Caster.Map;
            Vector3 center = Caster.DrawPos;
            int points = Mathf.Max(1, Props.effecterRingPoints);
            float innerRadius = Props.effecterRingRadius;
            float outerRadius = Props.radius;

            for (int i = 0; i < points; i++)
            {
                float baseAngle = (360f / points) * i + Rand.Range(-10f, 10f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                Vector3 innerPos = center + direction * innerRadius;
                IntVec3 innerCell = innerPos.ToIntVec3();

                float dist = outerRadius + Rand.Range(-0.5f, 0.5f);
                Vector3 outerPos = center + direction * dist;
                IntVec3 outerCell = outerPos.ToIntVec3();

                if (!innerCell.InBounds(map) || !outerCell.InBounds(map))
                {
                    continue;
                }

                parent.AddEffecterToMaintain(
                    currentEffecterDef.Spawn(innerCell, outerCell, map),
                    innerCell,
                    outerCell,
                    Props.effecterDurationTicks,
                    map);
            }

            // 中心也播放一个
            parent.AddEffecterToMaintain(
                currentEffecterDef.Spawn(Caster.Position, map),
                Caster.Position,
                Props.effecterDurationTicks,
                map);
        }

        private void ThrowFuelTick()
        {
            if (Caster?.Map == null || Props.filthDef == null)
            {
                return;
            }

            float chance = GetCurrentFuelSpawnChance();
            if (!Rand.Chance(chance))
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

        private void DoDamagePulse()
        {
            if (Caster == null || Caster.Map == null)
            {
                StopFlame();
                return;
            }

            DamageDef damageDef = GetCurrentDamageDef();
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
                GetCurrentDamAmount(),
                GetCurrentArmorPenetration(),
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

        #region 状态获取方法

        private FlameAuraState GetCurrentState()
        {
            if (Caster == null || Caster.health?.hediffSet == null || Props.states == null)
                return null;

            // 检查施法者是否有匹配的Hediff
            foreach (var state in Props.states)
            {
                if (state.hediffDef != null && Caster.health.hediffSet.HasHediff(state.hediffDef))
                {
                    return state;
                }
            }

            return null;
        }

        private DamageDef GetCurrentDamageDef()
        {
            var state = GetCurrentState();
            return state?.damageDef ?? Props.damageDef ?? DamageDefOf.Flame;
        }

        private int GetCurrentDamAmount()
        {
            var state = GetCurrentState();
            return state?.damAmount ?? Props.damAmount;
        }

        private float GetCurrentArmorPenetration()
        {
            var state = GetCurrentState();
            return state?.armorPenetration ?? Props.armorPenetration;
        }

        private EffecterDef GetCurrentEffecterDef()
        {
            var state = GetCurrentState();
            return state?.effecterDef ?? Props.effecterDef;
        }

        private float GetCurrentFuelSpawnChance()
        {
            var state = GetCurrentState();
            return state?.fuelSpawnChancePerTick ?? Props.fuelSpawnChancePerTick;
        }

        #endregion
    }
}
