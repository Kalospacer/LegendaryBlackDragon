using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// BlackDragon Need 的 ModExtension，定义4个阶段对应的 Thought
    /// </summary>
    public class NeedExtension_BlackDragon : DefModExtension
    {
        // 四个阶段的 ThoughtDef
        public ThoughtDef stage1Thought;
        public ThoughtDef stage2Thought;
        public ThoughtDef stage3Thought;
        public ThoughtDef stage4Thought;
        
        // 每个阶段对应的 Need 百分比阈值
        public float stage4Threshold = 0.75f;    // 阶段4: 75%-100%
        public float stage3Threshold = 0.50f;    // 阶段3: 50%-75%
        public float stage2Threshold = 0.25f;    // 阶段2: 25%-50%
        public float stage1Threshold = 0f;       // 阶段1: 0%-25%
        
        // Need 恢复速率（每秒）
        public float baseFallRatePerDay = -0.10f;  // 每天自然下降10%
        public float fireStartingGain = 0.15f;     // 每次点火获得的 Need 值
        
        // 点火行为配置
        public bool allowFireStarting = true;
        public float minFireStartingNeed = 0.40f;  // 低于40%时尝试点火
        public bool onlyOutsideHomeArea = true;    // 只在家园区域外点火
        
        // 紧急状态配置
        public bool emergencyFireStarting = true;  // 紧急点火（当Need极低时）
        public float emergencyThreshold = 0.15f;   // 紧急阈值
        public float emergencyFireGain = 0.25f;    // 紧急点火获得更多
        
        // 点火冷却时间（ticks）
        public int fireCooldownTicks = 6000;       // 100秒
        
        // 阶段持续时间乘数（在某个阶段停留时间越长，Need下降越快）
        public float stageDurationMultiplier = 1.0f;
        
        /// <summary>
        /// 根据 Need 百分比获取当前阶段
        /// </summary>
        public BlackDragonStage GetStage(float needPercent)
        {
            if (needPercent >= stage4Threshold)
                return BlackDragonStage.Stage4;
            if (needPercent >= stage3Threshold)
                return BlackDragonStage.Stage3;
            if (needPercent >= stage2Threshold)
                return BlackDragonStage.Stage2;
            return BlackDragonStage.Stage1;
        }
        
        /// <summary>
        /// 获取当前阶段对应的 ThoughtDef
        /// </summary>
        public ThoughtDef GetThoughtForStage(BlackDragonStage stage)
        {
            switch (stage)
            {
                case BlackDragonStage.Stage1: return stage1Thought;
                case BlackDragonStage.Stage2: return stage2Thought;
                case BlackDragonStage.Stage3: return stage3Thought;
                case BlackDragonStage.Stage4: return stage4Thought;
                default: return null;
            }
        }
        
        /// <summary>
        /// 获取点火获得的 Need 值
        /// </summary>
        public float GetFireStartingGain(BlackDragonStage currentStage)
        {
            float gain = fireStartingGain;
            
            // 根据阶段调整增益
            switch (currentStage)
            {
                case BlackDragonStage.Stage1:
                    gain *= 1.5f;  // 阶段1获得更多
                    break;
                case BlackDragonStage.Stage2:
                    gain *= 1.2f;  // 阶段2获得稍多
                    break;
                case BlackDragonStage.Stage4:
                    gain *= 0.5f;  // 阶段4获得较少（已经满足）
                    break;
            }
            
            return gain;
        }
    }
    
    /// <summary>
    /// 纵火狂阶段枚举
    /// </summary>
    public enum BlackDragonStage
    {
        Stage1,  // 最低阶段（0-25%）
        Stage2,  // 低阶段（25-50%）
        Stage3,  // 中阶段（50-75%）
        Stage4   // 高阶段（75-100%）
    }
}
