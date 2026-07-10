using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 纵火狂的火焰渴望 Need
    /// </summary>
    public class Need_BlackDragon : Need
    {
        // === 字段 ===
        
        /// <summary>
        /// 上次点火时间（ticks）
        /// </summary>
        private int lastFireStartTick = -1;
        
        /// <summary>
        /// 当前阶段
        /// </summary>
        private BlackDragonStage currentStage = BlackDragonStage.Stage4;
        
        /// <summary>
        /// 在当前阶段停留的时间（ticks）
        /// </summary>
        private int currentStageDuration = 0;
        
        /// <summary>
        /// 缓存当前 Thought
        /// </summary>
        private Thought_Memory currentThought = null;
        
        /// <summary>
        /// 是否正在寻找点火目标
        /// </summary>
        private bool isSearchingForTarget = false;
        
        /// <summary>
        /// 寻找目标的重试次数
        /// </summary>
        private int searchRetryCount = 0;
        
        /// <summary>
        /// 上次尝试寻找目标的时间（ticks）
        /// </summary>
        private int lastSearchTick = -1;
        
        /// <summary>
        /// 寻找目标的冷却时间（ticks）
        /// </summary>
        private const int SearchCooldownTicks = 120; // 2秒
        
        // === 属性 ===
        
        /// <summary>
        /// 获取 ModExtension
        /// </summary>
        public NeedExtension_BlackDragon Extension => def.GetModExtension<NeedExtension_BlackDragon>();
        
        /// <summary>
        /// 当前阶段
        /// </summary>
        public BlackDragonStage CurrentStage
        {
            get => currentStage;
            private set
            {
                if (currentStage != value)
                {
                    currentStage = value;
                    currentStageDuration = 0;
                    UpdateThought();
                }
            }
        }
        
        /// <summary>
        /// 是否在冷却中
        /// </summary>
        public bool IsOnCooldown
        {
            get
            {
                if (lastFireStartTick < 0 || Extension == null)
                    return false;
                    
                int cooldown = Extension.fireCooldownTicks;
                return Find.TickManager.TicksGame - lastFireStartTick < cooldown;
            }
        }
        
        /// <summary>
        /// 冷却剩余时间（秒）
        /// </summary>
        public float CooldownSecondsRemaining
        {
            get
            {
                if (!IsOnCooldown || Extension == null)
                    return 0f;
                    
                int elapsed = Find.TickManager.TicksGame - lastFireStartTick;
                int remaining = Extension.fireCooldownTicks - elapsed;
                return remaining / 60f;
            }
        }
        
        /// <summary>
        /// 是否应该尝试点火（基于Need值和配置）
        /// </summary>
        public bool ShouldAttemptFireStart
        {
            get
            {
                if (Extension == null || !Extension.allowFireStarting)
                    return false;
                    
                if (IsOnCooldown)
                    return false;
                    
                if (pawn.Downed || pawn.Dead || !pawn.Spawned)
                    return false;
                    
                // 检查当前Need是否低于阈值
                float threshold = Extension.minFireStartingNeed;
                
                // 如果启用了紧急点火且Need极低，也触发
                if (Extension.emergencyFireStarting && CurLevelPercentage <= Extension.emergencyThreshold)
                    return true;
                    
                return CurLevelPercentage <= threshold;
            }
        }
        
        /// <summary>
        /// 是否正在执行点火任务
        /// </summary>
        public bool IsExecutingFireStartJob
        {
            get
            {
                if (pawn == null || pawn.CurJob == null)
                    return false;
                    
                return pawn.CurJob.def == LBD_DefOf.LBD_Job_RandomStartFire;
            }
        }
        
        /// <summary>
        /// 是否拥有火焰活动 Hediff
        /// </summary>
        public bool HasFireActivityHediff
        {
            get
            {
                if (Extension == null || Extension.fireActivityHediff == null)
                    return false;
                    
                return pawn.health?.hediffSet?.HasHediff(Extension.fireActivityHediff) == true;
            }
        }
        
        // === 构造函数 ===
        
        public Need_BlackDragon(Pawn pawn) : base(pawn)
        {
            // 初始化时设置较高值，让纵火狂开始时比较满足
            CurLevelPercentage = 0.8f;
        }
        
        // === 序列化 ===
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref lastFireStartTick, "lastFireStartTick", -1);
            Scribe_Values.Look(ref currentStage, "currentStage", BlackDragonStage.Stage4);
            Scribe_Values.Look(ref currentStageDuration, "currentStageDuration", 0);
            Scribe_Values.Look(ref isSearchingForTarget, "isSearchingForTarget", false);
            Scribe_Values.Look(ref searchRetryCount, "searchRetryCount", 0);
            Scribe_Values.Look(ref lastSearchTick, "lastSearchTick", -1);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                UpdateThought();
            }
        }
        
        // === Need 更新 ===
        
        public override void NeedInterval()
        {
            if (Extension == null || pawn == null || pawn.Dead)
                return;
                
            // 更新阶段持续时间
            currentStageDuration += 150; // 每个NeedInterval是150ticks
            
            // 检查是否拥有火焰活动 Hediff
            if (HasFireActivityHediff && Extension.hediffNeedGainPerSecond > 0)
            {
                // 拥有 Hediff，增加 Need
                float gain = Extension.hediffNeedGainPerSecond * 2.5f; // 每150ticks
                CurLevel = Mathf.Min(1f, CurLevel + gain);
            }
            else
            {
                // 没有 Hediff，自然下降
                float fallRate = Extension.baseFallRatePerDay / 60000f * 150f; // 转换为每150ticks的下降量
                
                // 如果在低阶段停留时间较长，下降更快
                if (CurrentStage == BlackDragonStage.Stage1 || CurrentStage == BlackDragonStage.Stage2)
                {
                    float multiplier = 1.0f + (currentStageDuration / 60000f) * Extension.stageDurationMultiplier;
                    fallRate *= multiplier;
                }
                
                // 应用下降
                CurLevel = Mathf.Max(0f, CurLevel + fallRate);
            }
            
            // 更新当前阶段
            UpdateCurrentStage();
            
            // 更新 Thought
            UpdateThought();
            
            // 检查是否需要点火
            CheckForFireStarting();
        }
        
        /// <summary>
        /// 更新当前阶段
        /// </summary>
        private void UpdateCurrentStage()
        {
            if (Extension == null)
                return;
                
            BlackDragonStage newStage = Extension.GetStage(CurLevelPercentage);
            if (newStage != CurrentStage)
            {
                CurrentStage = newStage;
            }
        }
        
        /// <summary>
        /// 更新 Thought
        /// </summary>
        private void UpdateThought()
        {
            if (Extension == null || pawn == null || pawn.needs == null || pawn.needs.mood == null)
                return;

            MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
            ThoughtDef desiredThoughtDef = Extension.GetThoughtForStage(CurrentStage);

            for (int i = memories.Memories.Count - 1; i >= 0; i--)
            {
                Thought_Memory memory = memories.Memories[i];
                if (IsStageThought(memory.def) && memory.def != desiredThoughtDef)
                {
                    memories.RemoveMemory(memory);
                }
            }

            currentThought = desiredThoughtDef == null
                ? null
                : memories.GetFirstMemoryOfDef(desiredThoughtDef);

            if (desiredThoughtDef != null && currentThought == null)
            {
                Thought_Memory newThought = ThoughtMaker.MakeThought(desiredThoughtDef) as Thought_Memory;
                if (newThought != null)
                {
                    memories.TryGainMemory(newThought);
                    currentThought = memories.GetFirstMemoryOfDef(desiredThoughtDef);
                }
            }
        }

        private bool IsStageThought(ThoughtDef thoughtDef)
        {
            return thoughtDef != null &&
                   (thoughtDef == Extension.stage1Thought ||
                    thoughtDef == Extension.stage2Thought ||
                    thoughtDef == Extension.stage3Thought ||
                    thoughtDef == Extension.stage4Thought);
        }
        
        /// <summary>
        /// 获取当前阶段的 ThoughtDef
        /// </summary>
        public ThoughtDef GetCurrentThoughtDef()
        {
            if (Extension == null)
                return null;
                
            return Extension.GetThoughtForStage(CurrentStage);
        }
        
        /// <summary>
        /// 检查是否需要点火
        /// </summary>
        private void CheckForFireStarting()
        {
            // 如果不在应该尝试点火的状态，重置状态
            if (!ShouldAttemptFireStart)
            {
                ResetSearchState();
                return;
            }
            
            // 如果正在执行点火任务，不需要再寻找
            if (IsExecutingFireStartJob)
                return;
            
            // 检查寻找目标冷却
            int currentTick = Find.TickManager.TicksGame;
            if (lastSearchTick >= 0 && currentTick - lastSearchTick < SearchCooldownTicks)
                return;
            
            // 更新最后寻找时间
            lastSearchTick = currentTick;
            
            // 尝试寻找点火目标并创建任务
            TryFindAndStartFireJob();
        }
        
        /// <summary>
        /// 尝试寻找点火目标并开始任务
        /// </summary>
        private void TryFindAndStartFireJob()
        {
            try
            {
                isSearchingForTarget = true;
                searchRetryCount++;
                
                // 获取配置
                var extension = Extension;
                if (extension == null)
                    return;
                
                // 寻找点火目标
                Thing target = FindFireStartTarget(pawn, extension);
                
                if (target != null)
                {
                    // 找到目标，创建点火任务
                    StartFireStartJob(target);
                    ResetSearchState();
                }
                else
                {
                    // 没有找到目标，记录并可能重试
                    if (searchRetryCount >= 5) // 最多重试5次
                    {
                        ResetSearchState();
                    }
                }
            }
            catch (Exception ex)
            {
                ResetSearchState();
                Log.Error($"[BlackDragon] 寻找点火目标时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 寻找点火目标
        /// </summary>
        private Thing FindFireStartTarget(Pawn pawn, NeedExtension_BlackDragon extension)
        {
            // 尝试在pawn所在区域寻找
            Region region = pawn.GetRegion();
            if (region == null)
                return null;
            
            // 获取所有潜在目标
            var potentialTargets = new List<Thing>();
            List<Thing> allThings = region.ListerThings.AllThings;
            
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                
                // 检查是否是可点火目标
                if (!IsValidFireTarget(thing, pawn))
                    continue;
                
                // 检查是否在家园区域内（如果配置要求在家园区域外）
                if (extension.onlyOutsideHomeArea && IsInHomeArea(thing, pawn.Map))
                    continue;
                
                // 检查距离（不要太远）
                if ((thing.Position - pawn.Position).LengthHorizontalSquared > 100) // 10格距离
                    continue;
                
                potentialTargets.Add(thing);
            }
            
            if (potentialTargets.Count == 0)
            {
                // 如果没有找到，尝试在其他区域寻找
                return FindTargetInOtherRegions(pawn, extension);
            }
            
            return potentialTargets.RandomElement();
        }
        
        /// <summary>
        /// 在其他区域寻找目标
        /// </summary>
        private Thing FindTargetInOtherRegions(Pawn pawn, NeedExtension_BlackDragon extension)
        {
            var potentialTargets = new List<Thing>();
            
            // 获取pawn可以到达的区域
            TraverseParms traverseParms = TraverseParms.For(pawn);
            RegionTraverser.BreadthFirstTraverse(pawn.GetRegion(), 
                (Region from, Region to) => to.Allows(traverseParms, false),
                delegate(Region region)
                {
                    List<Thing> things = region.ListerThings.AllThings;
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing thing = things[i];
                        
                        if (!IsValidFireTarget(thing, pawn))
                            continue;
                        
                        if (extension.onlyOutsideHomeArea && IsInHomeArea(thing, pawn.Map))
                            continue;
                        
                        // 添加到潜在目标
                        if (!potentialTargets.Contains(thing))
                            potentialTargets.Add(thing);
                    }
                    
                    return false; // 继续遍历
                }, 
                30); // 最多遍历30个区域
            
            if (potentialTargets.Count == 0)
                return null;
            
            // 选择最近的目标
            potentialTargets.SortBy(t => (t.Position - pawn.Position).LengthHorizontalSquared);
            return potentialTargets[0];
        }
        
        /// <summary>
        /// 检查是否是有效的点火目标
        /// </summary>
        private bool IsValidFireTarget(Thing thing, Pawn pawn)
        {
            // 基本检查
            if (thing == null || thing.Destroyed)
                return false;
            
            // 检查类别
            if (thing.def.category != ThingCategory.Building && 
                thing.def.category != ThingCategory.Item && 
                thing.def.category != ThingCategory.Plant)
                return false;
            
            // 检查是否可点燃
            if (!thing.FlammableNow)
                return false;
            
            // 检查是否已经在燃烧
            if (thing.IsBurning())
                return false;
            
            // 检查是否在pawn的位置（避免点燃自己）
            if (thing.OccupiedRect().Contains(pawn.Position))
                return false;
            
            // 检查是否可以到达
            if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// 检查是否在家园区域内
        /// </summary>
        private bool IsInHomeArea(Thing thing, Map map)
        {
            if (map == null || map.areaManager == null)
                return false;
            
            Area_Home homeArea = map.areaManager.Home;
            if (homeArea == null)
                return false;
            
            return homeArea[thing.Position];
        }
        
        /// <summary>
        /// 开始点火任务
        /// </summary>
        private void StartFireStartJob(Thing target)
        {
            if (target == null)
                return;
            
            // 创建自定义点火任务
            Job job = JobMaker.MakeJob(LBD_DefOf.LBD_Job_RandomStartFire, target);
            job.expiryInterval = 600; // 10秒超时
            job.checkOverrideOnExpire = true;
            
            // 强制Pawn开始任务
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, null, false, false);
        }
        
        /// <summary>
        /// 重置寻找状态
        /// </summary>
        private void ResetSearchState()
        {
            isSearchingForTarget = false;
            searchRetryCount = 0;
            lastSearchTick = -1;
        }
        
        // === 点火相关方法 ===
        
        /// <summary>
        /// 报告点火成功
        /// </summary>
        public void ReportFireStarted()
        {
            if (Extension == null)
                return;
            
            // 记录点火时间
            lastFireStartTick = Find.TickManager.TicksGame;
            
            // 增加 Need 值
            float gain = Extension.GetFireStartingGain(CurrentStage);
            CurLevel = Mathf.Min(1f, CurLevel + gain);
            
            // 重置标志
            ResetSearchState();
            
            // 如果处于紧急状态，获得更多
            if (Extension.emergencyFireStarting && CurLevelPercentage <= Extension.emergencyThreshold)
            {
                CurLevel = Mathf.Min(1f, CurLevel + Extension.emergencyFireGain);
            }
            
            // 更新阶段和 Thought
            UpdateCurrentStage();
            UpdateThought();
        }
        
        /// <summary>
        /// 报告点火失败
        /// </summary>
        public void ReportFireStartFailed()
        {
            ResetSearchState();
        }
    }
}
