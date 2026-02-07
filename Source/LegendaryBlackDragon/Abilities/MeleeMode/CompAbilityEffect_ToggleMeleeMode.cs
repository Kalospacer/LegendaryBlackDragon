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

        private static void ShowModeMessage(Pawn pawn, bool isScratchMode)
        {
            string key = isScratchMode ? "LBD_MeleeMode_Scratch" : "LBD_MeleeMode_Slam";
            Messages.Message(key.Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
        }
    }
}
