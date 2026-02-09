using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_FanShapedStunKnockback : CompAbilityEffect
    {
        private readonly List<IntVec3> tmpCells = new List<IntVec3>();
        private Effecter effecter;
        
        public new CompProperties_AbilityFanShapedStunKnockback Props => (CompProperties_AbilityFanShapedStunKnockback)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !target.IsValid)
                return;
            
            // 1. 获取扇形区域内的所有单元格
            List<IntVec3> affectedCells = GetFanShapedCells(caster, target.Cell);
            
            // 2. 收集区域内的所有目标
            var affectedTargets = CollectAffectedTargets(caster, affectedCells);
            
            // 3. 对每个目标应用效果
            foreach (Thing targetThing in affectedTargets)
            {
                if (targetThing != null && !targetThing.Destroyed && targetThing.Spawned)
                {
                    if (targetThing is Pawn pawn)
                    {
                        ApplyEffectToPawn(caster, pawn, target);
                    }
                    else if (Props.affectNonPawnThings)
                    {
                        ApplyEffectToNonPawnThing(caster, targetThing, target);
                    }
                }
            }
            
            // 4. 播放整体攻击效果（参考Verb_MeleeAttack_Cleave）
            PlayMainAttackEffect(caster, target);
        }
        
        /// <summary>
        /// 播放主要攻击效果
        /// </summary>
        private void PlayMainAttackEffect(Pawn caster, LocalTargetInfo target)
        {
            if (caster == null || caster.Map == null || !target.IsValid)
                return;
            
            // 播放主要攻击效果
            if (Props.impactEffecter != null)
            {
                effecter = Props.impactEffecter.Spawn();
                // 关键修复：第一个参数是施法者位置，第二个参数是目标位置
                effecter.Trigger(new TargetInfo(caster.Position, caster.Map), target.ToTargetInfo(caster.Map));
                effecter.Cleanup();
                effecter = null;
            }
            
            // 播放攻击音效
            if (Props.impactSound != null)
            {
                Props.impactSound.PlayOneShot(new TargetInfo(target.Cell, caster.Map));
            }
        }

        /// <summary>
        /// 收集扇形区域内的所有目标（包括Pawn和非Pawn物体）
        /// </summary>
        private List<Thing> CollectAffectedTargets(Pawn caster, List<IntVec3> cells)
        {
            List<Thing> targets = new List<Thing>();
            HashSet<Thing> addedThings = new HashSet<Thing>();
            
            if (caster == null || caster.Map == null || cells == null)
                return targets;
            
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(caster.Map))
                    continue;
                    
                List<Thing> things = cell.GetThingList(caster.Map);
                foreach (Thing thing in things)
                {
                    if (thing == null || addedThings.Contains(thing))
                        continue;
                    
                    // 检查是否为施法者
                    if (!Props.affectCaster && thing == caster)
                        continue;
                    
                    // 检查是否需要视线
                    if (Props.requireLineOfSightToTarget && !GenSight.LineOfSight(caster.Position, cell, caster.Map))
                        continue;
                    
                    // Pawn的处理
                    if (thing is Pawn pawn)
                    {
                        // 检查是否为敌人
                        if (Props.onlyAffectEnemies && !pawn.HostileTo(caster))
                            continue;
                            
                        targets.Add(pawn);
                        addedThings.Add(pawn);
                    }
                    // 非Pawn物体的处理
                    else if (Props.affectNonPawnThings && thing is ThingWithComps thingWithComps)
                    {
                        // 检查是否为敌人（如果物体有派系）
                        if (Props.onlyAffectEnemies)
                        {
                            bool isEnemy = IsThingEnemy(caster, thingWithComps);
                            if (!isEnemy)
                                continue;
                        }
                        
                        // 检查是否可以被伤害
                        if (Props.canDamageNonPawnThings && !CanBeDamaged(thingWithComps))
                            continue;
                            
                        targets.Add(thingWithComps);
                        addedThings.Add(thingWithComps);
                    }
                }
            }
            
            return targets;
        }
        
        /// <summary>
        /// 检查物体是否为敌人
        /// </summary>
        private bool IsThingEnemy(Pawn caster, Thing thing)
        {
            // 如果物体有派系，检查是否敌对
            if (thing.Faction != null)
            {
                return caster.HostileTo(thing);
            }
            
            // 如果物体是建筑且没有派系，根据设置决定
            // 默认情况下，视为中立（只有当onlyAffectEnemies为false时才影响）
            return false;
        }
        
        /// <summary>
        /// 检查物体是否可以被伤害
        /// </summary>
        private bool CanBeDamaged(Thing thing)
        {
            // 检查是否有生命值组件
            if (thing.def.useHitPoints)
            {
                // 检查是否被摧毁或已死亡
                if (thing.Destroyed || thing.HitPoints <= 0)
                    return false;
                    
                // 检查是否可以承受伤害
                if (thing.def.destroyable)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 对非Pawn物体应用效果
        /// </summary>
        private void ApplyEffectToNonPawnThing(Pawn caster, Thing targetThing, LocalTargetInfo targetInfo)
        {
            if (targetThing == null || caster == null)
                return;
            
            // 1. 造成伤害
            ApplyDamageToNonPawn(caster, targetThing);
            
            // 注意：非Pawn物体不进行击退，也不眩晕
        }
        
        /// <summary>
        /// 对非Pawn物体造成伤害
        /// </summary>
        private void ApplyDamageToNonPawn(Pawn caster, Thing targetThing)
        {
            if (!Props.canDamageNonPawnThings)
                return;
            
            // 获取调整后的伤害值
            float adjustedDamage = GetAdjustedDamage(caster);
            
            // 应用非Pawn物体的伤害倍率
            float finalDamage = adjustedDamage * Props.nonPawnDamageMultiplier;
            
            // 检查目标是否可以被伤害
            if (targetThing.def.useHitPoints && targetThing.HitPoints > 0)
            {
                // 创建伤害信息
                DamageInfo damageInfo = new DamageInfo(
                    Props.damageDef,
                    finalDamage,
                    Props.armorPenetration,
                    -1f,
                    caster,
                    null
                );
                
                // 应用伤害
                targetThing.TakeDamage(damageInfo);
                
                // 播放个体命中效果
                if (Props.applySpecialEffectsToNonPawn && Props.impactEffecter != null && caster.Map != null)
                {
                    Effecter effect = Props.impactEffecter.Spawn();
                    // 关键修复：第一个参数是施法者，第二个参数是目标
                    effect.Trigger(new TargetInfo(caster.Position, caster.Map), new TargetInfo(targetThing.Position, caster.Map));
                    effect.Cleanup();
                }
                
                // 播放个体命中音效
                if (Props.applySpecialEffectsToNonPawn && Props.impactSound != null && caster.Map != null)
                {
                    Props.impactSound.PlayOneShot(new TargetInfo(targetThing.Position, caster.Map));
                }
                
                // 记录日志（调试用）
                if (Prefs.DevMode)
                {
                    Log.Message($"[CompAbilityEffect_FanShapedStunKnockback] 对非Pawn物体 {targetThing.LabelCap} 造成 {finalDamage:F1} 伤害");
                }
            }
        }

        /// <summary>
        /// 获取伤害系数
        /// </summary>
        private float GetDamageMultiplier(Pawn caster)
        {
            if (caster == null) return 1f;

            if (Props.multiplyDamageByMeleeFactor)
            {
                if (Props.damageMultiplierStat != null)
                {
                    return caster.GetStatValue(Props.damageMultiplierStat);
                }
                return caster.GetStatValue(StatDefOf.MeleeDamageFactor);
            }

            return 1f;
        }
        /// <summary>
        /// 获取眩晕时间系数
        /// </summary>
        private float GetStunMultiplier(Pawn caster)
        {
            if (caster == null) return 1f;

            if (Props.multiplyStunTimeByMeleeFactor)
            {
                if (Props.stunMultiplierStat != null)
                {
                    return caster.GetStatValue(Props.stunMultiplierStat);
                }
                return caster.GetStatValue(StatDefOf.MeleeDamageFactor);
            }

            return 1f;
        }
        /// <summary>
        /// 获取调整后的伤害值
        /// </summary>
        private float GetAdjustedDamage(Pawn caster)
        {
            float baseDamage = Props.damageAmount;
            float multiplier = GetDamageMultiplier(caster);
            return baseDamage * multiplier;
        }
        /// <summary>
        /// 获取调整后的眩晕时间
        /// </summary>
        private int GetAdjustedStunTicks(Pawn caster)
        {
            int baseStunTicks = Props.stunTicks;
            float multiplier = GetStunMultiplier(caster);
            return Mathf.RoundToInt(baseStunTicks * multiplier);
        }
        /// <summary>
        /// 显示伤害和眩晕加成信息（用于预览）
        /// </summary>
        public string GetAdjustedDamageAndStunInfo(Pawn caster)
        {
            if (caster == null) return string.Empty;

            float damageMultiplier = GetDamageMultiplier(caster);
            float stunMultiplier = GetStunMultiplier(caster);

            // 如果都不需要乘以系数，则不显示信息
            if (damageMultiplier == 1f && stunMultiplier == 1f)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LBD_AdjustedEffects".Translate());

            if (damageMultiplier != 1f)
            {
                float adjustedDamage = Props.damageAmount * damageMultiplier;
                sb.AppendLine("LBD_AdjustedDamage".Translate(Props.damageAmount, adjustedDamage));
            }

            if (stunMultiplier != 1f)
            {
                int adjustedStunTicks = Mathf.RoundToInt(Props.stunTicks * stunMultiplier);
                float baseStunSeconds = Props.stunTicks / 60f;
                float adjustedStunSeconds = adjustedStunTicks / 60f;
                sb.AppendLine("LBD_AdjustedStun".Translate(baseStunSeconds, adjustedStunSeconds));
            }

            return sb.ToString();
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
        /// 对单个Pawn应用效果
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
            // 获取调整后的伤害和眩晕时间
            float adjustedDamage = GetAdjustedDamage(caster);
            int adjustedStunTicks = GetAdjustedStunTicks(caster);

            // 创建伤害信息
            DamageInfo damageInfo = new DamageInfo(
                Props.damageDef,
                adjustedDamage,
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

            // 播放个体命中效果（可选，因为已经有主要攻击效果）
            if (Props.applySpecialEffectsToNonPawn && Props.impactEffecter != null && caster.Map != null)
            {
                Effecter effect = Props.impactEffecter.Spawn();
                // 关键修复：第一个参数是施法者，第二个参数是目标
                effect.Trigger(new TargetInfo(caster.Position, caster.Map), new TargetInfo(target.Position, caster.Map));
                effect.Cleanup();
            }

            // 应用眩晕 - 只在目标存活时应用
            if (adjustedStunTicks > 0 && !target.Dead)
            {
                target.stances?.stunner?.StunFor(adjustedStunTicks, caster);
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
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CompAbilityEffect_FanShapedStunKnockback 调试信息 ===");
            sb.AppendLine($"影响非Pawn物体: {Props.affectNonPawnThings}");
            sb.AppendLine($"对非Pawn物体造成伤害: {Props.canDamageNonPawnThings}");
            sb.AppendLine($"非Pawn物体伤害倍率: {Props.nonPawnDamageMultiplier:F2}");
            sb.AppendLine($"对非Pawn物体应用特殊效果: {Props.applySpecialEffectsToNonPawn}");
            sb.AppendLine($"主要攻击效果器: {Props.impactEffecter?.defName ?? "null"}");
            return sb.ToString();
        }
    }
}
