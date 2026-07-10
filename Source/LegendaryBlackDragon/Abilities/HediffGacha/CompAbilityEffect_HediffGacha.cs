using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
using System.IO;

namespace LegendaryBlackDragon
{
    public class CompAbilityEffect_HediffGacha : CompAbilityEffect
    {
        // 缓存目标，用于窗口选择后的回调
        private Pawn cachedTarget;

        public new CompProperties_AbilityHediffGacha Props =>
            (CompProperties_AbilityHediffGacha)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 验证目标
            if (!target.IsValid || target.Pawn == null)
                return;

            Pawn targetPawn = target.Pawn;
            cachedTarget = targetPawn;

            // 从Hediff池中随机抽取（现在返回HediffPoolEntry列表）
            List<HediffPoolEntry> drawnEntries = DrawHediffsFromPool(Props.choiceCount, targetPawn);

            if (drawnEntries.NullOrEmpty())
            {
                Log.Warning("[LegendaryBlackDragon] HediffGacha: No valid hediffs drawn from pool!");

                // 显示错误消息
                if (Props.showSelectionMessage)
                {
                    Messages.Message("LBD_HediffGacha_NoOptions".Translate(),
                        targetPawn, MessageTypeDefOf.RejectInput);
                }
                return;
            }

            // 打开选择窗口（传递HediffPoolEntry列表，而不是HediffDef列表）
            Window_HediffSelection window = new Window_HediffSelection(
                drawnEntries,
                targetPawn,
                OnHediffSelected,
                Props.windowTitle,
                Props.allowCancel,
                Props
            );

            Find.WindowStack.Add(window);
        }

        /// <summary>
        /// 从Hediff池中随机抽取指定数量的Hediff条目
        /// 排除目标Pawn已经拥有的Hediff
        /// </summary>
        private List<HediffPoolEntry> DrawHediffsFromPool(int count, Pawn targetPawn)
        {
            if (Props.hediffPool.NullOrEmpty())
            {
                Log.Error("[LegendaryBlackDragon] HediffGacha: hediffPool is empty!");
                return null;
            }

            // 获取目标Pawn已有的所有Hediff类型
            HashSet<HediffDef> existingHediffs = GetExistingHediffs(targetPawn);

            // 创建可用池（排除目标已有的Hediff）
            List<HediffPoolEntry> availablePool = new List<HediffPoolEntry>();
            foreach (var entry in Props.hediffPool)
            {
                if (entry?.hediff == null)
                    continue;

                // 检查目标是否已有此Hediff
                bool hasHediff = existingHediffs.Contains(entry.hediff);

                // 如果目标已有此Hediff且设置为替换，则仍然可以抽取
                // 否则，跳过已有的Hediff
                if (!hasHediff || Props.replaceExisting)
                {
                    availablePool.Add(entry);
                }
            }

            // 如果可用池为空，返回空列表
            if (availablePool.Count == 0)
            {
                Log.Message($"[LegendaryBlackDragon] HediffGacha: No available hediffs for {targetPawn.LabelShort}. " +
                           $"Target has {existingHediffs.Count} of {Props.hediffPool.Count} possible hediffs.");
                return new List<HediffPoolEntry>();
            }

            List<HediffPoolEntry> result = new List<HediffPoolEntry>();

            // 如果池子不足以抽取指定数量，则取池子的全部
            int actualCount = System.Math.Min(count, availablePool.Count);

            // 记录日志
            Log.Message($"[LegendaryBlackDragon] HediffGacha: Drawing {actualCount} hediffs from pool of {availablePool.Count} " +
                       $"(excluding {existingHediffs.Count} already owned by {targetPawn.LabelShort})");

            for (int i = 0; i < actualCount; i++)
            {
                if (availablePool.Count == 0)
                    break;

                // 根据权重随机选择
                HediffPoolEntry selected;
                if (Props.useWeights)
                {
                    selected = availablePool.RandomElementByWeight(e => e.weight);
                }
                else
                {
                    selected = availablePool.RandomElement();
                }

                if (selected?.hediff != null)
                {
                    result.Add(selected);

                    // 从池子中移除已选择的，确保单次抽取不重复
                    if (!Props.allowDuplicates)
                    {
                        availablePool.Remove(selected);
                    }

                    // 记录抽取结果
                    Log.Message($"[LegendaryBlackDragon] HediffGacha: Drawn hediff {i + 1}: {selected.hediff.defName}");
                }
            }

            return result;
        }

        /// <summary>
        /// 获取目标Pawn已有的所有Hediff类型
        /// </summary>
        private HashSet<HediffDef> GetExistingHediffs(Pawn targetPawn)
        {
            HashSet<HediffDef> result = new HashSet<HediffDef>();

            if (targetPawn == null || targetPawn.health == null || targetPawn.health.hediffSet == null)
                return result;

            // 获取目标Pawn的所有Hediff
            List<Hediff> allHediffs = targetPawn.health.hediffSet.hediffs;

            if (allHediffs != null)
            {
                foreach (var hediff in allHediffs)
                {
                    if (hediff?.def != null)
                    {
                        result.Add(hediff.def);
                    }
                }
            }

            Log.Message($"[LegendaryBlackDragon] HediffGacha: Target {targetPawn.LabelShort} has {result.Count} unique hediffs");

            // 打印已有的Hediff列表用于调试
            if (result.Count > 0)
            {
                string hediffList = string.Join(", ", result.Select(h => h.defName));
                Log.Message($"[LegendaryBlackDragon] Existing hediffs: {hediffList}");
            }

            return result;
        }

        /// <summary>
        /// 玩家选择Hediff后的回调
        /// </summary>
        private void OnHediffSelected(HediffDef selectedHediff)
        {
            if (selectedHediff == null)
            {
                if (Props.allowCancel)
                {
                    Log.Message("[LegendaryBlackDragon] HediffGacha: Selection canceled by player");
                }
                else
                {
                    Log.Warning("[LegendaryBlackDragon] HediffGacha: No hediff selected");
                }

                cachedTarget = null;
                return;
            }

            if (cachedTarget == null || cachedTarget.Dead)
            {
                Log.Warning("[LegendaryBlackDragon] HediffGacha: Invalid selection or target is dead");
                cachedTarget = null;
                return;
            }

            // 为目标添加Hediff
            ApplyHediffToTarget(cachedTarget, selectedHediff);

            // 播放特效（如果有）
            if (Props.selectionFleck != null && cachedTarget.Map != null)
            {
                FleckMaker.Static(cachedTarget.Position, cachedTarget.Map, Props.selectionFleck);
            }

            // 显示消息（如果配置启用）
            if (Props.showSelectionMessage)
            {
                string message = Props.selectionMessageKey.NullOrEmpty()
                    ? "LBD_HediffGacha_Selected".Translate(cachedTarget.LabelShortCap, selectedHediff.LabelCap)
                    : Props.selectionMessageKey.Translate(cachedTarget.LabelShortCap, selectedHediff.LabelCap);

                Messages.Message(message, cachedTarget, MessageTypeDefOf.PositiveEvent);
            }

            // 清空缓存
            cachedTarget = null;
        }

        /// <summary>
        /// 将Hediff应用到目标
        /// </summary>
        private void ApplyHediffToTarget(Pawn target, HediffDef hediffDef)
        {
            if (target == null || hediffDef == null)
                return;

            // 记录添加前的状态
            bool alreadyHadHediff = target.health.hediffSet.HasHediff(hediffDef);
            Log.Message($"[LegendaryBlackDragon] HediffGacha: Applying {hediffDef.defName} to {target.LabelShort}. " +
                       $"Already has it: {alreadyHadHediff}");

            // 如果已有该Hediff且设置为替换，则先移除
            if (Props.replaceExisting && alreadyHadHediff)
            {
                Hediff existingHediff = target.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (existingHediff != null)
                {
                    target.health.RemoveHediff(existingHediff);
                    Log.Message($"[LegendaryBlackDragon] HediffGacha: Removed existing {hediffDef.defName} from {target.LabelShort}");
                }
            }

            // 创建新的Hediff
            BodyPartRecord targetPart = null;
            if (Props.onlyBrain)
            {
                targetPart = target.health.hediffSet.GetBrain();
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, target, targetPart);

            // 设置严重度（如果有指定）
            if (Props.initialSeverity >= 0f)
            {
                hediff.Severity = Props.initialSeverity;
            }

            // 设置持续时间（如果Hediff支持）
            HediffComp_Disappears disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp != null && Props.durationSeconds > 0)
            {
                disappearsComp.ticksToDisappear = (int)(Props.durationSeconds * 60f);
            }

            // 添加Hediff
            target.health.AddHediff(hediff);

            // 记录添加成功
            Log.Message($"[LegendaryBlackDragon] HediffGacha: Successfully added {hediffDef.defName} to {target.LabelShort}");
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;

            // 验证目标是Pawn
            if (target.Pawn == null)
            {
                if (throwMessages)
                    Messages.Message("LBD_HediffGacha_NeedPawnTarget".Translate(),
                                    MessageTypeDefOf.RejectInput);
                return false;
            }

            // 验证Hediff池不为空
            if (Props.hediffPool.NullOrEmpty())
            {
                if (throwMessages)
                    Messages.Message("LBD_HediffGacha_EmptyPool".Translate(),
                                    MessageTypeDefOf.RejectInput);
                return false;
            }

            // 检查目标是否还有可抽取的Hediff
            Pawn targetPawn = target.Pawn;
            HashSet<HediffDef> existingHediffs = GetExistingHediffs(targetPawn);

            // 计算可用的Hediff数量
            int availableCount = 0;
            foreach (var entry in Props.hediffPool)
            {
                if (entry?.hediff == null)
                    continue;

                bool hasHediff = existingHediffs.Contains(entry.hediff);
                if (!hasHediff || Props.replaceExisting)
                {
                    availableCount++;
                }
            }

            if (availableCount == 0)
            {
                if (throwMessages)
                    Messages.Message("LBD_HediffGacha_NoNewHediffs".Translate(targetPawn.LabelShortCap),
                                    MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.Pawn == null || Props.hediffPool.NullOrEmpty())
                return false;

            // 检查目标是否还有可抽取的Hediff
            Pawn targetPawn = target.Pawn;
            HashSet<HediffDef> existingHediffs = GetExistingHediffs(targetPawn);

            foreach (var entry in Props.hediffPool)
            {
                if (entry?.hediff == null)
                    continue;

                bool hasHediff = existingHediffs.Contains(entry.hediff);
                if (!hasHediff || Props.replaceExisting)
                {
                    return true; // 至少有一个可用的Hediff
                }
            }

            return false; // 没有可用的Hediff
        }

        /// <summary>
        /// 从Hediff池中随机抽取指定数量的Hediff条目
        /// </summary>
        private List<HediffPoolEntry> DrawHediffsFromPool(int count)
        {
            if (Props.hediffPool.NullOrEmpty())
            {
                Log.Error("[LegendaryBlackDragon] HediffGacha: hediffPool is empty!");
                return null;
            }

            // 复制池子以便进行无重复抽取
            List<HediffPoolEntry> availablePool = Props.hediffPool.ListFullCopy();
            List<HediffPoolEntry> result = new List<HediffPoolEntry>();

            // 如果池子不足以抽取指定数量，则取池子的全部
            int actualCount = System.Math.Min(count, availablePool.Count);

            for (int i = 0; i < actualCount; i++)
            {
                if (availablePool.Count == 0)
                    break;

                // 根据权重随机选择
                HediffPoolEntry selected;
                if (Props.useWeights)
                {
                    selected = availablePool.RandomElementByWeight(e => e.weight);
                }
                else
                {
                    selected = availablePool.RandomElement();
                }

                if (selected?.hediff != null)
                {
                    result.Add(selected);

                    // 从池子中移除已选择的，确保不重复
                    if (!Props.allowDuplicates)
                    {
                        availablePool.Remove(selected);
                    }
                }
            }

            return result;
        }
    }
}
