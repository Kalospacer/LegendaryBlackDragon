using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_AbilityToggleMeleeMode : CompProperties_AbilityEffect
    {
        // 添加两个标签字段，用于定义本地化名称
        public string scratchModeLabelKey = "LBD_MeleeMode_Scratch";  // Scratch模式标签键
        public string slamModeLabelKey = "LBD_MeleeMode_Slam";        // Slam模式标签键
        
        // 可选：添加自定义Gizmo标签
        public string gizmoExtraLabelKey = "LBD_CurrentMode";

        public CompProperties_AbilityToggleMeleeMode()
        {
            compClass = typeof(CompAbilityEffect_ToggleMeleeMode);
        }
    }
}
