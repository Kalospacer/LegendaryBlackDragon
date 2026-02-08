using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 持续引导型火焰光环的 JobDriver，施法期间锁定不动
    /// </summary>
    public class JobDriver_CastAbilityMaintainFlameAura : JobDriver_CastAbility
    {
        private CompAbilityEffect_SustainedFlameAura FlameComp
        {
            get
            {
                return job?.ability?.EffectComps?
                    .OfType<CompAbilityEffect_SustainedFlameAura>()
                    .FirstOrDefault();
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

                if (pawn?.flight != null && pawn.flight.Flying)
                {
                    pawn.flight.ForceLand();
                }
            });

            // 停步
            Toil stopToil = ToilMaker.MakeToil("StopBeforeFlameAura");
            stopToil.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            stopToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return stopToil;

            // 施法（warmup）
            Toil castToil = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            if (job.ability != null && job.ability.def.showCastingProgressBar && job.verbToUse != null)
            {
                castToil.WithProgressBar(TargetIndex.A, () => job.verbToUse.WarmupProgress);
            }
            yield return castToil;

            // 持续引导阶段
            Toil maintainToil = ToilMaker.MakeToil("MaintainFlameAura");
            maintainToil.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            maintainToil.tickAction = delegate
            {
                pawn.pather.StopDead();

                var comp = FlameComp;
                if (comp == null || !comp.IsActive)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            maintainToil.PlaySustainerOrSound(LBD_DefOf.LBD_Dragon_Fire_Ability_Maintain);
            maintainToil.FailOn(() => pawn.Dead || pawn.Downed || !pawn.Spawned);
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
