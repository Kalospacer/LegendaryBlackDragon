using RimWorld;
using Verse;
using System.Linq;

namespace LegendaryBlackDragon
{
    public class Verb_CastAbilityTrackingCharge : Verb_CastAbility
    {
        protected override bool TryCastShot()
        {
            var props = this.ability.def.comps?.OfType<CompProperties_TrackingCharge>().FirstOrDefault();
            if (props == null)
            {
                return false;
            }

            if (props.flyerDef == null)
            {
                return false;
            }

            // --- Best Practice: Cache Map and Position FIRST ---
            // Per MCP analysis, Caster.Map is the most reliable source.
            // Cache this before ANY other logic.
            Map map = this.Caster.Map;
            if (map == null)
            {
                return false;
            }
            
            if (this.CasterPawn == null || !this.CasterPawn.Spawned)
            {
                return false;
            }

            // --- This is now a fully custom Thing, so we spawn it directly ---
            var trackingCharge = (PawnFlyer_TrackingCharge)ThingMaker.MakeThing(props.flyerDef);
            
            // Inject properties
            trackingCharge.homingSpeed = props.homingSpeed;
            trackingCharge.initialDamage = props.initialDamage;
            trackingCharge.damagePerTile = props.damagePerTile;
            trackingCharge.inertiaDistance = props.inertiaDistance;
            trackingCharge.collisionDamageDef = props.collisionDamageDef;
            trackingCharge.primaryTarget = this.currentTarget;
            trackingCharge.collisionRadius = props.collisionRadius;
            trackingCharge.impactSound = props.impactSound;
            trackingCharge.damageHostileOnly = props.damageHostileOnly;

            // Setup and spawn
            trackingCharge.StartFlight(this.CasterPawn, this.currentTarget.Cell);
            GenSpawn.Spawn(trackingCharge, this.CasterPawn.Position, map); // Use the cached map

            // --- FIX for Comp Effects ---
            // --- The Standard Pattern to trigger Comps like EffecterOnCaster ---
            // After our custom verb logic (spawning the flyer) is done,
            // we call the ability's Activate method with invalid targets.
            // This triggers the standard Comp cycle without re-casting the verb.
            this.ability.Activate(LocalTargetInfo.Invalid, LocalTargetInfo.Invalid);

            return true;
        }
    }
}