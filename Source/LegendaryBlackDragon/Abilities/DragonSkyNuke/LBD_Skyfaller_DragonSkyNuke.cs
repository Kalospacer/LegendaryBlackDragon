using RimWorld;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class LBD_Skyfaller_DragonSkyNukeLeaving : Skyfaller_FlyingPawn
    {
        protected override void LeaveMap()
        {
            if (innerContainer != null && innerContainer.Count > 0)
            {
                if (innerContainer[0] is Pawn pawn)
                {
                    innerContainer.Remove(pawn);
                    pawn.Drawer?.renderer?.SetAnimation(null);
                }
            }

            base.LeaveMap();
        }
    }

    public class LBD_Skyfaller_DragonSkyNukeArrival : Skyfaller_FlyingPawn
    {
        private Pawn ContainedPawn
        {
            get
            {
                if (innerContainer == null || innerContainer.Count <= 0)
                {
                    return null;
                }

                return innerContainer[0] as Pawn;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            if (innerContainer == null || innerContainer.Count <= 0)
            {
                Destroy();
                return;
            }

            base.SpawnSetup(map, respawningAfterLoad);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = ContainedPawn;
            if (pawn == null)
            {
                return;
            }

            GetDrawPositionAndRotation(ref drawLoc, out _);
            pawn.DrawNowAt(drawLoc, flip);
            DrawDropSpotShadow();
        }

        protected override void SpawnThings()
        {
            Pawn pawn = ContainedPawn;
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, Position, Map);
            pawn.Rotation = Rot4.East;
            pawn.Drawer.renderer.SetAnimation(null);
        }
    }
}
