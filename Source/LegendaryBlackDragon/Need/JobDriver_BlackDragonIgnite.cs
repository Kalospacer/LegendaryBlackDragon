using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 自定义点火 JobDriver，用于纵火狂点火时触发 Need 恢复
    /// </summary>
    public class JobDriver_BlackDragonIgnite : JobDriver
    {
        public const TargetIndex TargetInd = TargetIndex.A;

        public Thing TargetThing => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            
            // 第一个 Toil：移动到目标
            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnBurningImmobile(TargetIndex.A);
            yield return gotoToil;
            
            // 第二个 Toil：执行点火
            Toil igniteToil = ToilMaker.MakeToil("MakeNewToils");
            igniteToil.initAction = delegate
            {
                // 执行原版点火逻辑
                pawn.natives.TryStartIgnite(TargetThing);
                
                // 触发 Need 恢复
                var need = pawn.needs?.TryGetNeed<Need_BlackDragon>();
                if (need != null)
                {
                    need.ReportFireStarted();
                    
                    // 给 Thought 报告点火
                    var thoughtDef = need.GetCurrentThoughtDef();
                    if (thoughtDef != null)
                    {
                        var thought = pawn.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(thoughtDef);
                        if (thought is Thought_BlackDragon blackDragonThought)
                        {
                            blackDragonThought.ReportFireStart();
                        }
                    }
                }
            };
            
            igniteToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return igniteToil;
        }
    }
}
