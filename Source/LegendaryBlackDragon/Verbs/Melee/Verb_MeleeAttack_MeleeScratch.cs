using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class Verb_MeleeAttack_MeleeScratch : Verb_MeleeAttack
    {
        private const int DefaultScratchKnockbackCells = 2;
        private const int DefaultScratchStunTicks = 45;

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
            if (caster == null || !target.HasThing)
            {
                return result;
            }

            Thing mainTarget = target.Thing;
            result = mainTarget.TakeDamage(MakeDamageInfo(verb, caster, mainTarget));
            if (mainTarget is Pawn pawn)
            {
                TryKnockbackPawn(verb, caster, pawn);
            }

            return result;
        }

        private static DamageInfo MakeDamageInfo(Verb verb, Pawn caster, Thing target)
        {
            float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb, caster);
            float armorPen = verb.verbProps.AdjustedArmorPenetration(verb, caster);
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

        private static void TryKnockbackPawn(Verb verb, Pawn caster, Pawn victim)
        {
            if (caster.MapHeld == null || victim.MapHeld != caster.MapHeld || victim.Dead || !victim.Spawned)
            {
                return;
            }

            VerbProperties_MeleeMode props = verb.verbProps as VerbProperties_MeleeMode;
            int knockbackCells = Mathf.Max(0, props?.scratchKnockbackCells ?? DefaultScratchKnockbackCells);
            int stunTicks = Mathf.Max(0, props?.scratchStunTicks ?? DefaultScratchStunTicks);
            ThingDef flyerDef = props?.scratchKnockbackFlyerDef ?? ThingDefOf.PawnFlyer;
            SoundDef landingSound = props?.scratchLandingSound ?? DefDatabase<SoundDef>.GetNamedSilentFail("PawnFlyer_Land");
            EffecterDef flightEffecter = props?.scratchFlightEffecterDef;

            IntVec3 from = caster.Position;
            IntVec3 victimPos = victim.Position;
            int dx = Mathf.Clamp(victimPos.x - from.x, -1, 1);
            int dz = Mathf.Clamp(victimPos.z - from.z, -1, 1);
            if (dx == 0 && dz == 0)
            {
                return;
            }

            IntVec3 step = new IntVec3(dx, 0, dz);
            IntVec3 destination = victimPos;
            for (int i = 1; i <= knockbackCells; i++)
            {
                IntVec3 cell = victimPos + step * i;
                if (!cell.InBounds(caster.MapHeld))
                {
                    break;
                }

                if (!cell.Standable(caster.MapHeld) || cell.GetFirstPawn(caster.MapHeld) != null)
                {
                    break;
                }

                destination = cell;
            }

            if (destination != victimPos)
            {
                PawnFlyer flyer = PawnFlyer.MakeFlyer(
                    flyerDef,
                    victim,
                    destination,
                    flightEffecter,
                    landingSound,
                    flyWithCarriedThing: false,
                    overrideStartVec: null,
                    triggeringAbility: null,
                    target: new LocalTargetInfo(destination));

                if (flyer != null)
                {
                    GenSpawn.Spawn(flyer, destination, caster.MapHeld);
                }
            }

            if (stunTicks > 0)
            {
                victim.stances?.stunner?.StunFor(stunTicks, caster, addBattleLog: false, showMote: false);
            }
        }
    }
}
