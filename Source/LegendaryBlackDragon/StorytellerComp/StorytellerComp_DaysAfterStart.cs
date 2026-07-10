using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    public class StorytellerComp_DaysAfterStart : StorytellerComp
    {
        private StorytellerCompProperties_DaysAfterStart Props => (StorytellerCompProperties_DaysAfterStart)props;
        
        // 组件唯一ID
        private string compId;
        private string CompId
        {
            get
            {
                if (compId == null)
                {
                    compId = DaysAfterStartManager.GenerateCompId(Props);
                }
                return compId;
            }
        }
        
        // 延迟触发标记
        private bool hasScheduledTrigger = false;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (Props?.incident == null || target == null)
            {
                yield break;
            }

            // 评估仅限于主玩家家园地图，避免从临时地图重复触发
            if (target is not Map mapTarget || !mapTarget.IsPlayerHome || mapTarget != Find.AnyPlayerHomeMap)
            {
                yield break;
            }

            // 检查是否已安排延迟触发
            if (hasScheduledTrigger)
            {
                yield break;
            }

            if (!CheckDaysCondition())
            {
                yield break;
            }

            FiringIncident incident = CreateIncident(target);
            if (incident != null)
            {
                // 注册到全局管理器
                RegisterWithManager();
                
                // 检查是否可以触发
                if (CanTriggerFromManager())
                {
                    // 如果有延迟，安排延迟触发
                    if (Props.delayTicks > 0)
                    {
                        hasScheduledTrigger = ScheduleDelayedTrigger(target);
                    }
                    else
                    {
                        // 记录触发
                        DaysAfterStartManager.Instance.RecordTrigger(CompId, Props.incident);
                        
                        yield return incident;
                    }
                }
            }
        }
        
        /// <summary>
        /// 注册到全局管理器
        /// </summary>
        private void RegisterWithManager()
        {
            var manager = DaysAfterStartManager.Instance;
            if (manager != null)
            {
                manager.RegisterComp(CompId, Props);
            }
        }
        
        /// <summary>
        /// 检查是否可以从管理器触发
        /// </summary>
        private bool CanTriggerFromManager()
        {
            var manager = DaysAfterStartManager.Instance;
            if (manager == null)
            {
                // 如果管理器不存在，使用原来的逻辑
                return CheckDaysCondition() && !HasTriggeredGlobally();
            }
            
            return manager.CanTriggerIncident(CompId, Props.incident, Props.repeatable, Props.repeatIntervalDays);
        }
        
        /// <summary>
        /// 安排延迟触发
        /// </summary>
        private bool ScheduleDelayedTrigger(IIncidentTarget target)
        {
            var manager = DaysAfterStartManager.Instance;
            if (manager == null)
            {
                return false;
            }

            if (!manager.ScheduleTrigger(CompId, Props.incident, Props.delayTicks, target))
            {
                return false;
            }

            manager.RecordTrigger(CompId, Props.incident);
            return true;
        }

        private bool CheckDaysCondition()
        {
            try
            {
                if (Current.Game == null || Find.TickManager == null)
                {
                    return false;
                }

                float daysPassed = GenDate.DaysPassedFloat;

                return daysPassed >= Props.daysAfterStart;
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStart] CheckDaysCondition error: {ex}");
                return false;
            }
        }

        private bool HasTriggeredGlobally()
        {
            if (Props?.incident == null)
            {
                return false;
            }
            
            // 使用管理器检查
            var manager = DaysAfterStartManager.Instance;
            if (manager != null)
            {
                var record = manager.GetTriggerRecord(CompId);
                return record.HasTriggered;
            }
            
            // 后备：使用原来的检查逻辑
            return GetLatestFireTick(Props.incident) >= 0;
        }

        private static int GetLatestFireTick(IncidentDef incident)
        {
            if (incident == null)
            {
                return -1;
            }

            int latest = -1;

            if (Find.World?.StoryState?.lastFireTicks != null &&
                Find.World.StoryState.lastFireTicks.TryGetValue(incident, out int worldTick))
            {
                latest = worldTick;
            }

            foreach (Map map in Find.Maps)
            {
                if (map?.StoryState?.lastFireTicks == null)
                {
                    continue;
                }

                if (map.StoryState.lastFireTicks.TryGetValue(incident, out int mapTick) && mapTick > latest)
                {
                    latest = mapTick;
                }
            }

            return latest;
        }

        private FiringIncident CreateIncident(IIncidentTarget target)
        {
            try
            {
                if (Props.incident == null)
                {
                    Log.Error("[DaysAfterStart] IncidentDef is null");
                    return null;
                }

                if (!Props.incident.TargetAllowed(target))
                {
                    return null;
                }

                IncidentParms parms = GenerateParms(Props.incident.category, target);
                if (!Props.incident.Worker.CanFireNow(parms))
                {
                    return null;
                }

                FiringIncident firingIncident = new FiringIncident(Props.incident, this, parms);

                return firingIncident;
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStart] CreateIncident error: {ex}");
                return null;
            }
        }

        public override IncidentParms GenerateParms(IncidentCategoryDef category, IIncidentTarget target)
        {
            return StorytellerUtility.DefaultParmsNow(category, target);
        }

        public string GetStatus(IIncidentTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== DaysAfterStart StorytellerComp Status ===");
            sb.AppendLine($"Target: {target}");
            sb.AppendLine($"Comp ID: {CompId}");
            sb.AppendLine($"Incident: {Props.incident?.defName ?? "NULL"}");
            sb.AppendLine($"Days after start: {Props.daysAfterStart}");
            sb.AppendLine($"Current days passed: {GenDate.DaysPassedFloat}");
            sb.AppendLine($"Has triggered globally: {HasTriggeredGlobally()}");
            sb.AppendLine($"Repeatable: {Props.repeatable}");
            if (Props.repeatable)
            {
                sb.AppendLine($"Repeat interval days: {Props.repeatIntervalDays}");
            }
            sb.AppendLine($"Can trigger now: {CheckDaysCondition()}");
            sb.AppendLine($"Has scheduled trigger: {hasScheduledTrigger}");
            
            // 添加管理器状态
            var manager = DaysAfterStartManager.Instance;
            if (manager != null)
            {
                sb.AppendLine($"Manager enabled: {manager.IsCompEnabled(CompId)}");
                var record = manager.GetTriggerRecord(CompId);
                sb.AppendLine($"Manager trigger count: {record.TriggerCount}");
                sb.AppendLine($"Manager last trigger: {record.LastTriggerTick} ticks ago");
            }
            
            return sb.ToString();
        }

        public void ForceTrigger(IIncidentTarget target)
        {
            FiringIncident incident = CreateIncident(target);
            if (incident == null)
            {
                return;
            }

            if (Props.incident.Worker.TryExecute(incident.parms))
            {
                target.StoryState.Notify_IncidentFired(incident);
                
                // 记录到管理器
                var manager = DaysAfterStartManager.Instance;
                if (manager != null)
                {
                    manager.RecordTrigger(CompId, Props.incident);
                }
            }
            else
            {
                Log.Error($"[DaysAfterStart] Force trigger failed: {Props.incident.defName}");
            }
        }
        
        public void PostExposeData()
        {
            // 序列化组件ID和延迟触发标记
            Scribe_Values.Look(ref compId, "compId");
            Scribe_Values.Look(ref hasScheduledTrigger, "hasScheduledTrigger", false);
        }
    }
}
