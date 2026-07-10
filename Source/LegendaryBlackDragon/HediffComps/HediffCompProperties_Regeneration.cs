using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace LegendaryBlackDragon
{
    public class HediffCompProperties_Regeneration : HediffCompProperties
    {
        public float activeSeverity = 0.5f;      // 有能量且损伤时的严重性
        public float inactiveSeverity = 1.5f;    // 其他情况的严重性
        public bool useRepairResource = false;
        public NeedDef repairResourceNeed;
        public List<HediffDef> additionalRepairableHediffs;
        public float repairCostPerHP = 0.03f;
        public int repairCooldownAfterDamage = 600; // 受到伤害后的修复冷却时间

        public HediffCompProperties_Regeneration()
        {
            compClass = typeof(HediffComp_Regeneration);
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (useRepairResource && repairResourceNeed == null)
            {
                yield return "useRepairResource is enabled but repairResourceNeed is null";
            }

            if (repairCostPerHP < 0f)
            {
                yield return "repairCostPerHP must be non-negative";
            }
        }
    }

    public class HediffComp_Regeneration : HediffComp
    {
        public HediffCompProperties_Regeneration Props => (HediffCompProperties_Regeneration)props;
        
        private int lastDamageTick = -9999;
        private const int CheckInterval = 60;
        private int debugCounter = 0;
        public bool repairSystemEnabled = true; // 默认开启修复系统

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 如果修复系统关闭，跳过所有修复逻辑
            if (!repairSystemEnabled)
            {
                // 如果系统关闭，设置为不活跃状态
                if (parent.Severity != Props.inactiveSeverity)
                {
                    parent.Severity = Props.inactiveSeverity;
                }
                return;
            }

            // 每60 ticks检查一次状态
            if (Find.TickManager.TicksGame % CheckInterval == 0)
            {
                debugCounter++;
                UpdateSeverityAndRepair();
            }
        }

        private void UpdateSeverityAndRepair()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            bool shouldBeActive = ShouldBeActive();
            float targetSeverity = shouldBeActive ? Props.activeSeverity : Props.inactiveSeverity;

            // 更新严重性
            if (parent.Severity != targetSeverity)
            {
                parent.Severity = targetSeverity;
            }

            // 如果处于活跃状态，执行修复
            if (shouldBeActive)
            {
                TryRepairDamage();
            }
        }

        private bool ShouldBeActive()
        {
            // 如果修复系统关闭，直接返回不活跃
            if (!repairSystemEnabled)
            {
                return false;
            }

            // 检查是否在冷却期内
            int cooldownRemaining = Props.repairCooldownAfterDamage - (Find.TickManager.TicksGame - lastDamageTick);
            if (cooldownRemaining > 0)
            {
                return false;
            }

            // 检查是否有需要修复的损伤
            if (!HasDamageToRepair())
            {
                return false;
            }

            return true;
        }

        private bool HasDamageToRepair()
        {
            if (Pawn.health == null || Pawn.health.hediffSet == null)
            {
                return false;
            }

            // 检查是否有缺失部件
            var missingParts = Pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (missingParts.Count > 0)
            {
                return true;
            }

            // 检查是否有损伤
            if (HasDamagedParts())
            {
                return true;
            }

            // 不再检查疾病
            return false;
        }

        // 使用 GetPartHealth 检测损伤
        private bool HasDamagedParts()
        {
            var bodyParts = Pawn.RaceProps.body.AllParts;
            int damagedCount = 0;
            
            foreach (var part in bodyParts)
            {
                // 如果部位不是缺失的，但健康值小于最大健康值，说明有损伤
                if (!Pawn.health.hediffSet.PartIsMissing(part))
                {
                    float maxHealth = part.def.GetMaxHealth(Pawn);
                    float currentHealth = Pawn.health.hediffSet.GetPartHealth(part);
                    
                    // 不再使用修复容忍度，任何损伤都需要修复
                    if (currentHealth < maxHealth)
                    {
                        damagedCount++;
                    }
                }
            }
                
            return damagedCount > 0;
        }

        // 获取所有需要修复的部位
        private List<BodyPartRecord> GetDamagedParts()
        {
            var damagedParts = new List<BodyPartRecord>();
            var bodyParts = Pawn.RaceProps.body.AllParts;
            
            foreach (var part in bodyParts)
            {
                if (!Pawn.health.hediffSet.PartIsMissing(part))
                {
                    float maxHealth = part.def.GetMaxHealth(Pawn);
                    float currentHealth = Pawn.health.hediffSet.GetPartHealth(part);
                    
                    // 不再使用修复容忍度，任何损伤都需要修复
                    if (currentHealth < maxHealth)
                    {
                        damagedParts.Add(part);
                    }
                }
            }
                
            return damagedParts;
        }

        private void TryRepairDamage()
        {
            // 优先修复缺失部件
            if (TryRepairMissingParts())
            {
                return;
            }

            // 然后修复损伤
            if (TryRepairDamagedParts())
            {
                return;
            }

            // 不再修复疾病
        }

        private bool TryRepairMissingParts()
        {
            var missingParts = Pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (missingParts == null || missingParts.Count == 0)
            {
                return false;
            }

            // 选择最小的缺失部件进行修复（成本较低）
            Hediff_MissingPart partToRepair = null;
            float minHealth = float.MaxValue;

            foreach (var missingPart in missingParts)
            {
                float partHealth = missingPart.Part.def.GetMaxHealth(Pawn);
                if (partHealth < minHealth)
                {
                    minHealth = partHealth;
                    partToRepair = missingPart;
                }
            }

            if (partToRepair != null)
            {
                float repairCost = minHealth * Props.repairCostPerHP;

                if (!CanAffordRepair(repairCost))
                {
                    return false;
                }

                if (ConvertMissingPartToInjury(partToRepair))
                {
                    ConsumeRepairCost(repairCost);
                    return true;
                }
            }
            return false;
        }

        private bool TryRepairDamagedParts()
        {
            var damagedParts = GetDamagedParts();
            if (damagedParts.Count == 0)
            {
                return false;
            }

            // 选择健康值最低的部位进行修复
            BodyPartRecord partToRepair = null;
            float minHealthRatio = float.MaxValue;

            foreach (var part in damagedParts)
            {
                float maxHealth = part.def.GetMaxHealth(Pawn);
                float currentHealth = Pawn.health.hediffSet.GetPartHealth(part);
                float healthRatio = currentHealth / maxHealth;
                
                if (healthRatio < minHealthRatio)
                {
                    minHealthRatio = healthRatio;
                    partToRepair = part;
                }
            }

            if (partToRepair != null)
            {
                float maxHealth = partToRepair.def.GetMaxHealth(Pawn);
                float currentHealth = Pawn.health.hediffSet.GetPartHealth(partToRepair);
                float healthToRepair = maxHealth - currentHealth;

                float repairCost = healthToRepair * Props.repairCostPerHP;
                if (!CanAffordRepair(repairCost))
                {
                    return false;
                }

                if (RepairDamagedPart(partToRepair))
                {
                    ConsumeRepairCost(repairCost);
                    return true;
                }
            }
            return false;
        }

        private bool CanAffordRepair(float repairCost)
        {
            if (!Props.useRepairResource || repairCost <= 0f)
            {
                return true;
            }

            if (Props.repairResourceNeed == null)
            {
                return false;
            }

            Need resourceNeed = Pawn.needs?.TryGetNeed(Props.repairResourceNeed);
            return resourceNeed != null && resourceNeed.CurLevel >= repairCost;
        }

        private void ConsumeRepairCost(float repairCost)
        {
            if (!Props.useRepairResource || repairCost <= 0f)
            {
                return;
            }

            if (Props.repairResourceNeed == null)
            {
                return;
            }

            Need resourceNeed = Pawn.needs?.TryGetNeed(Props.repairResourceNeed);
            if (resourceNeed != null)
            {
                resourceNeed.CurLevel = Mathf.Max(0f, resourceNeed.CurLevel - repairCost);
            }
        }

        // 新的修复逻辑：完美修复所有伤口
        private bool RepairDamagedPart(BodyPartRecord part)
        {
            try
            {
                float maxHealth = part.def.GetMaxHealth(Pawn);
                float currentHealth = Pawn.health.hediffSet.GetPartHealth(part);
                
                // 获取该部位的所有hediff
                var hediffsOnPart = new List<Hediff>();
                foreach (var hediff in Pawn.health.hediffSet.hediffs)
                {
                    if (hediff.Part == part)
                    {
                        hediffsOnPart.Add(hediff);
                    }
                }
                
                if (hediffsOnPart.Count == 0)
                {
                    return false;
                }
                
                bool anyRepairDone = false;
                
                foreach (var hediff in hediffsOnPart)
                {
                    // 检查hediff是否可修复
                    if (!CanRepairHediff(hediff))
                    {
                        continue;
                    }
                    
                    // 新的修复逻辑：对于小于1的伤口，直接删除
                    if (hediff.Severity < 1.0f)
                    {
                        Pawn.health.RemoveHediff(hediff);
                        anyRepairDone = true;
                    }
                    else
                    {
                        // 对于大于等于1的伤口，完全修复
                        float originalSeverity = hediff.Severity;
                        hediff.Severity = 0f;
                        Pawn.health.RemoveHediff(hediff);
                        anyRepairDone = true;
                    }
                }
                
                return anyRepairDone;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // 检查hediff是否可修复
        private bool CanRepairHediff(Hediff hediff)
        {
            // 跳过疾病
            if (IsDisease(hediff))
            {
                return false;
            }
            
            if (Props.additionalRepairableHediffs?.Contains(hediff.def) == true)
                return true;
            
            // 如果是损伤类型的hediff，可以修复
            if (hediff is Hediff_Injury)
                return true;
            
            // 其他情况不可修复
            return false;
        }

        // 检查是否是疾病
        private bool IsDisease(Hediff hediff)
        {
            // 这里可以定义哪些hediff被认为是疾病
            // 常见的疾病类型
            string[] diseaseKeywords = {
                "Disease", "Flu", "Plague", "Infection", "Malaria", 
                "SleepingSickness", "FibrousMechanites", "SensoryMechanites",
                "WoundInfection", "FoodPoisoning", "GutWorms", "MuscleParasites"
            };
            
            foreach (string keyword in diseaseKeywords)
            {
                if (hediff.def.defName.Contains(keyword))
                    return true;
            }
            
            return false;
        }

        // 将缺失部件转换为指定的hediff
        private bool ConvertMissingPartToInjury(Hediff_MissingPart missingPart)
        {
            try
            {
                float partMaxHealth = missingPart.Part.def.GetMaxHealth(Pawn);
                
                // 关键修复：确保转换后的损伤不会导致部位再次缺失
                // 我们设置损伤严重性为最大健康值-1，这样部位健康值至少为1
                float injurySeverity = partMaxHealth - 1;
                
                // 如果最大健康值为1，则设置为0.5，确保部位健康值大于0
                if (partMaxHealth <= 1)
                {
                    injurySeverity = 0.5f;
                }
                
                // 移除缺失部件hediff
                Pawn.health.RemoveHediff(missingPart);
                
                // 添加指定的hediff (Crush)
                HediffDef injuryDef = DefDatabase<HediffDef>.GetNamedSilentFail("Crush");
                if (injuryDef == null)
                {
                    return false;
                }
                
                // 创建损伤
                Hediff injury = HediffMaker.MakeHediff(injuryDef, Pawn, missingPart.Part);
                injury.Severity = injurySeverity;
                
                Pawn.health.AddHediff(injury);
                
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            
            // 记录最后一次受到伤害的时间
            lastDamageTick = Find.TickManager.TicksGame;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn.Faction == Faction.OfPlayer)
            {
                Command_Toggle toggleCommand = new Command_Toggle
                {
                    defaultLabel = repairSystemEnabled ? "LBD_Regeneration_Disable".Translate() : "LBD_Regeneration_Enable".Translate(),
                    defaultDesc = repairSystemEnabled ? "LBD_Regeneration_DisableDesc".Translate() : "LBD_Regeneration_EnableDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("LegendaryBlackDragon/UI/Commands/LBD_RegenerationHediff_Switch"),
                    isActive = () => repairSystemEnabled,
                    toggleAction = () => {
                        repairSystemEnabled = !repairSystemEnabled;
                    },
                    hotKey = KeyBindingDefOf.Misc1
                };

                yield return toggleCommand;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                string status = repairSystemEnabled ? 
                    (ShouldBeActive() ? 
                        "LBD_Regeneration_Active".Translate() : 
                        "LBD_Regeneration_Inactive".Translate()) :
                    "LBD_Regeneration_Disabled".Translate();
                return status;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastDamageTick, "lastDamageTick", -9999);
            Scribe_Values.Look(ref repairSystemEnabled, "repairSystemEnabled", true);
        }
    }
}
