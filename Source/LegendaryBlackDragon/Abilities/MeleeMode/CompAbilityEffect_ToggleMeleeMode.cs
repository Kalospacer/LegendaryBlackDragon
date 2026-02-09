using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_ToggleMeleeMode : CompAbilityEffect
    {
        public new CompProperties_AbilityToggleMeleeMode Props => (CompProperties_AbilityToggleMeleeMode)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn == null)
            {
                return;
            }

            CompMeleeModeState modeState = pawn.TryGetComp<CompMeleeModeState>();
            if (modeState == null)
            {
                return;
            }

            bool toScratch = !modeState.ScratchMode;
            modeState.ScratchMode = toScratch;
            ShowModeMessage(pawn, toScratch);
        }

        private void ShowModeMessage(Pawn pawn, bool isScratchMode)
        {
            // 使用Props中定义的标签键，如果未定义则使用默认键
            string key = isScratchMode ? 
                (Props.scratchModeLabelKey ?? "LBD_MeleeMode_Scratch") : 
                (Props.slamModeLabelKey ?? "LBD_MeleeMode_Slam");
            
            Messages.Message(key.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
        }

        // 重写Gizmo的额外标签描述
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn == null)
            {
                return null;
            }

            CompMeleeModeState modeState = pawn.TryGetComp<CompMeleeModeState>();
            if (modeState == null)
            {
                return null;
            }

            // 获取当前模式对应的标签键
            string currentModeKey = modeState.ScratchMode ? 
                (Props.scratchModeLabelKey ?? "LBD_MeleeMode_Scratch") : 
                (Props.slamModeLabelKey ?? "LBD_MeleeMode_Slam");
            
            string currentModeLabel = currentModeKey.Translate();

            // 使用自定义Gizmo标签键，如果未定义则使用默认
            string gizmoKey = Props.gizmoExtraLabelKey ?? "LBD_CurrentMode";
            
            return gizmoKey.Translate(currentModeLabel);
        }

        // 可选：也可以在Gizmo的主标签中显示当前状态
        public override string ExtraTooltipPart()
        {
            Pawn pawn = parent.pawn;
            if (pawn == null)
            {
                return null;
            }

            CompMeleeModeState modeState = pawn.TryGetComp<CompMeleeModeState>();
            if (modeState == null)
            {
                return null;
            }

            string currentModeKey = modeState.ScratchMode ? 
                (Props.scratchModeLabelKey ?? "LBD_MeleeMode_Scratch") : 
                (Props.slamModeLabelKey ?? "LBD_MeleeMode_Slam");
            
            string currentModeLabel = currentModeKey.Translate();

            return "LBD_CurrentModeTooltip".Translate(currentModeLabel);
        }
    }
}
