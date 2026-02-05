using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Text;

namespace LegendaryBlackDragon
{
    public class StorytellerComp_DaysAfterStart : StorytellerComp
    {
        #region 字段
        private StorytellerCompProperties_DaysAfterStart Props => (StorytellerCompProperties_DaysAfterStart)props;
        
        // 状态跟踪
        private bool hasTriggered = false;
        private int lastCheckTick = -1;
        
        // 检查间隔（ticks），避免每帧都检查
        private const int CHECK_INTERVAL_TICKS = 6000; // 每6000ticks（游戏内1/10天）检查一次
        #endregion

        #region 主要方法
        /// <summary>
        /// 故事讲述者组件的主要入口点 - 每过一段时间调用
        /// </summary>
        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            // 如果已经触发过，不再检查
            if (hasTriggered)
                yield break;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // 检查间隔，避免每帧都检查
            if (currentTick - lastCheckTick < CHECK_INTERVAL_TICKS)
                yield break;
            
            lastCheckTick = currentTick;
            
            // 检查是否满足天数条件
            if (!CheckDaysCondition())
                yield break;
            
            // 生成事件
            FiringIncident incident = CreateIncident(target);
            if (incident != null)
            {
                hasTriggered = true;
                yield return incident;
            }
        }
        
        /// <summary>
        /// 检查天数条件
        /// </summary>
        private bool CheckDaysCondition()
        {
            try
            {
                // 确保游戏已经开始
                if (Current.Game == null || Find.TickManager == null)
                    return false;
                
                // 确保有玩家派系
                if (Faction.OfPlayer == null)
                    return false;
                
                // 计算已经过去的天数
                int daysPassed = GenDate.DaysPassed;
                
                if (Props.debugLogging)
                {
                    Log.Message($"[DaysAfterStart] 已过天数: {daysPassed}, 触发天数: {Props.daysAfterStart}");
                }
                
                // 检查是否达到触发天数
                return daysPassed >= Props.daysAfterStart;
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStart] 检查天数条件时出错: {ex}");
                return false;
            }
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
                    // 尝试获取任意玩家家园地图
                    map = Find.AnyPlayerHomeMap;
                    if (map == null)
                    {
                        if (Props.debugLogging)
                            Log.Warning("[DaysAfterStart] 没有找到玩家家园地图");
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
                               $"目标地图: {map.Parent.Label}, " +
                               $"已过天数: {GenDate.DaysPassed}");
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

        #region 序列化和状态管理
        /// <summary>
        /// 序列化状态
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref hasTriggered, "hasTriggered", false);
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", -1);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 加载后可以执行一些初始化
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
        public string GetStatus()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== DaysAfterStart StorytellerComp Status ===");
            sb.AppendLine($"Incident: {Props.incident?.defName ?? "NULL"}");
            sb.AppendLine($"Days after start: {Props.daysAfterStart}");
            sb.AppendLine($"Current days passed: {GenDate.DaysPassed}");
            sb.AppendLine($"Has triggered: {hasTriggered}");
            sb.AppendLine($"Last check tick: {lastCheckTick}");
            sb.AppendLine($"Debug logging: {Props.debugLogging}");
            
            // 检查条件
            bool canTrigger = CheckDaysCondition();
            sb.AppendLine($"Can trigger: {canTrigger}");
            
            if (Current.Game != null && Faction.OfPlayer != null)
            {
                sb.AppendLine($"Player faction: {Faction.OfPlayer.Name}");
                sb.AppendLine($"Player maps: {Find.Maps.Count(m => m.IsPlayerHome)}");
            }
            
            return sb.ToString();
        }
        #endregion

        #region 调试方法
        /// <summary>
        /// 强制触发事件（调试用）
        /// </summary>
        public void ForceTrigger()
        {
            if (hasTriggered)
            {
                Log.Warning("[DaysAfterStart] 事件已经触发过");
                return;
            }
            
            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
            {
                Log.Error("[DaysAfterStart] 没有找到玩家家园地图，无法强制触发");
                return;
            }
            
            // 创建并直接触发事件
            FiringIncident incident = CreateIncident(map);
            if (incident != null)
            {
                hasTriggered = true;
                incident.parms.target = map;
                
                if (Props.incident.Worker.TryExecute(incident.parms))
                {
                    Log.Message($"[DaysAfterStart] 成功强制触发事件: {Props.incident.defName}");
                }
                else
                {
                    Log.Error($"[DaysAfterStart] 强制触发事件失败: {Props.incident.defName}");
                }
            }
        }
        
        /// <summary>
        /// 重置触发状态（调试用）
        /// </summary>
        public void Reset()
        {
            hasTriggered = false;
            lastCheckTick = -1;
            Log.Message("[DaysAfterStart] 触发状态已重置");
        }
        #endregion
    }
}
