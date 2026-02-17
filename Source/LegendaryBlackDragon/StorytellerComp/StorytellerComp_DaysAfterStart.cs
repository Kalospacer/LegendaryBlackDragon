using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace LegendaryBlackDragon
{
    public class StorytellerComp_DaysAfterStart : StorytellerComp
    {
        #region 字段
        private StorytellerCompProperties_DaysAfterStart Props => (StorytellerCompProperties_DaysAfterStart)props;
        
        // 缓存主地图，避免重复查找
        private Map cachedMainMap;
        private int cachedMainMapTick = -99999;
        #endregion

        #region 主要方法
        /// <summary>
        /// 故事讲述者组件的主要入口点 - 每过一段时间调用
        /// </summary>
        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            // 检查是否满足条件（只在主地图触发）
            if (!CheckCondition(target))
                yield break;
            
            // 生成事件
            FiringIncident incident = CreateIncident(target);
            if (incident != null)
            {
                yield return incident;
            }
        }
        
        /// <summary>
        /// 检查触发条件 - 只在主地图触发一次
        /// </summary>
        private bool CheckCondition(IIncidentTarget target)
        {
            try
            {
                // 确保游戏已经开始
                if (Current.Game == null || Find.TickManager == null)
                    return false;

                // 重要：只在地图目标上触发，并且只针对主地图
                Map targetMap = target as Map;
                if (targetMap == null)
                    return false;
                
                // 只处理玩家家园地图
                if (!targetMap.IsPlayerHome)
                    return false;
                
                // 获取主地图（第一个玩家家园地图）
                Map mainMap = GetMainMap();
                if (mainMap == null)
                    return false;
                
                // 如果不是主地图，则不触发
                if (targetMap != mainMap)
                {
                    return false;
                }
                
                // 检查是否已经触发过
                if (target.StoryState.lastFireTicks.TryGetValue(Props.incident, out int lastTick))
                {
                    if (!Props.repeatable)
                    {
                        if (Props.debugLogging)
                        {
                            Log.Message($"[DaysAfterStart] 事件 {Props.incident.defName} 不可重复，已触发过");
                        }
                        return false;
                    }

                    // 检查重复间隔
                    int ticksSinceLastFire = Find.TickManager.TicksGame - lastTick;
                    int requiredTicks = Props.repeatIntervalDays * 60000; // 60000 ticks = 1天
                    
                    if (ticksSinceLastFire < requiredTicks)
                    {
                        if (Props.debugLogging)
                        {
                            Log.Message($"[DaysAfterStart] 事件 {Props.incident.defName} 还在冷却中，距离上次触发: {ticksSinceLastFire.ToStringTicksToDays()} 天，需要间隔: {Props.repeatIntervalDays} 天");
                        }
                        return false;
                    }
                }
                
                // 计算已经过去的天数
                float daysPassed = GenDate.DaysPassedFloat;
                
                // 检查是否达到触发天数
                bool shouldFire = daysPassed >= Props.daysAfterStart;
                
                if (Props.debugLogging && !shouldFire)
                {
                    Log.Message($"[DaysAfterStart] 未达到触发天数，还需等待: {Props.daysAfterStart - daysPassed:F1} 天");
                }
                
                return shouldFire;
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStart] 检查条件时出错: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取主地图（第一个玩家家园地图）
        /// </summary>
        private Map GetMainMap()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // 每60秒刷新一次缓存
            if (currentTick - cachedMainMapTick > 3600 || cachedMainMap == null)
            {
                cachedMainMap = Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                cachedMainMapTick = currentTick;
            }
            
            return cachedMainMap;
        }
        
        /// <summary>
        /// 创建要触发的事件
        /// </summary>
        private FiringIncident CreateIncident(IIncidentTarget target)
        {
            try
            {
                // 确保有有效的事件
                if (Props.incident == null)
                {
                    Log.Error("[DaysAfterStart] IncidentDef 为 null");
                    return null;
                }
                
                // 获取目标地图
                Map map = target as Map;
                if (map == null)
                {
                    // 尝试获取主地图
                    map = GetMainMap();
                    if (map == null)
                    {
                        if (Props.debugLogging)
                            Log.Warning("[DaysAfterStart] 没有找到主地图");
                        return null;
                    }
                }
                
                // 生成事件参数
                IncidentParms parms = GenerateParms(Props.incident.category, target);
                
                // 检查事件是否可以触发
                if (!Props.incident.Worker.CanFireNow(parms))
                {
                    if (Props.debugLogging)
                    {
                        Log.Warning($"[DaysAfterStart] 事件 {Props.incident.defName} 当前无法触发");
                    }
                    return null;
                }
                
                // 创建并返回事件
                FiringIncident firingIncident = new FiringIncident(Props.incident, this, parms);
                
                if (Props.debugLogging)
                {
                    Log.Message($"[DaysAfterStart] 创建事件: {Props.incident.defName}, " +
                               $"目标: {target}, " +
                               $"已过天数: {GenDate.DaysPassedFloat:F1}");
                }
                
                return firingIncident;
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStart] 创建事件时出错: {ex}");
                return null;
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 生成事件参数
        /// </summary>
        public override IncidentParms GenerateParms(IncidentCategoryDef category, IIncidentTarget target)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(category, target);
            
            // 可以根据需要调整参数
            // 例如：parms.forced = true; // 强制触发
            
            return parms;
        }
        
        /// <summary>
        /// 获取状态信息（调试用）
        /// </summary>
        public string GetStatus(IIncidentTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== DaysAfterStart StorytellerComp Status ===");
            sb.AppendLine($"Target: {target}");
            sb.AppendLine($"Incident: {Props.incident?.defName ?? "NULL"}");
            sb.AppendLine($"Days after start: {Props.daysAfterStart}");
            sb.AppendLine($"Current days passed: {GenDate.DaysPassedFloat:F1}");
            
            // 主地图信息
            Map mainMap = GetMainMap();
            
            bool fired = target.StoryState.lastFireTicks.TryGetValue(Props.incident, out int lastTick);
            sb.AppendLine($"Has fired: {fired}");
            if (fired)
            {
                sb.AppendLine($"Last fire tick: {lastTick} ({(Find.TickManager.TicksGame - lastTick).ToStringTicksToDays()} days ago)");
            }
            
            sb.AppendLine($"Repeatable: {Props.repeatable}");
            if (Props.repeatable)
            {
                sb.AppendLine($"Repeat interval days: {Props.repeatIntervalDays}");
            }
            
            // 检查条件
            bool canTrigger = CheckCondition(target);
            sb.AppendLine($"Can trigger now: {canTrigger}");
            
            return sb.ToString();
        }
        #endregion

        #region 调试方法
        /// <summary>
        /// 强制触发事件（调试用）
        /// </summary>
        public void ForceTrigger(IIncidentTarget target)
        {
            // 创建并直接触发事件
            FiringIncident incident = CreateIncident(target);
            if (incident != null)
            {
                if (Props.incident.Worker.TryExecute(incident.parms))
                {
                    target.StoryState.Notify_IncidentFired(incident);
                    Log.Message($"[DaysAfterStart] 成功强制触发事件: {Props.incident.defName}");
                }
                else
                {
                    Log.Error($"[DaysAfterStart] 强制触发事件失败: {Props.incident.defName}");
                }
            }
        }
        #endregion
    }
}
