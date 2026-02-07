using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class Verb_MeleeAttack_MeleeMode : Verb_MeleeAttack
    {
        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            Pawn pawn = CasterPawn;
            if (pawn == null)
            {
                return false;
            }

            return pawn.equipment?.Primary == null;
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            if (CasterPawn == null || !target.HasThing)
            {
                return new DamageWorker.DamageResult();
            }

            if (IsScratchMode(CasterPawn))
            {
                return Verb_MeleeAttack_MeleeScratch.Execute(this, target);
            }

            return Verb_MeleeAttack_MeleeSlam.Execute(this, target);
        }

        private bool IsScratchMode(Pawn pawn)
        {
            CompMeleeModeState modeState = pawn.TryGetComp<CompMeleeModeState>();
            return modeState != null && modeState.ScratchMode;
        }
    }
}
