using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
    public class JobDriver_CastAbilityMaintainFlame : JobDriver_CastAbility
    {
        private CompAbilityEffect_SustainedFlameCone FlameComp
        {
            get
            {
                if (job?.ability?.EffectComps == null)
                {
                    return null;
                }

                return job.ability.EffectComps.OfType<CompAbilityEffect_SustainedFlameCone>().FirstOrDefault();
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job.ability == null || (!job.ability.CanCast && !job.ability.Casting));
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                {
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                }
            });

            Toil stopToil = ToilMaker.MakeToil("StopBeforeFlameCast");
            stopToil.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            stopToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return stopToil;

            Toil castToil = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            if (job.ability != null && job.ability.def.showCastingProgressBar && job.verbToUse != null)
            {
                castToil.WithProgressBar(TargetIndex.A, () => job.verbToUse.WarmupProgress);
            }
            yield return castToil;

            Toil maintainToil = ToilMaker.MakeToil("MaintainFlameCone");
            maintainToil.initAction = delegate
            {
                pawn.pather.StopDead();
                rotateToFace = TargetIndex.A;
            };
            maintainToil.tickAction = delegate
            {
                pawn.pather.StopDead();

                CompAbilityEffect_SustainedFlameCone flameComp = FlameComp;
                if (flameComp?.TargetCell.IsValid == true)
                {
                    job.targetA = flameComp.TargetCell;
                }

                if (flameComp == null || !flameComp.IsActive)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            maintainToil.PlaySustainerOrSound(LBD_DefOf.LBD_Dragon_Fire_Ability_Maintain);
            maintainToil.FailOn(() => pawn.Dead || pawn.Downed || !pawn.Spawned);
            maintainToil.handlingFacing = true;
            maintainToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return maintainToil;
        }

        public override string GetReport()
        {
            if (job?.ability != null)
            {
                return "UsingVerbNoTarget".Translate(job.verbToUse.ReportLabel);
            }

            return base.GetReport();
        }
    }
}
