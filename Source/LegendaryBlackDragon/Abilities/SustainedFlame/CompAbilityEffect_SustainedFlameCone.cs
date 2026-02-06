using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_SustainedFlameCone : CompAbilityEffect
    {
        private readonly List<IntVec3> tmpCells = new List<IntVec3>();

        private IntVec3 targetCell = IntVec3.Invalid;
        private bool isActive;
        private int startTick;
        private int nextDamageTick;

        public new CompProperties_AbilitySustainedFlameCone Props => (CompProperties_AbilitySustainedFlameCone)props;

        public bool IsActive => isActive;
        public IntVec3 TargetCell => targetCell;

        public int ActiveTicks
        {
            get
            {
                if (!isActive)
                {
                    return 0;
                }

                return Find.TickManager.TicksGame - startTick;
            }
        }

        private Pawn Caster => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (Caster == null || Caster.MapHeld == null || !target.IsValid)
            {
                return;
            }

            targetCell = target.Cell.ClampInsideMap(Caster.MapHeld);
            isActive = true;
            startTick = Find.TickManager.TicksGame;
            nextDamageTick = startTick + Mathf.Max(0, Props.startDamageTick);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawFieldEdges(AffectedCells(target));
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!isActive)
            {
                return;
            }

            if (Caster == null || Caster.Dead || !Caster.Spawned || Caster.MapHeld == null)
            {
                StopFlame();
                return;
            }

            if (!parent.Casting)
            {
                StopFlame();
                return;
            }

            if (Props.maxSustainTicks > 0 && ActiveTicks >= Props.maxSustainTicks)
            {
                StopFlame();
                return;
            }

            if (targetCell.IsValid)
            {
                Caster.rotationTracker.FaceCell(targetCell);
            }

            int currentTick = Find.TickManager.TicksGame;
            TickVisual(currentTick);

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
            Scribe_Values.Look(ref targetCell, "targetCell", IntVec3.Invalid);
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref nextDamageTick, "nextDamageTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && isActive && targetCell == IntVec3.Invalid)
            {
                isActive = false;
            }
        }

        private void StopFlame()
        {
            isActive = false;
            targetCell = IntVec3.Invalid;
            startTick = 0;
            nextDamageTick = 0;
        }

        private void TickVisual(int currentTick)
        {
            if (Caster?.Map == null || targetCell == IntVec3.Invalid)
            {
                return;
            }

            int visualInterval = Mathf.Max(1, Props.visualIntervalTicks);
            if (currentTick % visualInterval != 0)
            {
                return;
            }

            EmitBurnerVisual();
        }

        private void EmitBurnerVisual()
        {
            if (Caster?.Map == null || targetCell == IntVec3.Invalid)
            {
                return;
            }

            Vector3 drawPos = Caster.DrawPos;
            IntVec3 sourceCell = drawPos.Yto0().ToIntVec3();
            Map map = Caster.Map;
            IncineratorSpray spray = GenSpawn.Spawn(ThingDefOf.IncineratorSpray, sourceCell, map) as IncineratorSpray;
            if (spray == null)
            {
                return;
            }

            IntVec3 tipCell = GetTipCell();
            Vector3 normalized = (tipCell.ToVector3Shifted() - drawPos).normalized;
            int streamCount = Mathf.Max(1, Props.numStreams);
            float visualRange = Props.range + Props.visualRangeOffset;

            for (int i = 0; i < streamCount; i++)
            {
                float angle = Rand.Range(0f - Props.coneSizeDegrees, Props.coneSizeDegrees);
                Vector3 streamVector = normalized.RotatedBy(angle);
                Vector3 projectedTarget = drawPos + streamVector * (visualRange + Rand.Value * Props.rangeNoise);
                IntVec3 streamTarget = GenSight.LastPointOnLineOfSight(sourceCell, projectedTarget.ToIntVec3(), c => c.CanBeSeenOverFast(map), skipFirstCell: true);
                if (!streamTarget.IsValid)
                {
                    streamTarget = projectedTarget.ToIntVec3();
                }

                float distance = Vector3.Distance(streamTarget.ToVector3(), drawPos);
                float scaleFactor = Mathf.Clamp01(distance / Props.sizeReductionDistanceThreshold);
                if (Vector3.Dot((streamTarget.ToVector3() - drawPos).normalized, streamVector) <= 0.5f)
                {
                    continue;
                }

                MoteDualAttached mote = MoteMaker.MakeInteractionOverlay(ThingDefOf.Mote_IncineratorBurst, new TargetInfo(sourceCell, map), new TargetInfo(streamTarget, map));
                spray.Add(new IncineratorProjectileMotion
                {
                    mote = mote,
                    targetDest = targetCell,
                    worldSource = drawPos + streamVector * Props.barrelOffsetDistance,
                    worldTarget = streamTarget.ToVector3(),
                    moveVector = streamVector,
                    startScale = Rand.Range(0.8f, 1.2f) * scaleFactor,
                    endScale = (1f + Rand.Range(0.1f, 0.4f)) * scaleFactor,
                    lifespanTicks = Mathf.FloorToInt(distance * 5f) + Rand.Range(-Props.lifespanNoise, Props.lifespanNoise)
                });

                if (Props.effecterDef != null)
                {
                    map.effecterMaintainer.AddEffecterToMaintain(Props.effecterDef.Spawn(streamTarget, map), streamTarget, 100);
                }
            }
        }

        private void DoDamagePulse()
        {
            if (Caster == null || Caster.Map == null || targetCell == IntVec3.Invalid)
            {
                StopFlame();
                return;
            }

            List<IntVec3> cells = AffectedCells(targetCell);
            if (cells.Count == 0)
            {
                return;
            }

            DamageDef damageDef = Props.damageDef ?? DamageDefOf.Flame;
            SimpleCurve fireCurve = parent.verb?.verbProps?.flammabilityAttachFireChanceCurve;

            GenExplosion.DoExplosion(
                targetCell,
                Caster.MapHeld,
                0f,
                damageDef,
                Caster,
                Props.damageAmount,
                Props.armorPenetration,
                null,
                null,
                null,
                null,
                Props.filthDef,
                1f,
                1,
                null,
                null,
                255,
                applyDamageToExplosionCellsNeighbors: false,
                null,
                0f,
                1,
                1f,
                damageFalloff: false,
                null,
                null,
                null,
                doVisualEffects: false,
                0.6f,
                0f,
                doSoundEffects: false,
                null,
                1f,
                fireCurve,
                cells
            );
        }

        private IntVec3 GetTipCell()
        {
            if (Caster?.Map == null || targetCell == IntVec3.Invalid)
            {
                return Caster?.Position ?? IntVec3.Invalid;
            }

            IntVec3 casterPos = Caster.Position;
            IntVec3 clampedTarget = targetCell.ClampInsideMap(Caster.Map);
            if (casterPos == clampedTarget)
            {
                return casterPos;
            }

            float horizontalLength = (clampedTarget - casterPos).LengthHorizontal;
            float dirX = (clampedTarget.x - casterPos.x) / horizontalLength;
            float dirZ = (clampedTarget.z - casterPos.z) / horizontalLength;

            IntVec3 tip = clampedTarget;
            tip.x = Mathf.RoundToInt(casterPos.x + dirX * Props.range);
            tip.z = Mathf.RoundToInt(casterPos.z + dirZ * Props.range);

            IntVec3 lastVisible = GenSight.LastPointOnLineOfSight(casterPos, tip, c => c.CanBeSeenOverFast(Caster.Map), skipFirstCell: true);
            if (lastVisible.IsValid)
            {
                return lastVisible;
            }

            return tip.ClampInsideMap(Caster.Map);
        }

        private List<IntVec3> AffectedCells(LocalTargetInfo target)
        {
            return AffectedCells(target.Cell);
        }

        private List<IntVec3> AffectedCells(IntVec3 target)
        {
            tmpCells.Clear();

            if (Caster == null || Caster.Map == null)
            {
                return tmpCells;
            }

            IntVec3 casterPos = Caster.Position;
            IntVec3 clampedTarget = target.ClampInsideMap(Caster.Map);
            if (casterPos == clampedTarget)
            {
                return tmpCells;
            }

            Vector3 casterVector = casterPos.ToVector3Shifted().Yto0();
            float horizontalLength = (clampedTarget - casterPos).LengthHorizontal;
            float dirX = (clampedTarget.x - casterPos.x) / horizontalLength;
            float dirZ = (clampedTarget.z - casterPos.z) / horizontalLength;

            clampedTarget.x = Mathf.RoundToInt(casterPos.x + dirX * Props.range);
            clampedTarget.z = Mathf.RoundToInt(casterPos.z + dirZ * Props.range);

            float targetAngle = Vector3.SignedAngle(clampedTarget.ToVector3Shifted().Yto0() - casterVector, Vector3.right, Vector3.up);
            float halfWidth = Props.lineWidthEnd / 2f;
            float coneEdgeDistance = Mathf.Sqrt(Mathf.Pow((clampedTarget - casterPos).LengthHorizontal, 2f) + Mathf.Pow(halfWidth, 2f));
            float halfAngle = Mathf.Rad2Deg * Mathf.Asin(halfWidth / coneEdgeDistance);

            int radialCellCount = GenRadial.NumCellsInRadius(Props.range);
            for (int i = 0; i < radialCellCount; i++)
            {
                IntVec3 cell = casterPos + GenRadial.RadialPattern[i];
                if (!CanUseCell(cell))
                {
                    continue;
                }

                float cellAngle = Vector3.SignedAngle(cell.ToVector3Shifted().Yto0() - casterVector, Vector3.right, Vector3.up);
                if (Mathf.Abs(Mathf.DeltaAngle(cellAngle, targetAngle)) <= halfAngle)
                {
                    tmpCells.Add(cell);
                }
            }

            List<IntVec3> lineCells = GenSight.BresenhamCellsBetween(casterPos, clampedTarget);
            for (int i = 0; i < lineCells.Count; i++)
            {
                IntVec3 cell = lineCells[i];
                if (!tmpCells.Contains(cell) && CanUseCell(cell))
                {
                    tmpCells.Add(cell);
                }
            }

            return tmpCells;
        }

        private bool CanUseCell(IntVec3 cell)
        {
            if (Caster == null || Caster.Map == null)
            {
                return false;
            }

            if (!cell.InBounds(Caster.Map))
            {
                return false;
            }

            if (!Props.affectCaster && cell == Caster.Position)
            {
                return false;
            }

            if (!Props.canHitFilledCells && cell.Filled(Caster.Map))
            {
                return false;
            }

            if (!cell.InHorDistOf(Caster.Position, Props.range))
            {
                return false;
            }

            if (parent?.verb == null)
            {
                return false;
            }

            return parent.verb.TryFindShootLineFromTo(Caster.Position, cell, out _);
        }
    }
}
