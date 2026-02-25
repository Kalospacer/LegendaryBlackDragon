using Verse;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 装备脱下时自动销毁
    /// </summary>
    public class CompDestroyOnUnequip : ThingComp
    {
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            
            if (parent != null && !parent.Destroyed)
            {
                parent.Destroy(DestroyMode.Vanish);
            }
        }
    }

    /// <summary>
    /// 组件属性
    /// </summary>
    public class CompProperties_DestroyOnUnequip : CompProperties
    {
        public CompProperties_DestroyOnUnequip()
        {
            compClass = typeof(CompDestroyOnUnequip);
        }
    }
}