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
        
        // === 新增：调试计数器 ===
        private static int knockbackAttempts = 0;
        private static int knockbackSuccesses = 0;
        private static int knockbackFailures = 0;
        
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
            
            // 4. 播放整体攻击效果
            PlayMainAttackEffect(caster, target);
            
            // 5. 记录调试信息
            if (DebugSettings.godMode)
            {
                Log.Message($"[FanShapedKnockback] Applied to {affectedTargets.Count} targets. " +
                           $"Total attempts: {knockbackAttempts}, Successes: {knockbackSuccesses}, Failures: {knockbackFailures}");
            }
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
        /// 收集扇形区域内的所有目标
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
                        // 检查是否为敌人
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
            if (thing.Faction != null)
            {
                return caster.HostileTo(thing);
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查物体是否可以被伤害
        /// </summary>
        private bool CanBeDamaged(Thing thing)
        {
            if (thing.def.useHitPoints)
            {
                if (thing.Destroyed || thing.HitPoints <= 0)
                    return false;
                    
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
            
            ApplyDamageToNonPawn(caster, targetThing);
        }
        
        /// <summary>
        /// 对非Pawn物体造成伤害
        /// </summary>
        private void ApplyDamageToNonPawn(Pawn caster, Thing targetThing)
        {
            if (!Props.canDamageNonPawnThings)
                return;
            
            float adjustedDamage = GetAdjustedDamage(caster);
            float finalDamage = adjustedDamage * Props.nonPawnDamageMultiplier;
            
            if (targetThing.def.useHitPoints && targetThing.HitPoints > 0)
            {
                DamageInfo damageInfo = new DamageInfo(
                    Props.damageDef,
                    finalDamage,
                    Props.armorPenetration,
                    -1f,
                    caster,
                    null
                );
                
                targetThing.TakeDamage(damageInfo);
                
                if (Props.applySpecialEffectsToNonPawn && Props.impactEffecter != null && caster.Map != null)
                {
                    Effecter effect = Props.impactEffecter.Spawn();
                    effect.Trigger(new TargetInfo(caster.Position, caster.Map), new TargetInfo(targetThing.Position, caster.Map));
                    effect.Cleanup();
                }
                
                if (Props.applySpecialEffectsToNonPawn && Props.impactSound != null && caster.Map != null)
                {
                    Props.impactSound.PlayOneShot(new TargetInfo(targetThing.Position, caster.Map));
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
        /// 显示伤害和眩晕加成信息
        /// </summary>
        public string GetAdjustedDamageAndStunInfo(Pawn caster)
        {
            if (caster == null) return string.Empty;

            float damageMultiplier = GetDamageMultiplier(caster);
            float stunMultiplier = GetStunMultiplier(caster);

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
            
            if (casterPos == clampedTarget)
                return tmpCells;
            
            Vector3 casterVector = casterPos.ToVector3Shifted().Yto0();
            
            float horizontalLength = (clampedTarget - casterPos).LengthHorizontal;
            float dirX = (clampedTarget.x - casterPos.x) / horizontalLength;
            float dirZ = (clampedTarget.z - casterPos.z) / horizontalLength;
            
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
            
            int radialCellCount = GenRadial.NumCellsInRadius(Props.range);
            for (int i = 0; i < radialCellCount; i++)
            {
                IntVec3 cell = casterPos + GenRadial.RadialPattern[i];
                
                if (!CanUseCell(caster, cell))
                    continue;
                
                float cellAngle = Vector3.SignedAngle(
                    cell.ToVector3Shifted().Yto0() - casterVector, 
                    Vector3.right, 
                    Vector3.up);
                
                if (Mathf.Abs(Mathf.DeltaAngle(cellAngle, targetAngle)) <= halfAngle)
                {
                    tmpCells.Add(cell);
                }
            }
            
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
            bool targetDied = ApplyDamageAndStun(caster, target);
            
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
            float adjustedDamage = GetAdjustedDamage(caster);
            int adjustedStunTicks = GetAdjustedStunTicks(caster);

            DamageInfo damageInfo = new DamageInfo(
                Props.damageDef,
                adjustedDamage,
                Props.armorPenetration,
                -1f,
                caster,
                null
            );
            
            target.TakeDamage(damageInfo);

            bool targetDied = target.Dead || target.Destroyed;

            if (targetDied)
            {
                return true;
            }

            if (Props.applySpecialEffectsToNonPawn && Props.impactEffecter != null && caster.Map != null)
            {
                Effecter effect = Props.impactEffecter.Spawn();
                effect.Trigger(new TargetInfo(caster.Position, caster.Map), new TargetInfo(target.Position, caster.Map));
                effect.Cleanup();
            }

            if (adjustedStunTicks > 0 && !target.Dead)
            {
                target.stances?.stunner?.StunFor(adjustedStunTicks, caster);
            }
            
            return false;
        }

        /// <summary>
        /// === 新增：击退位置提前校验 ===
        /// </summary>
        private bool IsValidKnockbackDestination(IntVec3 destination, Map map, Pawn victim, Pawn caster)
        {
            // 基本空值检查
            if (destination == null || map == null || victim == null || caster == null)
            {
                Log.Warning("[FanShapedKnockback] Invalid parameters for destination validation");
                return false;
            }

            // 检查目的地是否在游戏世界内
            if (!destination.IsValid)
            {
                Log.Warning($"[FanShapedKnockback] Destination {destination} is invalid");
                return false;
            }

            // 检查目的地是否在地图范围内
            if (!destination.InBounds(map))
            {
                Log.Warning($"[FanShapedKnockback] Destination {destination} is out of map bounds");
                return false;
            }

            // 检查目的地是否可站立
            if (!destination.Standable(map))
            {
                Log.Warning($"[FanShapedKnockback] Destination {destination} is not standable");
                return false;
            }

            // 检查目的地是否有其他pawn（避免重叠）
            Pawn existingPawn = destination.GetFirstPawn(map);
            if (existingPawn != null && existingPawn != victim)
            {
                Log.Warning($"[FanShapedKnockback] Destination {destination} already occupied by {existingPawn.Label}");
                return false;
            }

            // 检查目的地是否有不可穿过的建筑或障碍物
            Building building = destination.GetEdifice(map);
            if (building != null && building.def.passability == Traversability.Impassable)
            {
                if (!Props.canKnockbackIntoWalls)
                {
                    Log.Warning($"[FanShapedKnockback] Destination {destination} has impassable building: {building.Label}");
                    return false;
                }
            }

            // 检查视线要求
            if (Props.requireLineOfSight && !GenSight.LineOfSight(victim.Position, destination, map))
            {
                Log.Warning($"[FanShapedKnockback] No line of sight from {victim.Position} to {destination}");
                return false;
            }

            // 检查目的地是否在水体中
            TerrainDef terrain = destination.GetTerrain(map);
            if (terrain != null && terrain.IsWater)
            {
                Log.Warning($"[FanShapedKnockback] Destination {destination} is in water terrain: {terrain.defName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// === 修改后的执行击退方法 ===
        /// </summary>
        private void PerformKnockback(Pawn caster, Pawn target)
        {
            knockbackAttempts++;
            
            if (target == null || target.Destroyed || target.Dead || caster.Map == null)
            {
                Log.Warning($"[FanShapedKnockback] Knockback pre-check failed for {target?.Label ?? "null"}");
                knockbackFailures++;
                return;
            }

            // 计算击退方向
            IntVec3 knockbackDirection = CalculateKnockbackDirection(caster, target.Position);
            
            // 寻找最远的可站立击退位置
            IntVec3 knockbackDestination = FindFarthestStandablePosition(caster, target, knockbackDirection);
            
            // 提前校验目的地
            if (IsValidKnockbackDestination(knockbackDestination, caster.Map, target, caster))
            {
                if (knockbackDestination != target.Position)
                {
                    bool flyerCreated = TryCreateKnockbackFlyer(caster, target, knockbackDestination);
                    if (flyerCreated)
                    {
                        knockbackSuccesses++;
                        Log.Message($"[FanShapedKnockback] Successfully knocked back {target.Label} to {knockbackDestination}");
                    }
                    else
                    {
                        knockbackFailures++;
                    }
                }
                else
                {
                    Log.Warning($"[FanShapedKnockback] Destination same as current position for {target.Label}");
                    knockbackFailures++;
                }
            }
            else
            {
                Log.Warning($"[FanShapedKnockback] Invalid knockback destination for {target.Label}: {knockbackDestination}");
                knockbackFailures++;
            }
        }

        /// <summary>
        /// === 新增：安全创建击退飞行器 ===
        /// </summary>
        private bool TryCreateKnockbackFlyer(Pawn caster, Pawn target, IntVec3 destination)
        {
            try
            {
                Map map = caster.Map;
                
                if (!IsValidKnockbackDestination(destination, map, target, caster))
                {
                    Log.Error($"[FanShapedKnockback] Final destination validation failed for {target.Label} at {destination}");
                    return false;
                }

                // 使用自定义飞行器或默认飞行器
                ThingDef flyerDef = Props.knockbackFlyerDef ?? ThingDefOf.PawnFlyer;
                
                // 创建飞行器
                PawnFlyer flyer = PawnFlyer.MakeFlyer(
                    flyerDef,
                    target,
                    destination,
                    Props.flightEffecterDef,
                    Props.landingSound,
                    false,
                    null,
                    parent,
                    new LocalTargetInfo(destination)
                );

                if (flyer == null)
                {
                    Log.Error($"[FanShapedKnockback] Failed to create PawnFlyer for {target.Label}");
                    return false;
                }

                // 检查PawnFlyer是否有效
                if (flyer.DestinationPos == null)
                {
                    Log.Error($"[FanShapedKnockback] Created PawnFlyer has invalid destination: {flyer.DestinationPos}");
                    flyer.Destroy(DestroyMode.Vanish);
                    return false;
                }

                // 生成PawnFlyer
                GenSpawn.Spawn(flyer, destination, map);
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FanShapedKnockback] Exception creating PawnFlyer for {target?.Label ?? "null"}: {ex}");
                return false;
            }
        }


        /// <summary>
        /// 计算击退方向
        /// </summary>
        private IntVec3 CalculateKnockbackDirection(Pawn caster, IntVec3 targetPosition)
        {
            IntVec3 direction = targetPosition - caster.Position;
            
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
        /// === 修改后的寻找最远可站立位置方法 ===
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
                
                // 使用新的校验方法
                if (IsValidKnockbackDestination(testPos, map, target, caster))
                {
                    farthestValidPos = testPos;
                    break;
                }
            }

            return farthestValidPos;
        }

        /// <summary>
        /// === 新增：检查格子是否可站立且没有其他Pawn（兼容旧代码）===
        /// </summary>
        private bool IsCellStandableAndEmpty(Pawn caster, Pawn target, IntVec3 cell, Map map)
        {
            return IsValidKnockbackDestination(cell, map, target, caster);
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
