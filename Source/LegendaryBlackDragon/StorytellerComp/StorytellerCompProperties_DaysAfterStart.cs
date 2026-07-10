using RimWorld;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class StorytellerCompProperties_DaysAfterStart : StorytellerCompProperties
    {
        #region 字段
        // 要触发的事件
        public IncidentDef incident;
        
        // 游戏开始后多少天触发（必需）
        public float daysAfterStart = 1f;
        
        // 延迟ticks（可选，事件触发后的延迟）
        public int delayTicks = 0;
        
        // 调试选项
        public bool debugLogging = false;
        
        // 重复触发选项
        public bool repeatable = false;
        public int repeatIntervalDays = 0;
        #endregion

        #region 构造函数
        public StorytellerCompProperties_DaysAfterStart()
        {
            compClass = typeof(StorytellerComp_DaysAfterStart);
        }
        #endregion

        #region 验证方法
        /// <summary>
        /// 验证配置
        /// </summary>
        public override void ResolveReferences(StorytellerDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // 验证事件定义
            if (incident == null)
            {
                Log.Error($"[DaysAfterStart] StorytellerCompProperties_DaysAfterStart: incident 为 null");
            }
            
            // 验证天数
            if (daysAfterStart < 0f)
            {
                Log.Warning($"[DaysAfterStart] StorytellerCompProperties_DaysAfterStart: daysAfterStart 为负数，已设为0");
                daysAfterStart = 0f;
            }
            
            // 验证延迟
            if (delayTicks < 0)
            {
                Log.Warning($"[DaysAfterStart] StorytellerCompProperties_DaysAfterStart: delayTicks 为负数，已设为0");
                delayTicks = 0;
            }
            
            if (debugLogging)
            {
                Log.Message($"[DaysAfterStart] StorytellerCompProperties_DaysAfterStart 配置完成:");
                Log.Message($"  incident: {incident?.defName ?? "NULL"}");
                Log.Message($"  daysAfterStart: {daysAfterStart}");
                Log.Message($"  delayTicks: {delayTicks}");
                Log.Message($"  repeatable: {repeatable}");
                Log.Message($"  repeatIntervalDays: {repeatIntervalDays}");
            }
        }
        #endregion
    }
}
