using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LegendaryBlackDragon
{
    public class Verb_MeleeAttack_MeleeSlam : Verb_MeleeAttack
    {
        private const float DefaultSlamRadius = 1.9f;
        private const float DefaultSlamDamageFactor = 0.65f;

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            return CasterPawn?.equipment?.Primary == null;
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            return Execute(this, target);
        }

        public static DamageWorker.DamageResult Execute(Verb verb, LocalTargetInfo target)
        {
            DamageWorker.DamageResult result = new DamageWorker.DamageResult();
            Pawn caster = verb.CasterPawn;
            if (caster == null || caster.MapHeld == null || !target.HasThing)
            {
                return result;
            }
            VerbProperties_MeleeMode props = verb.verbProps as VerbProperties_MeleeMode;
            float slamRadius = props?.slamRadius ?? DefaultSlamRadius;
            float slamDamageFactor = props?.slamDamageFactor ?? DefaultSlamDamageFactor;

            Thing mainTarget = target.Thing;
            result = mainTarget.TakeDamage(MakeDamageInfo(verb, caster, mainTarget, 1f));

            IEnumerable<Thing> targets = GenRadial.RadialDistinctThingsAround(mainTarget.Position, caster.MapHeld, slamRadius, useCenter: true);
            foreach (Thing thing in targets)
            {
                if (thing == mainTarget || thing == caster || !(thing is Pawn pawn))
                {
                    continue;
                }

                if (!pawn.HostileTo(caster) || pawn.Downed)
                {
                    continue;
                }

                pawn.TakeDamage(MakeDamageInfo(verb, caster, pawn, slamDamageFactor));
            }

            return result;
        }

        private static DamageInfo MakeDamageInfo(Verb verb, Pawn caster, Thing target, float damageFactor)
        {
            float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb, caster) * damageFactor;
            float armorPen = verb.verbProps.AdjustedArmorPenetration(verb, caster) * damageFactor;
            DamageDef damageDef = verb.verbProps.meleeDamageDef ?? DamageDefOf.Blunt;

            DamageInfo dinfo = new DamageInfo(
                damageDef,
                damage,
                armorPen,
                -1f,
                caster,
                null,
                verb.EquipmentSource?.def ?? caster.def);

            dinfo.SetTool(verb.tool);
            dinfo.SetAngle((target.Position - caster.Position).ToVector3());
            return dinfo;
        }
    }
}
