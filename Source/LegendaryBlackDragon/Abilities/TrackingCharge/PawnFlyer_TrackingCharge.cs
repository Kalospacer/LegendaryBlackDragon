using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;
using System.Linq;
using Verse.AI;
using System.Collections.Generic;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class PawnFlyer_TrackingCharge : PawnFlyer
    {
        // --- Public fields to be set by the Verb ---
        public float homingSpeed;
        public float initialDamage;
        public float damagePerTile;
        public float inertiaDistance;
        public DamageDef collisionDamageDef;
        public LocalTargetInfo primaryTarget;
        public float collisionRadius;
        public SoundDef impactSound;
        public bool damageHostileOnly;
        public int maxFlightTicks = 1000; // 最大飞行时间，防止无限飞行

        // --- Internal state ---
        private bool homing = true;
        private bool hasHitPrimaryTarget = false;
        private Vector3 exactPosition;
        private IntVec3? desiredLandingCell = null; // 新增：期望的降落位置
        private bool isLanding = false; // 新增：标记是否正在降落
        private bool positionAdjusted = false; // 新增：标记是否已调整位置
        private HashSet<Thing> alreadyDamaged = new HashSet<Thing>(); // 新增：记录已经造成伤害的目标
        private int lastPathDamageTick = 0; // 新增：上次路径伤害的tick
        private const int PATH_DAMAGE_INTERVAL = 2; // 新增：路径伤害间隔（每2帧检查一次）

        // --- Reflection Fields ---
        private static FieldInfo TicksFlyingInfo;
        private static FieldInfo TicksFlightTimeInfo;
        private static FieldInfo StartVecInfo;
        private static FieldInfo DestCellInfo;
        private static FieldInfo PawnWasDraftedInfo;
        private static FieldInfo PawnCanFireAtWillInfo;
        private static FieldInfo JobQueueInfo;
        private static FieldInfo InnerContainerInfo;

        static PawnFlyer_TrackingCharge()
        {
            TicksFlyingInfo = typeof(PawnFlyer).GetField("ticksFlying", BindingFlags.Instance | BindingFlags.NonPublic);
            TicksFlightTimeInfo = typeof(PawnFlyer).GetField("ticksFlightTime", BindingFlags.Instance | BindingFlags.NonPublic);
            StartVecInfo = typeof(PawnFlyer).GetField("startVec", BindingFlags.Instance | BindingFlags.NonPublic);
            DestCellInfo = typeof(PawnFlyer).GetField("destCell", BindingFlags.Instance | BindingFlags.NonPublic);
            PawnWasDraftedInfo = typeof(PawnFlyer).GetField("pawnWasDrafted", BindingFlags.NonPublic | BindingFlags.Instance);
            PawnCanFireAtWillInfo = typeof(PawnFlyer).GetField("pawnCanFireAtWill", BindingFlags.NonPublic | BindingFlags.Instance);
            JobQueueInfo = typeof(PawnFlyer).GetField("jobQueue", BindingFlags.NonPublic | BindingFlags.Instance);
            InnerContainerInfo = typeof(PawnFlyer).GetField("innerContainer", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // Custom initializer called by the Verb
        public void StartFlight(Pawn pawn, IntVec3 finalDest)
        {
            var innerContainer = (ThingOwner)InnerContainerInfo.GetValue(this);

            StartVecInfo.SetValue(this, pawn.TrueCenter());
            DestCellInfo.SetValue(this, finalDest);
            PawnWasDraftedInfo.SetValue(this, pawn.Drafted);
            if (pawn.drafter != null) PawnCanFireAtWillInfo.SetValue(this, pawn.drafter.FireAtWill);
            if (pawn.CurJob != null) pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced);
            JobQueueInfo.SetValue(this, pawn.jobs.CaptureAndClearJobQueue());

            if (pawn.Spawned) pawn.DeSpawn(DestroyMode.WillReplace);
            if (!innerContainer.TryAdd(pawn))
            {
                pawn.Destroy();
            }
        }
        
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                this.exactPosition = base.DrawPos;
                alreadyDamaged.Clear();
                lastPathDamageTick = 0;
            }
        }

        protected override void Tick()
        {
            // --- 安全防护：防止无限飞行 ---
            int ticksFlying = (int)TicksFlyingInfo.GetValue(this);
            if (ticksFlying > maxFlightTicks)
            {
                ForceLanding();
                return;
            }

            // --- 飞行中逻辑 ---
            if (homing && primaryTarget.HasThing && primaryTarget.Thing.Spawned)
            {
                // 更新目的地以引导飞行器
                DestCellInfo.SetValue(this, primaryTarget.Thing.Position);
            }

            // --- 路径伤害逻辑：飞行过程中持续造成伤害 ---
            ApplyPathDamage();

            // --- 主目标碰撞检测 ---
            if (!hasHitPrimaryTarget && primaryTarget.HasThing && primaryTarget.Thing.Spawned)
            {
                float distanceToTargetSq = (this.DrawPos - primaryTarget.Thing.DrawPos).sqrMagnitude;
                float collisionRadiusSq = this.collisionRadius * this.collisionRadius;
                
                if (distanceToTargetSq <= collisionRadiusSq)
                {
                    // --- 碰撞发生！ ---
                    ImpactPrimaryTarget();
                }
            }

            // --- 基础Tick逻辑 ---
            base.Tick();

            // --- 到达目的地后的处理 ---
            if (!homing && !isLanding)
            {
                HandlePostImpactMovement();
            }
        }

        // 修改：应用路径伤害（飞行过程中持续造成伤害）
        private void ApplyPathDamage()
        {
            int ticksFlying = (int)TicksFlyingInfo.GetValue(this);
            
            // 限制检查频率
            if (ticksFlying - lastPathDamageTick < PATH_DAMAGE_INTERVAL)
                return;
                
            lastPathDamageTick = ticksFlying;
            
            // 计算当前伤害值
            Vector3 startPosition = (Vector3)StartVecInfo.GetValue(this);
            float distanceTravelled = (this.DrawPos - startPosition).magnitude;
            float currentDamage = this.initialDamage + (distanceTravelled * this.damagePerTile);
            
            // 获取当前位置周围的所有物体
            var thingsInRadius = GenRadial.RadialDistinctThingsAround(this.Position, this.Map, this.collisionRadius, false).ToList();
            
            foreach (var thing in thingsInRadius)
            {
                // 跳过自己、飞行器和已经被伤害过的目标
                if (thing == this.FlyingPawn || thing == this || alreadyDamaged.Contains(thing))
                    continue;
                    
                // 如果是主目标，跳过（主目标有专门的碰撞检测）
                if (primaryTarget.HasThing && thing == primaryTarget.Thing && !hasHitPrimaryTarget)
                    continue;
                    
                // 检查是否需要伤害
                if (ShouldDamageThing(thing))
                {
                    // 创建伤害信息
                    var dinfo = new DamageInfo(this.collisionDamageDef, currentDamage, 1f, -1, this.FlyingPawn);
                    
                    // 应用伤害
                    thing.TakeDamage(dinfo);
                    
                    // 记录已经伤害过的目标
                    alreadyDamaged.Add(thing);
                    
                    // 播放音效（可选）
                    if (this.impactSound != null && thing is Pawn)
                    {
                        SoundStarter.PlayOneShot(this.impactSound, new TargetInfo(thing.Position, this.Map));
                    }
                    
                    if (Prefs.DevMode)
                    {
                    }
                }
            }
            
            // 可选：添加视觉效果
            if (ticksFlying % 10 == 0 && Prefs.DevMode)
            {
                FleckMaker.ThrowDustPuff(this.DrawPos + Gen.RandomHorizontalVector(0.5f), this.Map, 0.5f);
            }
        }

        // 新增：判断是否应该伤害物体
        private bool ShouldDamageThing(Thing thing)
        {
            if (thing == null || thing.Destroyed)
                return false;
                
            // 如果是生物
            if (thing is Pawn pawn)
            {
                if (pawn.Downed || pawn.Dead)
                    return false;
                    
                // 检查是否只伤害敌对目标
                if (this.damageHostileOnly && !pawn.HostileTo(this.FlyingPawn))
                    return false;
                    
                return true;
            }
            // 如果是建筑
            else if (thing.def.destroyable && thing.def.building != null)
            {
                return true;
            }
            // 如果是门或其他障碍物
            else if (thing.def.passability == Traversability.PassThroughOnly)
            {
                return true;
            }
            
            return false;
        }

        // 修改：处理主目标碰撞
        private void ImpactPrimaryTarget()
        {
            // 播放音效
            if (this.impactSound != null)
            {
                SoundStarter.PlayOneShot(this.impactSound, new TargetInfo(this.Position, this.Map));
            }

            // 计算伤害
            Vector3 startPosition = (Vector3)StartVecInfo.GetValue(this);
            float distance = (this.DrawPos - startPosition).magnitude;
            float calculatedDamage = this.initialDamage + (distance * this.damagePerTile);
            var dinfo = new DamageInfo(this.collisionDamageDef, calculatedDamage, 1f, -1, this.FlyingPawn);

            primaryTarget.Thing.TakeDamage(dinfo);
            hasHitPrimaryTarget = true;
            
            // 将主目标添加到已伤害列表，避免后续路径伤害重复伤害
            if (!alreadyDamaged.Contains(primaryTarget.Thing))
            {
                alreadyDamaged.Add(primaryTarget.Thing);
            }
            
            homing = false;

            // 计算期望的降落位置（目标身后一格）
            CalculateDesiredLandingCell();
        }

        // 新增：计算期望的降落位置
        private void CalculateDesiredLandingCell()
        {
            if (!primaryTarget.HasThing || !primaryTarget.Thing.Spawned)
            {
                // 如果没有主目标，就停在当前位置
                desiredLandingCell = this.Position;
                return;
            }

            Vector3 startPos = (Vector3)StartVecInfo.GetValue(this);
            Vector3 targetPos = primaryTarget.Thing.DrawPos;
            
            // 计算从起点到目标的方向
            Vector3 direction = (targetPos - startPos).normalized;
            
            // 计算目标身后一格的位置（延长线方向）
            Vector3 behindTargetPos = targetPos + direction * 1.0f; // 1格距离
            
            IntVec3 candidateCell = behindTargetPos.ToIntVec3();
            
            // 验证这个位置是否可用
            if (IsValidLandingCell(candidateCell))
            {
                desiredLandingCell = candidateCell;
            }
            else
            {
                // 如果不可用，寻找最近的可用位置
                desiredLandingCell = FindNearbyLandableCell(candidateCell);
            }

            if (desiredLandingCell.HasValue)
            {
                // 立即设置目的地
                DestCellInfo.SetValue(this, desiredLandingCell.Value);
            }
        }

        // 新增：验证单元格是否适合降落
        private bool IsValidLandingCell(IntVec3 cell)
        {
            if (cell == this.Position)
                return true;
                
            if (!cell.InBounds(this.Map))
                return false;
                
            // 检查单元格是否可行走
            if (!cell.Walkable(this.Map))
                return false;
                
            // 检查是否有障碍物
            if (cell.GetFirstThing<Building>(this.Map) != null)
                return false;
                
            // 检查是否有其他生物
            Pawn pawnAtCell = cell.GetFirstPawn(this.Map);
            if (pawnAtCell != null && pawnAtCell != this.FlyingPawn)
                return false;
                
            return true;
        }

        // 新增：寻找附近的可用降落单元格
        private IntVec3 FindNearbyLandableCell(IntVec3 centerCell)
        {
            // 优先检查当前位置
            if (IsValidLandingCell(this.Position))
                return this.Position;
                
            // 从近到远搜索可用单元格
            for (int radius = 1; radius <= 5; radius++)
            {
                List<IntVec3> cellsInRadius = GenRadial.RadialCellsAround(centerCell, radius, true).ToList();
                
                // 按与中心距离排序
                cellsInRadius.Sort((a, b) => 
                    (a - centerCell).LengthHorizontalSquared.CompareTo((b - centerCell).LengthHorizontalSquared));
                
                foreach (var cell in cellsInRadius)
                {
                    if (IsValidLandingCell(cell))
                        return cell;
                }
            }
            
            // 如果都找不到，返回最近的可行走单元格
            CellFinder.TryFindRandomCellNear(centerCell, this.Map, 10, 
                cell => cell.Walkable(this.Map), out IntVec3 fallbackCell);
            
            return fallbackCell;
        }

        // 新增：处理撞击后的移动
        private void HandlePostImpactMovement()
        {
            int ticksFlying = (int)TicksFlyingInfo.GetValue(this);
            int ticksFlightTime = (int)TicksFlightTimeInfo.GetValue(this);
            
            // 如果已经到达目的地或接近目的地，强制降落
            if (ticksFlying >= ticksFlightTime - 5) // 提前几tick准备降落
            {
                isLanding = true;
                
                // 确保目的地是我们期望的位置
                if (desiredLandingCell.HasValue && this.Position != desiredLandingCell.Value && !positionAdjusted)
                {
                    // 如果不在期望的位置，尝试移动到那里
                    if (this.Position.DistanceTo(desiredLandingCell.Value) <= 2)
                    {
                        // 很近，直接设置位置
                        this.Position = desiredLandingCell.Value;
                        positionAdjusted = true;
                    }
                }
            }
            
            // 额外的距离检查：如果已经超过惯性距离，强制结束飞行
            Vector3 startPosition = (Vector3)StartVecInfo.GetValue(this);
            float traveledDistance = (this.DrawPos - startPosition).magnitude;
            float maxAllowedDistance = (primaryTarget.HasThing && primaryTarget.Thing.Spawned) 
                ? (startPosition - primaryTarget.Thing.DrawPos).magnitude + inertiaDistance + 2.0f // 多加2格容差
                : inertiaDistance * 2;
                
            if (traveledDistance > maxAllowedDistance)
            {
                ForceLanding();
            }
        }

        // 新增：强制降落
        private void ForceLanding()
        {
            // 如果还没有计算降落位置，现在计算
            if (!desiredLandingCell.HasValue)
            {
                CalculateDesiredLandingCell();
            }
            
            // 确保有降落位置
            if (!desiredLandingCell.HasValue)
            {
                desiredLandingCell = this.Position;
            }
            
            // 立即设置目的地并强制完成飞行
            DestCellInfo.SetValue(this, desiredLandingCell.Value);
            int ticksFlightTime = (int)TicksFlightTimeInfo.GetValue(this);
            int currentTicksFlying = (int)TicksFlyingInfo.GetValue(this);
            
            // 如果已经飞了很长时间，直接设置完成
            if (currentTicksFlying >= ticksFlightTime - 1)
            {
                TicksFlyingInfo.SetValue(this, ticksFlightTime);
                isLanding = true;
            }
        }

        // 新增：在飞行器销毁前调整位置
        protected override void TickInterval(int delta)
        {
            // 先让基类处理基础逻辑
            base.TickInterval(delta);
            
            // 如果我们已经击中了目标，并且已经设置了期望的降落位置
            // 在飞行即将结束时，确保位置正确
            int ticksFlying = (int)TicksFlyingInfo.GetValue(this);
            int ticksFlightTime = (int)TicksFlightTimeInfo.GetValue(this);
            
            if (hasHitPrimaryTarget && desiredLandingCell.HasValue && ticksFlying >= ticksFlightTime - 2 && !positionAdjusted)
            {
                // 在飞行结束前2tick，尝试调整到期望位置
                if (this.Position != desiredLandingCell.Value && IsValidLandingCell(desiredLandingCell.Value))
                {
                    this.Position = desiredLandingCell.Value;
                    positionAdjusted = true;
                }
            }
        }

        // 修改：在降落时确保驾驶员在正确位置
        protected override void RespawnPawn()
        {
            // 降落前最后的位置修正
            if (desiredLandingCell.HasValue && this.Position != desiredLandingCell.Value && !positionAdjusted)
            {
                // 检查期望位置是否可用
                if (IsValidLandingCell(desiredLandingCell.Value))
                {
                    this.Position = desiredLandingCell.Value;
                    positionAdjusted = true;
                }
            }
            
            base.RespawnPawn();
            
            // 确保驾驶员处于正确状态
            Pawn flyingPawn = this.FlyingPawn;
            if (flyingPawn != null && flyingPawn.Spawned)
            {
                // 如果驾驶员不在期望位置，尝试移动
                if (desiredLandingCell.HasValue && flyingPawn.Position != desiredLandingCell.Value && IsValidLandingCell(desiredLandingCell.Value))
                {
                    flyingPawn.Position = desiredLandingCell.Value;
                }
                
                // 停止所有工作，防止继续移动
                flyingPawn.pather?.StopDead();
                flyingPawn.jobs?.StopAll();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref homing, "homing", true);
            Scribe_Values.Look(ref hasHitPrimaryTarget, "hasHitPrimaryTarget", false);
            Scribe_Values.Look(ref isLanding, "isLanding", false);
            Scribe_Values.Look(ref positionAdjusted, "positionAdjusted", false);
            Scribe_Values.Look(ref lastPathDamageTick, "lastPathDamageTick", 0);
            
            // 保存已伤害的目标列表
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<Thing> damagedThingsList = alreadyDamaged.ToList();
                Scribe_Collections.Look(ref damagedThingsList, "damagedThings", LookMode.Reference);
                
                if (desiredLandingCell.HasValue)
                {
                    IntVec3 cell = desiredLandingCell.Value;
                    Scribe_Values.Look(ref cell, "desiredLandingCell");
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<Thing> damagedThingsList = null;
                Scribe_Collections.Look(ref damagedThingsList, "damagedThings", LookMode.Reference);
                if (damagedThingsList != null)
                {
                    alreadyDamaged = new HashSet<Thing>(damagedThingsList);
                }
                
                IntVec3 cell = IntVec3.Invalid;
                Scribe_Values.Look(ref cell, "desiredLandingCell");
                if (cell.IsValid)
                {
                    desiredLandingCell = cell;
                }
            }
        }
    }
}
