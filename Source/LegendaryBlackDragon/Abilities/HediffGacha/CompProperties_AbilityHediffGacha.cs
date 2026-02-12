using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// Hediff池条目 - 包含Hediff定义和权重
    /// </summary>
    public class HediffPoolEntry
    {
        /// <summary>
        /// Hediff定义
        /// </summary>
        public HediffDef hediff;
        
        /// <summary>
        /// 抽取权重（仅在useWeights为true时生效）
        /// </summary>
        public float weight = 1f;
        
        /// <summary>
        /// 可选的描述覆盖（用于在选择界面显示自定义描述）
        /// </summary>
        public string descriptionOverride;
        
        /// <summary>
        /// 自定义图标路径（可选的，如果不设置则使用Hediff默认图标）
        /// </summary>
        public string iconPath;
        
        /// <summary>
        /// 自定义图标颜色（可选的，如果不设置则使用默认白色）
        /// </summary>
        public Color? iconColor;
        
        /// <summary>
        /// 图标缩放（默认1.0）
        /// </summary>
        public float iconScale = 1.0f;
        
        /// <summary>
        /// 图标是否带有背景（默认true）
        /// </summary>
        public bool iconHasBackground = true;
        
        /// <summary>
        /// 图标背景颜色（可选的）
        /// </summary>
        public Color? iconBackgroundColor;
    }
    
    /// <summary>
    /// Hediff抽卡技能属性
    /// </summary>
    public class CompProperties_AbilityHediffGacha : CompProperties_AbilityEffect
    {
        /// <summary>
        /// Hediff池 - 可供抽取的Hediff列表
        /// </summary>
        public List<HediffPoolEntry> hediffPool;
        
        /// <summary>
        /// 每次抽取的选项数量（默认3个，即三选一）
        /// </summary>
        public int choiceCount = 3;
        
        /// <summary>
        /// 是否使用权重进行随机抽取
        /// </summary>
        public bool useWeights = true;
        
        /// <summary>
        /// 是否允许抽取重复的Hediff
        /// </summary>
        public bool allowDuplicates = false;
        
        /// <summary>
        /// 窗口标题翻译键
        /// </summary>
        public string windowTitle = "DD_HediffGacha_Title";
        
        /// <summary>
        /// 是否允许取消选择
        /// </summary>
        public bool allowCancel = false;
        
        /// <summary>
        /// 选择后的特效
        /// </summary>
        public FleckDef selectionFleck;
        
        /// <summary>
        /// 是否显示选择消息
        /// </summary>
        public bool showSelectionMessage = true;
        
        /// <summary>
        /// 选择消息的翻译键（可选，默认使用DD_HediffGacha_Selected）
        /// </summary>
        public string selectionMessageKey;
        
        /// <summary>
        /// Hediff初始严重度（负数表示使用默认值）
        /// </summary>
        public float initialSeverity = -1f;
        
        /// <summary>
        /// Hediff持续时间（秒，0表示永久）
        /// </summary>
        public float durationSeconds = 0f;
        
        /// <summary>
        /// 是否只应用到大脑
        /// </summary>
        public bool onlyBrain = false;
        
        /// <summary>
        /// 是否替换已存在的相同Hediff
        /// </summary>
        public bool replaceExisting = true;
        
        /// <summary>
        /// 卡片默认背景颜色（用于没有自定义图标背景的卡片）
        /// </summary>
        public Color cardDefaultBackground = new Color(0.15f, 0.15f, 0.18f);
        
        /// <summary>
        /// 卡片悬停背景颜色
        /// </summary>
        public Color cardHoverBackground = new Color(0.25f, 0.25f, 0.3f);
        
        /// <summary>
        /// 卡片默认边框颜色
        /// </summary>
        public Color cardDefaultBorder = new Color(0.4f, 0.4f, 0.4f);
        
        /// <summary>
        /// 卡片悬停边框颜色
        /// </summary>
        public Color cardHoverBorder = new Color(0.8f, 0.7f, 0.4f);
        
        public CompProperties_AbilityHediffGacha()
        {
            compClass = typeof(CompAbilityEffect_HediffGacha);
        }
    }
}
