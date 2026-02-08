using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_AbilityDragonSkyNuke : CompAbilityEffect
    {
        public new CompProperties_AbilityDragonSkyNuke Props => (CompProperties_AbilityDragonSkyNuke)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            ThingDef controllerDef = Props.strikeControllerDef ?? LBD_DefOf.LBD_DragonSkyNukeStrikeController;
            if (controllerDef == null)
            {
                Log.Error("[LBD] DragonSkyNuke: strike controller def is null.");
                return;
            }

            foreach (Thing thing in caster.Map.listerThings.ThingsOfDef(controllerDef))
            {
                if (thing is LBD_DragonSkyNukeStrikeController existing && existing.Caster == caster && !existing.Finished)
                {
                    return;
                }
            }

            Thing madeThing = ThingMaker.MakeThing(controllerDef);
            if (!(madeThing is LBD_DragonSkyNukeStrikeController controller))
            {
                Log.Error("[LBD] DragonSkyNuke: wrong controller thing class.");
                return;
            }

            GenSpawn.Spawn(controller, caster.Position, caster.Map);
            controller.Initialize(caster, Props);
        }
    }
}
