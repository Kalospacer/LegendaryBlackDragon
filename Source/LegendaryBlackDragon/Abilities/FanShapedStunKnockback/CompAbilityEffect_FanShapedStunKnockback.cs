using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_FanShapedStunKnockback : CompAbilityEffect
    {
        private readonly List<IntVec3> tmpCells = new List<IntVec3>();
        
        public new CompProperties_AbilityFanShapedStunKnockback Props => (CompProperties_AbilityFanShapedStunKnockback)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !target.IsValid)
                return;
            
            // 1. 获取扇形区域内的所有单元格
            List<IntVec3> affectedCells = GetFanShapedCells(caster, target.Cell);
            
            // 2. 收集区域内的所有敌人
            List<Pawn> affectedPawns = CollectAffectedPawns(caster, affectedCells);
            
            // 3. 对每个敌人应用效果
            foreach (Pawn pawn in affectedPawns)
            {
                if (pawn != null && !pawn.Dead && pawn.Spawned)
                {
                    ApplyEffectToPawn(caster, pawn, target);
                }
            }
        }

        /// <summary>
        /// 获取扇形区域内的所有单元格
        /// </summary>
        private List<IntVec3> GetFanShapedCells(Pawn caster, IntVec3 targetCell)
        {
            tmpCells.Clear();
            
            if (caster == null || caster.Map == null)
                return tmpCells;
            
            IntVec3 casterPos = caster.Position;
            IntVec3 clampedTarget = targetCell.ClampInsideMap(caster.Map);
            
            // 如果施法者和目标在同一位置，则没有扇形
            if (casterPos == clampedTarget)
                return tmpCells;
            
            Vector3 casterVector = casterPos.ToVector3Shifted().Yto0();
            
            // 计算方向向量和角度
            float horizontalLength = (clampedTarget - casterPos).LengthHorizontal;
            float dirX = (clampedTarget.x - casterPos.x) / horizontalLength;
            float dirZ = (clampedTarget.z - casterPos.z) / horizontalLength;
            
            // 调整目标点到扇形半径
            clampedTarget.x = Mathf.RoundToInt(casterPos.x + dirX * Props.range);
            clampedTarget.z = Mathf.RoundToInt(casterPos.z + dirZ * Props.range);
            
            // 计算扇形的中心角
            float targetAngle = Vector3.SignedAngle(
                clampedTarget.ToVector3Shifted().Yto0() - casterVector, 
                Vector3.right, 
                Vector3.up);
            
            // 计算扇形的半角（从中心线到边缘）
            float halfWidth = Props.lineWidthEnd / 2f;
            float coneEdgeDistance = Mathf.Sqrt(
                Mathf.Pow((clampedTarget - casterPos).LengthHorizontal, 2f) + 
                Mathf.Pow(halfWidth, 2f));
            float halfAngle = Mathf.Rad2Deg * Mathf.Asin(halfWidth / coneEdgeDistance);
            
            // 限制最大角度不超过设定值
            halfAngle = Mathf.Min(halfAngle, Props.coneSizeDegrees / 2f);
            
            // 遍历半径内的所有单元格，检查是否在扇形内
            int radialCellCount = GenRadial.NumCellsInRadius(Props.range);
            for (int i = 0; i < radialCellCount; i++)
            {
                IntVec3 cell = casterPos + GenRadial.RadialPattern[i];
                
                // 检查单元格是否有效
                if (!CanUseCell(caster, cell))
                    continue;
                
                // 计算单元格相对于施法者的角度
                float cellAngle = Vector3.SignedAngle(
                    cell.ToVector3Shifted().Yto0() - casterVector, 
                    Vector3.right, 
                    Vector3.up);
                
                // 检查角度差是否在扇形范围内
                if (Mathf.Abs(Mathf.DeltaAngle(cellAngle, targetAngle)) <= halfAngle)
                {
                    tmpCells.Add(cell);
                }
            }
            
            // 添加从施法者到目标点的直线上的单元格
            List<IntVec3> lineCells = GenSight.BresenhamCellsBetween(casterPos, clampedTarget);
            for (int i = 0; i < lineCells.Count; i++)
            {
                IntVec3 cell = lineCells[i];
                if (!tmpCells.Contains(cell) && CanUseCell(caster, cell))
                {
                    tmpCells.Add(cell);
                }
            }
            
            return tmpCells;
        }

        /// <summary>
        /// 收集扇形区域内的所有敌人
        /// </summary>
        private List<Pawn> CollectAffectedPawns(Pawn caster, List<IntVec3> cells)
        {
            List<Pawn> pawns = new List<Pawn>();
            HashSet<Pawn> addedPawns = new HashSet<Pawn>();
            
            if (caster == null || caster.Map == null || cells == null)
                return pawns;
            
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(caster.Map))
                    continue;
                    
                List<Thing> things = cell.GetThingList(caster.Map);
                foreach (Thing thing in things)
                {
                    if (thing is Pawn pawn && !addedPawns.Contains(pawn))
                    {
                        // 检查是否为敌人
                        if (Props.onlyAffectEnemies && !pawn.HostileTo(caster))
                            continue;
                            
                        // 检查是否影响施法者
                        if (!Props.affectCaster && pawn == caster)
                            continue;
                            
                        // 检查是否需要视线
                        if (Props.requireLineOfSightToTarget && !GenSight.LineOfSight(caster.Position, cell, caster.Map))
                            continue;
                            
                        pawns.Add(pawn);
                        addedPawns.Add(pawn);
                    }
                }
            }
            
            return pawns;
        }

        /// <summary>
        /// 对单个敌人应用效果
        /// </summary>
        private void ApplyEffectToPawn(Pawn caster, Pawn target, LocalTargetInfo targetInfo)
        {
            // 1. 造成伤害
            bool targetDied = ApplyDamageAndStun(caster, target);
            
            // 2. 如果目标存活，执行击退
            if (!targetDied && target != null && !target.Dead && !target.Downed)
            {
                PerformKnockback(caster, target);
            }
        }

        /// <summary>
        /// 应用伤害和眩晕效果，返回目标是否死亡
        /// </summary>
        private bool ApplyDamageAndStun(Pawn caster, Pawn target)
        {
            // 创建伤害信息
            DamageInfo damageInfo = new DamageInfo(
                Props.damageDef,
                Props.damageAmount,
                Props.armorPenetration,
                -1f,
                caster,
                null
            );

            // 应用伤害
            target.TakeDamage(damageInfo);
            
            // 检查目标是否死亡
            bool targetDied = target.Dead || target.Destroyed;
            
            if (targetDied)
            {
                return true;
            }
            
            // 播放冲击效果
            if (Props.impactEffecter != null && caster.Map != null)
            {
                Effecter effect = Props.impactEffecter.Spawn();
                effect.Trigger(new TargetInfo(target.Position, caster.Map), new TargetInfo(target.Position, caster.Map));
                effect.Cleanup();
            }
            
            // 播放冲击音效
            if (Props.impactSound != null && caster.Map != null)
            {
                Props.impactSound.PlayOneShot(new TargetInfo(target.Position, caster.Map));
            }
            
            // 应用眩晕 - 只在目标存活时应用
            if (Props.stunTicks > 0 && !target.Dead)
            {
                target.stances?.stunner?.StunFor(Props.stunTicks, caster);
            }

            return false;
        }

        /// <summary>
        /// 执行击退
        /// </summary>
        private void PerformKnockback(Pawn caster, Pawn target)
        {
            if (target == null || target.Destroyed || target.Dead || caster.Map == null)
                return;

            // 计算击退方向（从施法者指向目标）
            IntVec3 knockbackDirection = CalculateKnockbackDirection(caster, target.Position);
            
            // 寻找最远的可站立击退位置
            IntVec3 knockbackDestination = FindFarthestStandablePosition(caster, target, knockbackDirection);
            
            // 如果找到了有效位置，执行击退飞行
            if (knockbackDestination.IsValid && knockbackDestination != target.Position)
            {
                CreateKnockbackFlyer(caster, target, knockbackDestination);
            }
        }

        /// <summary>
        /// 计算击退方向
        /// </summary>
        private IntVec3 CalculateKnockbackDirection(Pawn caster, IntVec3 targetPosition)
        {
            IntVec3 direction = targetPosition - caster.Position;
            
            // 标准化方向（保持整数坐标）
            if (direction.x != 0 || direction.z != 0)
            {
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
                {
                    return new IntVec3(Mathf.Sign(direction.x) > 0 ? 1 : -1, 0, 0);
                }
                else
                {
                    return new IntVec3(0, 0, Mathf.Sign(direction.z) > 0 ? 1 : -1);
                }
            }
            
            // 如果施法者和目标在同一位置，使用随机方向
            return new IntVec3(Rand.Value > 0.5f ? 1 : -1, 0, 0);
        }

        /// <summary>
        /// 寻找最远的可站立击退位置
        /// </summary>
        private IntVec3 FindFarthestStandablePosition(Pawn caster, Pawn target, IntVec3 direction)
        {
            Map map = caster.Map;
            IntVec3 currentPos = target.Position;
            IntVec3 farthestValidPos = currentPos;

            // 从最大距离开始向回找，找到第一个可站立的格子
            for (int distance = Props.maxKnockbackDistance; distance >= 1; distance--)
            {
                IntVec3 testPos = currentPos + (direction * distance);
                
                if (!testPos.InBounds(map))
                    continue;

                if (IsCellStandableAndEmpty(caster, target, testPos, map))
                {
                    farthestValidPos = testPos;
                    break;
                }
            }

            return farthestValidPos;
        }

        /// <summary>
        /// 检查格子是否可站立且没有其他Pawn
        /// </summary>
        private bool IsCellStandableAndEmpty(Pawn caster, Pawn target, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
                return false;

            // 检查是否可站立
            if (!cell.Standable(map))
                return false;

            // 检查是否有建筑阻挡
            if (!Props.canKnockbackIntoWalls)
            {
                Building edifice = cell.GetEdifice(map);
                if (edifice != null && !(edifice is Building_Door))
                    return false;
            }

            // 检查视线
            if (Props.requireLineOfSight && !GenSight.LineOfSight(target.Position, cell, map))
                return false;

            // 检查是否有其他pawn
            List<Thing> thingList = cell.GetThingList(map);
            foreach (Thing thing in thingList)
            {
                if (thing is Pawn otherPawn && otherPawn != target)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 创建击退飞行器
        /// </summary>
        private void CreateKnockbackFlyer(Pawn caster, Pawn target, IntVec3 destination)
        {
            Map map = caster.Map;
            
            // 使用自定义飞行器或默认飞行器
            ThingDef flyerDef = Props.knockbackFlyerDef ?? ThingDefOf.PawnFlyer;
            
            // 创建飞行器
            PawnFlyer flyer = PawnFlyer.MakeFlyer(
                flyerDef,
                target,
                destination,
                Props.flightEffecterDef,
                Props.landingSound,
                false, // 不携带物品
                null,  // 不覆盖起始位置
                parent, // 传递Ability对象
                new LocalTargetInfo(destination)
            );

            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, destination, map);
            }
        }

        /// <summary>
        /// 检查单元格是否可用于效果
        /// </summary>
        private bool CanUseCell(Pawn caster, IntVec3 cell)
        {
            if (caster == null || caster.Map == null)
                return false;

            if (!cell.InBounds(caster.Map))
                return false;

            if (!Props.affectCaster && cell == caster.Position)
                return false;

            if (!Props.canHitFilledCells && cell.Filled(caster.Map))
                return false;

            if (!cell.InHorDistOf(caster.Position, Props.range))
                return false;

            return true;
        }

        /// <summary>
        /// 绘制效果预览
        /// </summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !target.IsValid)
                return;
            
            // 绘制扇形区域
            List<IntVec3> cells = GetFanShapedCells(caster, target.Cell);
            GenDraw.DrawFieldEdges(cells, Color.red);
            
            // 绘制扇形边线
            DrawConeBoundaries(caster, target.Cell);
        }

        /// <summary>
        /// 绘制扇形边界线
        /// </summary>
        private void DrawConeBoundaries(Pawn caster, IntVec3 targetCell)
        {
            if (caster == null || caster.Map == null)
                return;
            
            IntVec3 casterPos = caster.Position;
            Vector3 casterVector = casterPos.ToVector3Shifted().Yto0();
            
            // 计算中心线
            float horizontalLength = (targetCell - casterPos).LengthHorizontal;
            float dirX = (targetCell.x - casterPos.x) / horizontalLength;
            float dirZ = (targetCell.z - casterPos.z) / horizontalLength;
            
            IntVec3 clampedTarget = targetCell;
            clampedTarget.x = Mathf.RoundToInt(casterPos.x + dirX * Props.range);
            clampedTarget.z = Mathf.RoundToInt(casterPos.z + dirZ * Props.range);
            
            float targetAngle = Vector3.SignedAngle(
                clampedTarget.ToVector3Shifted().Yto0() - casterVector, 
                Vector3.right, 
                Vector3.up);
            
            float halfWidth = Props.lineWidthEnd / 2f;
            float coneEdgeDistance = Mathf.Sqrt(
                Mathf.Pow((clampedTarget - casterPos).LengthHorizontal, 2f) + 
                Mathf.Pow(halfWidth, 2f));
            float halfAngle = Mathf.Rad2Deg * Mathf.Asin(halfWidth / coneEdgeDistance);
            halfAngle = Mathf.Min(halfAngle, Props.coneSizeDegrees / 2f);
            
            // 绘制两条边界线
            float leftAngle = targetAngle - halfAngle;
            float rightAngle = targetAngle + halfAngle;
            
            Vector3 leftDir = Quaternion.Euler(0, leftAngle, 0) * Vector3.right;
            Vector3 rightDir = Quaternion.Euler(0, rightAngle, 0) * Vector3.right;
            
            IntVec3 leftEnd = casterPos + new IntVec3(
                Mathf.RoundToInt(leftDir.x * Props.range),
                0,
                Mathf.RoundToInt(leftDir.z * Props.range)
            ).ClampInsideMap(caster.Map);
            
            IntVec3 rightEnd = casterPos + new IntVec3(
                Mathf.RoundToInt(rightDir.x * Props.range),
                0,
                Mathf.RoundToInt(rightDir.z * Props.range)
            ).ClampInsideMap(caster.Map);
            
            GenDraw.DrawLineBetween(casterPos.ToVector3Shifted(), leftEnd.ToVector3Shifted(), SimpleColor.White);
            GenDraw.DrawLineBetween(casterPos.ToVector3Shifted(), rightEnd.ToVector3Shifted(), SimpleColor.White);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
            
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
                return false;
            
            // 检查目标是否在范围内
            float distance = caster.Position.DistanceTo(target.Cell);
            if (distance > Props.range)
            {
                if (throwMessages)
                    Messages.Message("AbilityTargetOutOfRange".Translate(), caster, MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }
    }
}
