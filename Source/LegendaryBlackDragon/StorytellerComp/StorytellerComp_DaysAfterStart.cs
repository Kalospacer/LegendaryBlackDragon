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

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (Props?.incident == null || target == null)
            {
                yield break;
            }

            // Evaluate only on the primary player-home map to avoid duplicate triggers from temporary maps
            if (target is not Map mapTarget || !mapTarget.IsPlayerHome || mapTarget != Find.AnyPlayerHomeMap)
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
                yield return incident;
            }
        }

        private bool CheckDaysCondition()
        {
            try
            {
                if (Current.Game == null || Find.TickManager == null)
                {
                    return false;
                }

                if (!Props.repeatable && HasTriggeredGlobally())
                {
                    return false;
                }

                if (Props.repeatable)
                {
                    int latestFireTick = GetLatestFireTick(Props.incident);
                    if (latestFireTick >= 0)
                    {
                        int repeatIntervalTicks = Props.repeatIntervalDays * 60000;
                        if (repeatIntervalTicks > 0 && Find.TickManager.TicksGame - latestFireTick < repeatIntervalTicks)
                        {
                            return false;
                        }
                    }
                }

                float daysPassed = GenDate.DaysPassedFloat;
                if (Props.debugLogging)
                {
                    Log.Message($"[DaysAfterStart] daysPassed={daysPassed}, threshold={Props.daysAfterStart}, incident={Props.incident.defName}");
                }

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

            if (GetLatestFireTick(Props.incident) >= 0)
            {
                return true;
            }

            QuestScriptDef questScript = Props.incident.questScriptDef;
            if (questScript != null && Find.QuestManager != null)
            {
                return Find.QuestManager.QuestsListForReading.Any(q => q.root == questScript);
            }

            return false;
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
                    if (Props.debugLogging)
                    {
                        Log.Warning($"[DaysAfterStart] Incident {Props.incident.defName} cannot fire now for target {target}");
                    }
                    return null;
                }

                FiringIncident firingIncident = new FiringIncident(Props.incident, this, parms);

                if (Props.debugLogging)
                {
                    Log.Message($"[DaysAfterStart] Created incident {Props.incident.defName} for target {target} at day {GenDate.DaysPassedFloat}");
                }

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
                Log.Message($"[DaysAfterStart] Force triggered incident: {Props.incident.defName}");
            }
            else
            {
                Log.Error($"[DaysAfterStart] Force trigger failed: {Props.incident.defName}");
            }
        }
    }
}
