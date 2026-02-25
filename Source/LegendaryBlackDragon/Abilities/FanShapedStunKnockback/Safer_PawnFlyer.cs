using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class SaferPawnFlyer : PawnFlyer
    {
        // 重写TickInterval，增加空值检查
        protected override void TickInterval(int delta)
        {
            // 如果FlyingThing为空，直接销毁并返回
            if (FlyingThing == null || FlyingThing.Destroyed)
            {
                Destroy();
                return;
            }

            base.TickInterval(delta);
        }

        // 重写RespawnPawn，增加空值检查
        protected override void RespawnPawn()
        {
            // 如果FlyingThing为空，直接销毁并返回
            if (FlyingThing == null || FlyingThing.Destroyed)
            {
                Destroy();
                return;
            }

            // 调用基类方法
            base.RespawnPawn();
        }
    }
}
