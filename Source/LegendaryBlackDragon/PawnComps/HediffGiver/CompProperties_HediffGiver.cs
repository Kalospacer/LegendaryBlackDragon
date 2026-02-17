using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LegendaryBlackDragon
{
    public enum BodyPartSelectionMode
    {
        First,      // 第一个匹配的部位
        Random,     // 随机选择一个匹配的部位
        All,        // 所有匹配的部位
        MostDamaged, // 受伤最严重的部位
        LeastDamaged // 受伤最轻的部位
    }
    public class CompProperties_HediffGiver : CompProperties
    {
        // 要添加的hediff列表
        public List<HediffDef> hediffs;
        
        // 添加hediff的概率（0-1之间）
        public float addChance = 1.0f;
        
        // 是否允许重复添加相同的hediff
        public bool allowDuplicates = false;
        
        // 只在玩家家园地图应用
        public bool onlyApplyInPlayerHome = true;
        
        // 身体部位设置
        public BodyPartDef bodyPart = null;
        public bool skipIfPartMissing = false;
        public BodyPartSelectionMode partSelectionMode = BodyPartSelectionMode.First;
        public int maxParts = 1;
        public bool symmetrical = false;
        
        // Hediff升级相关
        public bool canUpgrade = false;
        public bool requiresExistingHediff = false;
        public float minSeverity = 0f;
        public float maxSeverity = 1f;
        
        // 严重度设置
        public float initialSeverity = -1f;
        
        // 是否检查部位有效性
        public bool checkPartValidity = true;
        
        // 效果和声音
        public EffecterDef applicationEffect = null;
        public bool showApplicationMessage = false;
        public string applicationMessageKey = null;
        public SoundDef applicationSound = null;
        
        // 调试选项
        public bool debugLogging = false;

        public CompProperties_HediffGiver()
        {
            this.compClass = typeof(CompHediffGiver);
        }
    }
}
