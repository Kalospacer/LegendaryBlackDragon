using RimWorld;
using Verse;
using UnityEngine;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 纵火狂阶段的 Thought
    /// </summary>
    public class Thought_BlackDragon : Thought_Memory
    {
        // 可以添加特定于纵火狂 Thought 的逻辑
        // 例如：根据点火次数增加心情影响
        
        private int fireStartCount = 0;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fireStartCount, "fireStartCount", 0);
        }
        
        /// <summary>
        /// 报告一次点火
        /// </summary>
        public void ReportFireStart()
        {
            fireStartCount++;
        }
        
        /// <summary>
        /// 重写心情影响，可以根据点火次数调整
        /// </summary>
        public override float MoodOffset()
        {
            float baseOffset = base.MoodOffset();
            
            // 如果有点火记录，增加心情（纵火狂喜欢点火）
            if (fireStartCount > 0)
            {
                baseOffset += Mathf.Min(fireStartCount * 0.1f, 5f); // 最多+5心情
            }
            
            return baseOffset;
        }
        
        /// <summary>
        /// 获取描述，显示点火次数
        /// </summary>
        public override string Description
        {
            get
            {
                string baseDesc = base.Description;
                
                if (fireStartCount > 0)
                {
                    baseDesc += $"\n\n{"BlackDragon_FireStartCount".Translate()}: {fireStartCount}";
                }
                
                return baseDesc;
            }
        }
    }
    
    /// <summary>
    /// 纵火狂阶段1的 Thought（渴望火焰）
    /// </summary>
    public class Thought_BlackDragon_Stage1 : Thought_BlackDragon
    {
        // 阶段1的特殊逻辑
        public override float MoodOffset()
        {
            // 阶段1的心情影响较大（负面）
            return base.MoodOffset() * 1.5f;
        }
    }
    
    /// <summary>
    /// 纵火狂阶段2的 Thought（需要火焰）
    /// </summary>
    public class Thought_BlackDragon_Stage2 : Thought_BlackDragon
    {
        // 阶段2的特殊逻辑
        public override float MoodOffset()
        {
            return base.MoodOffset() * 1.2f;
        }
    }
    
    /// <summary>
    /// 纵火狂阶段3的 Thought（满足）
    /// </summary>
    public class Thought_BlackDragon_Stage3 : Thought_BlackDragon
    {
        // 阶段3是中性或轻微正面
        public override float MoodOffset()
        {
            return base.MoodOffset() * 0.5f;
        }
    }
    
    /// <summary>
    /// 纵火狂阶段4的 Thought（非常满足）
    /// </summary>
    public class Thought_BlackDragon_Stage4 : Thought_BlackDragon
    {
        // 阶段4是正面心情
        public override float MoodOffset()
        {
            float offset = base.MoodOffset();
            // 确保是正面的
            return offset > 0 ? offset : Mathf.Abs(offset);
        }
    }
}
