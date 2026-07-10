using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace LegendaryBlackDragon
{
    public class CompHediffGiver : ThingComp
    {
        private bool hediffsApplied = false; // 标记是否已经应用过hediff
        private string appliedOnMap = ""; // 记录在哪张地图上应用的
        
        // 用于记录添加的hediff和部位
        private Dictionary<HediffDef, List<BodyPartRecord>> appliedHediffParts = 
            new Dictionary<HediffDef, List<BodyPartRecord>>();

        public CompProperties_HediffGiver Props => (CompProperties_HediffGiver)this.props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // 重要：只在主世界地图第一次spawn时执行，不在地图切换时执行
            TryApplyHediffsOnce();
        }

        /// <summary>
        /// 核心方法：确保hediff只被应用一次
        /// </summary>
        private void TryApplyHediffsOnce()
        {
            try
            {
                // 如果不是pawn，直接返回
                if (!(this.parent is Pawn pawn))
                    return;

                // 如果已经标记为已应用，则不再执行
                if (hediffsApplied)
                {
                    if (Props.debugLogging)
                        Log.Message($"[CompHediffGiver] Hediffs已经应用过，跳过: {pawn.LabelShort}");
                    return;
                }

                // 检查是否是在世界地图上（而不是在某个具体地图中）
                // 如果是世界地图（远征队），我们也不应用hediff
                if (pawn.Map == null)
                {
                    if (Props.debugLogging)
                        Log.Message($"[CompHediffGiver] Pawn在地图外，跳过: {pawn.LabelShort}");
                    return;
                }

                // 只允许在特定类型的地图上应用（通常是主基地地图）
                // 这里可以根据需要调整，默认允许在任何地图上应用，但只应用一次
                // 我们记录应用时的地图，确保不会在其他地图重复应用
                
                // 检查是否允许在当前地图应用
                if (!CanApplyInCurrentMap(pawn.Map))
                {
                    if (Props.debugLogging)
                        Log.Message($"[CompHediffGiver] 不允许在当前地图应用: {pawn.LabelShort}");
                    return;
                }

                // 记录要应用hediff的地图
                appliedOnMap = pawn.Map.uniqueID.ToString();

                // 应用hediff
                AddHediffsToPawn(pawn);
                hediffsApplied = true;

                if (Props.debugLogging)
                    Log.Message($"[CompHediffGiver] 成功应用hediff到: {pawn.LabelShort}, 地图ID: {appliedOnMap}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CompHediffGiver] TryApplyHediffsOnce失败: {ex}");
            }
        }

        /// <summary>
        /// 检查是否允许在当前地图应用hediff
        /// </summary>
        private bool CanApplyInCurrentMap(Map map)
        {
            if (map == null)
                return false;

            // 如果已经有记录的应用地图，检查是否是同一张地图
            if (!string.IsNullOrEmpty(appliedOnMap))
            {
                return map.uniqueID.ToString() == appliedOnMap;
            }

            // 默认允许在玩家家园地图上应用
            if (Props.onlyApplyInPlayerHome && !map.IsPlayerHome)
            {
                if (Props.debugLogging)
                    Log.Message($"[CompHediffGiver] 地图不是玩家家园");
                return false;
            }

            return true;
        }

        private void AddHediffsToPawn(Pawn pawn)
        {
            try
            {
                // 安全检查
                if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
                {
                    Log.Warning($"[CompHediffGiver] Pawn或health组件为空: {this.parent?.Label ?? "null"}");
                    return;
                }

                // 检查是否有hediff列表
                if (Props?.hediffs == null || Props.hediffs.Count == 0)
                {
                    Log.Warning($"[CompHediffGiver] 没有hediff配置: {this.parent?.Label ?? "null"}");
                    return;
                }

                // 检查概率
                if (Props.addChance < 1.0f && Rand.Value > Props.addChance)
                {
                    if (Props.debugLogging)
                        Log.Message($"[CompHediffGiver] 概率检查失败: {pawn.LabelShort}");
                    return;
                }
                
                // 显示应用消息
                if (Props.showApplicationMessage && pawn.Faction == Faction.OfPlayer)
                {
                    string message = Props.applicationMessageKey != null
                        ? Props.applicationMessageKey.Translate(pawn.LabelShort)
                        : "LBD_HediffGiver_Applied".Translate(pawn.LabelShort, Props.hediffs.Count);
                        
                    Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent);
                }

                // 为每个hediff添加到pawn
                foreach (HediffDef hediffDef in Props.hediffs)
                {
                    if (hediffDef == null)
                    {
                        Log.Warning($"[CompHediffGiver] HediffDef为空，跳过");
                        continue;
                    }

                    // 检查是否允许重复添加
                    if (!Props.allowDuplicates && pawn.health.hediffSet.HasHediff(hediffDef))
                    {
                        if (Props.debugLogging)
                            Log.Message($"[CompHediffGiver] 不允许重复，已存在hediff: {hediffDef.defName}");
                        continue;
                    }

                    // 获取身体部位
                    List<BodyPartRecord> bodyParts = GetBodyPartsForHediff(pawn, hediffDef);
                    
                    if (bodyParts == null || bodyParts.Count == 0)
                    {
                        if (Props.skipIfPartMissing)
                        {
                            if (Props.debugLogging)
                                Log.Message($"[CompHediffGiver] 指定部位不存在，跳过: {Props.bodyPart?.defName ?? "null"}");
                            continue;
                        }
                        // 如果不跳过，则添加到全身
                        bodyParts = new List<BodyPartRecord> { null };
                    }
                    
                    // 添加到所有选定部位
                    foreach (var bodyPart in bodyParts)
                    {
                        if (!AddHediffToPawn(pawn, hediffDef, bodyPart))
                        {
                            if (Props.debugLogging)
                                Log.Warning($"[CompHediffGiver] 添加hediff失败: {hediffDef.defName}");
                            continue;
                        }
                        
                        // 记录应用到的部位
                        if (bodyPart != null)
                        {
                            if (!appliedHediffParts.ContainsKey(hediffDef))
                            {
                                appliedHediffParts[hediffDef] = new List<BodyPartRecord>();
                            }
                            appliedHediffParts[hediffDef].Add(bodyPart);
                        }
                        
                        if (Props.debugLogging)
                            Log.Message($"[CompHediffGiver] 成功添加hediff: {hediffDef.defName} 到 {pawn.LabelShort}");
                    }
                }
                
                // 播放应用效果
                if (Props.applicationEffect != null && pawn.Spawned)
                {
                    Effecter effecter = Props.applicationEffect.Spawn();
                    effecter.Trigger(pawn, pawn);
                    effecter.Cleanup();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CompHediffGiver] 添加hediff时出错: {ex}");
            }
        }
        
        /// <summary>
        /// 获取身体部位列表
        /// </summary>
        private List<BodyPartRecord> GetBodyPartsForHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn == null || pawn.RaceProps == null || pawn.RaceProps.body == null)
            {
                if (Props.debugLogging)
                    Log.Warning($"[CompHediffGiver] Pawn或身体为空: {pawn?.LabelShort ?? "null"}");
                return null;
            }
            
            List<BodyPartRecord> result = new List<BodyPartRecord>();
            
            // 使用指定的身体部位
            if (Props.bodyPart != null)
            {
                var parts = pawn.RaceProps.body.GetPartsWithDef(Props.bodyPart);
                if (parts == null || !parts.Any())
                {
                    return null;
                }
                
                // 根据选择模式选择部位
                switch (Props.partSelectionMode)
                {
                    case BodyPartSelectionMode.First:
                        result.Add(parts.FirstOrDefault());
                        break;
                        
                    case BodyPartSelectionMode.Random:
                        result.Add(parts.RandomElement());
                        break;
                        
                    case BodyPartSelectionMode.All:
                        result.AddRange(parts);
                        break;
                        
                    case BodyPartSelectionMode.MostDamaged:
                        result.Add(parts
                            .OrderByDescending(p => pawn.health.hediffSet.GetPartHealth(p) / p.def.GetMaxHealth(pawn))
                            .FirstOrDefault());
                        break;
                        
                    case BodyPartSelectionMode.LeastDamaged:
                        result.Add(parts
                            .OrderBy(p => pawn.health.hediffSet.GetPartHealth(p) / p.def.GetMaxHealth(pawn))
                            .FirstOrDefault());
                        break;
                }
            }
            else if (hediffDef?.defaultInstallPart != null)
            {
                // 使用hediff的默认安装部位
                var parts = pawn.RaceProps.body.GetPartsWithDef(hediffDef.defaultInstallPart);
                result.Add(parts?.FirstOrDefault());
            }
            
            return result;
        }
        
        /// <summary>
        /// 添加hediff到pawn的指定部位
        /// </summary>
        private bool AddHediffToPawn(Pawn pawn, HediffDef hediffDef, BodyPartRecord bodyPart)
        {
            try
            {
                if (pawn == null || hediffDef == null)
                {
                    if (Props.debugLogging)
                        Log.Warning($"[CompHediffGiver] 添加hediff参数为空");
                    return false;
                }
                
                // 检查部位是否有效
                if (bodyPart != null && Props.checkPartValidity)
                {
                    if (!IsBodyPartValid(pawn, bodyPart))
                    {
                        if (Props.debugLogging)
                            Log.Warning($"[CompHediffGiver] 身体部位无效: {bodyPart.def?.defName ?? "null"}");
                        return false;
                    }
                }

                // 检查是否已存在
                Hediff existingHediff = GetExistingHediffOnPart(pawn, hediffDef, bodyPart);
                
                if (existingHediff == null || Props.canUpgrade)
                {
                    Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn, bodyPart);
                    
                    if (hediff == null)
                    {
                        if (Props.debugLogging)
                            Log.Warning($"[CompHediffGiver] 创建hediff失败: {hediffDef.defName}");
                        return false;
                    }
                    
                    // 设置严重度
                    SetHediffSeverity(hediff);
                    
                    // 添加hediff
                    pawn.health.AddHediff(hediff, bodyPart);
                    
                    return true;
                }
                else
                {
                    if (Props.debugLogging)
                        Log.Message($"[CompHediffGiver] 已存在hediff且不允许升级: {hediffDef.defName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CompHediffGiver] 添加hediff {hediffDef?.defName ?? "null"} 到 {pawn?.Label ?? "null"} 失败: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// 设置hediff的严重度
        /// </summary>
        private void SetHediffSeverity(Hediff hediff)
        {
            if (Props.initialSeverity >= 0f)
            {
                hediff.Severity = Props.initialSeverity;
            }
            else if (Props.minSeverity > 0f && Props.maxSeverity > 0f)
            {
                hediff.Severity = Rand.Range(Props.minSeverity, Props.maxSeverity);
            }
        }
        
        /// <summary>
        /// 检查身体部位是否有效
        /// </summary>
        private bool IsBodyPartValid(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null || part == null)
                return false;
                
            // 检查部位是否完整存在
            if (pawn.health.hediffSet.PartIsMissing(part))
                return false;
                
            // 检查部位是否还有血量
            if (pawn.health.hediffSet.GetPartHealth(part) <= 0f)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// 检查指定部位是否已有相同的hediff
        /// </summary>
        private Hediff GetExistingHediffOnPart(Pawn pawn, HediffDef hediffDef, BodyPartRecord bodyPart)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return null;
            
            // 如果bodyPart为null，检查全身
            if (bodyPart == null)
            {
                return pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            }
            
            // 检查指定部位
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == hediffDef && hediff.Part == bodyPart)
                {
                    return hediff;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查是否可以为指定Pawn添加hediff
        /// </summary>
        public AcceptanceReport CanAddHediffToPawn(Pawn pawn)
        {
            if (pawn == null || Props?.hediffs == null)
                return false;
            
            // 检查是否需要特定身体部位
            if (Props.bodyPart != null)
            {
                var bodyPartRecord = pawn.RaceProps.body?.GetPartsWithDef(Props.bodyPart).FirstOrFallback();
                if (bodyPartRecord == null)
                {
                    return "InstallImplantNoBodyPart".Translate() + ": " + Props.bodyPart.LabelShort;
                }
            }
            
            // 检查是否已有hediff（如果不允许重复）
            if (!Props.allowDuplicates)
            {
                foreach (HediffDef hediffDef in Props.hediffs)
                {
                    if (pawn.health.hediffSet.HasHediff(hediffDef))
                    {
                        return "InstallImplantAlreadyInstalled".Translate();
                    }
                }
            }
            
            // 检查是否需要现有hediff才能升级
            if (Props.requiresExistingHediff)
            {
                foreach (HediffDef hediffDef in Props.hediffs)
                {
                    var existingHediff = GetExistingHediffOnPart(pawn, hediffDef, 
                        Props.bodyPart != null ? 
                        pawn.RaceProps.body?.GetPartsWithDef(Props.bodyPart).FirstOrFallback() : 
                        null);
                    
                    if (existingHediff == null)
                    {
                        return "InstallImplantHediffRequired".Translate(hediffDef.label);
                    }
                }
            }
            
            return true;
        }

        // 序列化
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hediffsApplied, "hediffsApplied", false);
            Scribe_Values.Look(ref appliedOnMap, "appliedOnMap", "");
            
            // 序列化已应用的hediff部位记录
            Scribe_Collections.Look(ref appliedHediffParts, "appliedHediffParts", 
                LookMode.Def, LookMode.BodyPart);
            
            if (Props.debugLogging && Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Log.Message($"[CompHediffGiver] 加载数据: hediffsApplied={hediffsApplied}, appliedOnMap={appliedOnMap}");
            }
        }

        // 调试方法
        public void DebugApplyHediffs()
        {
            if (DebugSettings.godMode && this.parent is Pawn pawn && !hediffsApplied)
            {
                Log.Message($"[CompHediffGiver] 调试：手动应用hediff到 {pawn.LabelShort}");
                AddHediffsToPawn(pawn);
                hediffsApplied = true;
            }
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CompHediffGiver Debug Info ===");
            sb.AppendLine($"Parent: {parent?.Label ?? "null"}");
            sb.AppendLine($"Hediffs Applied: {hediffsApplied}");
            sb.AppendLine($"Applied On Map: {appliedOnMap ?? "null"}");
            sb.AppendLine($"Current Map ID: {(parent?.Map?.uniqueID.ToString() ?? "null")}");
            
            return sb.ToString();
        }
    }
}
