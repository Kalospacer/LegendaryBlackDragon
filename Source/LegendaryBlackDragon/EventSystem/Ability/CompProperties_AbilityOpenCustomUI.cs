using System; // Required for Activator
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Text.RegularExpressions;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// AbilityComp 属性：用于打开自定义UI的能力组件
    /// </summary>
    public class CompProperties_AbilityOpenCustomUI : CompProperties_AbilityEffect
    {
        /// <summary>
        /// 要打开的 EventDef 的名称
        /// </summary>
        public string uiDefName;
        
        /// <summary>
        /// 延迟执行的 tick 数（0表示立即执行）
        /// </summary>
        public int delayTicks = 0;
        
        public CompProperties_AbilityOpenCustomUI()
        {
            compClass = typeof(CompAbilityEffect_OpenCustomUI);
        }
    }
    
    /// <summary>
    /// AbilityComp 实现：用于打开自定义UI的能力效果组件
    /// </summary>
    public class CompAbilityEffect_OpenCustomUI : CompAbilityEffect
    {
        public new CompProperties_AbilityOpenCustomUI Props => 
            (CompProperties_AbilityOpenCustomUI)props;
        
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            // 执行打开UI的逻辑
            ExecuteOpenUI();
        }
        
        private void ExecuteOpenUI()
        {
            if (Props.delayTicks > 0)
            {
                // 延迟打开
                var actionManager = Find.World.GetComponent<DelayedActionManager>();
                if (actionManager != null)
                {
                    actionManager.AddAction(Props.uiDefName, Props.delayTicks);
                }
                else
                {
                    Log.Message("[LBD] DelayedActionManager not found. Cannot schedule delayed UI opening.");
                    OpenUI();
                }
            }
            else
            {
                // 立即打开
                OpenUI();
            }
        }
        
        private void OpenUI()
        {
            EventDef eventDef = DefDatabase<EventDef>.GetNamed(Props.uiDefName, false);
            if (eventDef != null)
            {
                if (eventDef.hiddenWindow)
                {
                    // 对于隐藏窗口，直接执行 dismissEffects
                    if (!eventDef.dismissEffects.NullOrEmpty())
                    {
                        foreach (var conditionalEffect in eventDef.dismissEffects)
                        {
                            string reason;
                            if (AreConditionsMet(conditionalEffect.conditions, out reason))
                            {
                                conditionalEffect.Execute(null);
                            }
                        }
                    }
                }
                else
                {
                    // 创建并显示窗口
                    Window window = (Window)Activator.CreateInstance(eventDef.windowType, eventDef);
                    Find.WindowStack.Add(window);
                }
            }
            else
            {
                Log.Message($"[LBD] AbilityOpenCustomUI could not find EventDef named '{Props.uiDefName}'");
            }
        }
        
        private bool AreConditionsMet(List<ConditionBase> conditions, out string reason)
        {
            reason = "";
            if (conditions.NullOrEmpty())
            {
                return true;
            }

            foreach (var condition in conditions)
            {
                if (!condition.IsMet(out string singleReason))
                {
                    reason = singleReason;
                    return false;
                }
            }
            return true;
        }
        
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
            
            // 验证 EventDef 存在
            EventDef eventDef = DefDatabase<EventDef>.GetNamed(Props.uiDefName, false);
            if (eventDef == null)
            {
                if (throwMessages)
                    Messages.Message($"Event '{Props.uiDefName}' not found", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }
        
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest);
        }
    }
}
