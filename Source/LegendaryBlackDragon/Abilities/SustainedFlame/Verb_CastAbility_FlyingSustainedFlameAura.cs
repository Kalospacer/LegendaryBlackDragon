using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class Verb_CastAbility_FlyingSustainedFlameAura : Verb_CastAbility
    {
        protected override bool TryCastShot()
        {
            bool casted = base.TryCastShot();
            if (!casted)
            {
                return false;
            }

            Pawn casterPawn = CasterPawn;
            if (casterPawn == null || casterPawn.Dead || !casterPawn.Spawned)
            {
                return true;
            }

            Pawn_FlightTracker flight = casterPawn.flight;
            if (flight == null || !flight.CanEverFly)
            {
                return true;
            }

            if (!flight.Flying && flight.CanFlyNow)
            {
                flight.StartFlying();
            }

            if (casterPawn.CurJob != null)
            {
                casterPawn.CurJob.flying = true;
            }

            return true;
        }
    }
}
