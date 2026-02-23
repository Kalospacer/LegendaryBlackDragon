using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LegendaryBlackDragon
{
    /// <summary>
    /// 基于天数的Storyteller触发器全局管理器
    /// </summary>
    public class DaysAfterStartManager : GameComponent
    {
        private static DaysAfterStartManager instance;
        public static DaysAfterStartManager Instance => instance;
        
        // 存储所有注册的StorytellerComp配置
        private Dictionary<string, StorytellerCompProperties_DaysAfterStart> registeredCompProperties;
        
        // 触发记录：compId -> 触发信息
        private Dictionary<string, TriggerRecord> triggerRecords;
        
        // 配置状态：compId -> 是否启用
        private Dictionary<string, bool> compEnabledStates;
        
        // 延迟触发队列
        private List<ScheduledTrigger> scheduledTriggers;
        
        // 调试模式
        private bool debugMode = false;
        private const int DebugLogInterval = 60000; // 每游戏分钟记录一次
        
        // === 新增：用于序列化的临时列表 ===
        private List<string> serializedTriggerRecordKeys;
        private List<TriggerRecord> serializedTriggerRecordValues;
        private List<string> serializedCompEnabledStatesKeys;
        private List<bool> serializedCompEnabledStatesValues;
        
        public DaysAfterStartManager(Game game)
        {
            instance = this;
            Initialize();
        }
        
        private void Initialize()
        {
            registeredCompProperties = new Dictionary<string, StorytellerCompProperties_DaysAfterStart>();
            triggerRecords = new Dictionary<string, TriggerRecord>();
            compEnabledStates = new Dictionary<string, bool>();
            scheduledTriggers = new List<ScheduledTrigger>();
            
            serializedTriggerRecordKeys = new List<string>();
            serializedTriggerRecordValues = new List<TriggerRecord>();
            serializedCompEnabledStatesKeys = new List<string>();
            serializedCompEnabledStatesValues = new List<bool>();
            
            Log.Message("[DaysAfterStartManager] Initialized global manager");
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            // 序列化触发记录
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 保存时清理无效记录
                CleanupInvalidRecords();
                
                // 将字典转换为列表以便序列化
                serializedTriggerRecordKeys = new List<string>(triggerRecords.Keys);
                serializedTriggerRecordValues = new List<TriggerRecord>(triggerRecords.Values);
                
                serializedCompEnabledStatesKeys = new List<string>(compEnabledStates.Keys);
                serializedCompEnabledStatesValues = new List<bool>(compEnabledStates.Values);
                
                Scribe_Collections.Look(ref serializedTriggerRecordKeys, "triggerRecordKeys", LookMode.Value);
                Scribe_Collections.Look(ref serializedTriggerRecordValues, "triggerRecordValues", LookMode.Deep);
                
                Scribe_Collections.Look(ref serializedCompEnabledStatesKeys, "compEnabledStatesKeys", LookMode.Value);
                Scribe_Collections.Look(ref serializedCompEnabledStatesValues, "compEnabledStatesValues", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 加载时从列表重建字典
                serializedTriggerRecordKeys = new List<string>();
                serializedTriggerRecordValues = new List<TriggerRecord>();
                serializedCompEnabledStatesKeys = new List<string>();
                serializedCompEnabledStatesValues = new List<bool>();
                
                Scribe_Collections.Look(ref serializedTriggerRecordKeys, "triggerRecordKeys", LookMode.Value);
                Scribe_Collections.Look(ref serializedTriggerRecordValues, "triggerRecordValues", LookMode.Deep);
                
                Scribe_Collections.Look(ref serializedCompEnabledStatesKeys, "compEnabledStatesKeys", LookMode.Value);
                Scribe_Collections.Look(ref serializedCompEnabledStatesValues, "compEnabledStatesValues", LookMode.Value);
                
                // 重建字典
                triggerRecords = new Dictionary<string, TriggerRecord>();
                compEnabledStates = new Dictionary<string, bool>();
                
                if (serializedTriggerRecordKeys != null && serializedTriggerRecordValues != null &&
                    serializedTriggerRecordKeys.Count == serializedTriggerRecordValues.Count)
                {
                    for (int i = 0; i < serializedTriggerRecordKeys.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(serializedTriggerRecordKeys[i]) && 
                            serializedTriggerRecordValues[i] != null)
                        {
                            triggerRecords[serializedTriggerRecordKeys[i]] = serializedTriggerRecordValues[i];
                        }
                    }
                }
                
                if (serializedCompEnabledStatesKeys != null && serializedCompEnabledStatesValues != null &&
                    serializedCompEnabledStatesKeys.Count == serializedCompEnabledStatesValues.Count)
                {
                    for (int i = 0; i < serializedCompEnabledStatesKeys.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(serializedCompEnabledStatesKeys[i]))
                        {
                            compEnabledStates[serializedCompEnabledStatesKeys[i]] = serializedCompEnabledStatesValues[i];
                        }
                    }
                }
                
                Log.Message($"[DaysAfterStartManager] Loaded {triggerRecords?.Count ?? 0} trigger records");
            }
            
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            
            // 注意：registeredCompProperties 不需要序列化，因为会在游戏加载时重新注册
            // scheduledTriggers 也不需要序列化，因为它们是临时的
        }
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 处理延迟触发
            ProcessScheduledTriggers();
            
            // 定期调试日志
            if (debugMode && Find.TickManager.TicksGame % DebugLogInterval == 0)
            {
                LogManagerStatus();
            }
        }
        
        /// <summary>
        /// 注册StorytellerComp配置
        /// </summary>
        public void RegisterComp(string compId, StorytellerCompProperties_DaysAfterStart props)
        {
            if (string.IsNullOrEmpty(compId) || props == null)
            {
                Log.Warning("[DaysAfterStartManager] Attempted to register null comp or compId");
                return;
            }
            
            if (!registeredCompProperties.ContainsKey(compId))
            {
                registeredCompProperties[compId] = props;
                
                // 如果还没有启用状态记录，设置默认启用
                if (!compEnabledStates.ContainsKey(compId))
                {
                    compEnabledStates[compId] = true;
                }
                
                if (debugMode)
                {
                    Log.Message($"[DaysAfterStartManager] Registered comp: {compId}, Incident: {props.incident?.defName}");
                }
            }
        }
        
        /// <summary>
        /// 注销StorytellerComp配置
        /// </summary>
        public void UnregisterComp(string compId)
        {
            if (registeredCompProperties.ContainsKey(compId))
            {
                registeredCompProperties.Remove(compId);
                compEnabledStates.Remove(compId);
                
                if (debugMode)
                {
                    Log.Message($"[DaysAfterStartManager] Unregistered comp: {compId}");
                }
            }
        }
        
        /// <summary>
        /// 检查是否可以触发指定的事件
        /// </summary>
        public bool CanTriggerIncident(string compId, IncidentDef incident, bool isRepeatable, int repeatIntervalDays)
        {
            if (string.IsNullOrEmpty(compId) || incident == null)
                return false;
            
            // 检查是否启用
            if (!IsCompEnabled(compId))
            {
                if (debugMode)
                {
                    Log.Message($"[DaysAfterStartManager] Comp {compId} is disabled");
                }
                return false;
            }
            
            // 获取触发记录
            TriggerRecord record = GetTriggerRecord(compId);
            
            // 对于非重复事件，如果已经触发过则不能再次触发
            if (!isRepeatable && record.HasTriggered)
            {
                return false;
            }
            
            // 对于重复事件，检查间隔
            if (isRepeatable && record.HasTriggered)
            {
                if (repeatIntervalDays > 0)
                {
                    int repeatIntervalTicks = repeatIntervalDays * 60000;
                    if (Find.TickManager.TicksGame - record.LastTriggerTick < repeatIntervalTicks)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 记录事件触发
        /// </summary>
        public void RecordTrigger(string compId, IncidentDef incident)
        {
            if (string.IsNullOrEmpty(compId) || incident == null)
                return;
            
            TriggerRecord record = GetTriggerRecord(compId);
            record.IncidentDef = incident;
            record.LastTriggerTick = Find.TickManager.TicksGame;
            record.TriggerCount++;
            record.HasTriggered = true;
            
            if (debugMode)
            {
                Log.Message($"[DaysAfterStartManager] Recorded trigger for {compId}, Incident: {incident.defName}, Count: {record.TriggerCount}");
            }
            
            triggerRecords[compId] = record;
        }
        
        /// <summary>
        /// 获取触发记录
        /// </summary>
        public TriggerRecord GetTriggerRecord(string compId)
        {
            if (!triggerRecords.ContainsKey(compId))
            {
                triggerRecords[compId] = new TriggerRecord();
            }
            return triggerRecords[compId];
        }
        
        /// <summary>
        /// 获取所有触发记录
        /// </summary>
        public Dictionary<string, TriggerRecord> GetAllTriggerRecords()
        {
            return new Dictionary<string, TriggerRecord>(triggerRecords);
        }
        
        /// <summary>
        /// 检查组件是否启用
        /// </summary>
        public bool IsCompEnabled(string compId)
        {
            return compEnabledStates.ContainsKey(compId) && compEnabledStates[compId];
        }
        
        /// <summary>
        /// 启用或禁用组件
        /// </summary>
        public void SetCompEnabled(string compId, bool enabled)
        {
            if (compEnabledStates.ContainsKey(compId))
            {
                compEnabledStates[compId] = enabled;
                
                if (debugMode)
                {
                    Log.Message($"[DaysAfterStartManager] Comp {compId} {(enabled ? "enabled" : "disabled")}");
                }
            }
        }
        
        /// <summary>
        /// 获取延迟触发到指定时间
        /// </summary>
        public void ScheduleTrigger(string compId, IncidentDef incident, int delayTicks, IIncidentTarget target)
        {
            if (delayTicks <= 0 || target == null)
                return;
            
            var scheduledTrigger = new ScheduledTrigger
            {
                CompId = compId,
                Incident = incident,
                TriggerTick = Find.TickManager.TicksGame + delayTicks,
                Target = target
            };
            
            scheduledTriggers.Add(scheduledTrigger);
            
            if (debugMode)
            {
                Log.Message($"[DaysAfterStartManager] Scheduled trigger for {compId}, Incident: {incident.defName}, in {delayTicks} ticks");
            }
        }
        
        /// <summary>
        /// 处理延迟触发
        /// </summary>
        private void ProcessScheduledTriggers()
        {
            if (scheduledTriggers.Count == 0)
                return;
            
            int currentTick = Find.TickManager.TicksGame;
            var triggersToExecute = scheduledTriggers.Where(t => t.TriggerTick <= currentTick).ToList();
            
            foreach (var trigger in triggersToExecute)
            {
                ExecuteScheduledTrigger(trigger);
                scheduledTriggers.Remove(trigger);
            }
        }
        
        /// <summary>
        /// 执行延迟触发
        /// </summary>
        private void ExecuteScheduledTrigger(ScheduledTrigger trigger)
        {
            try
            {
                if (!registeredCompProperties.ContainsKey(trigger.CompId))
                {
                    Log.Warning($"[DaysAfterStartManager] Scheduled trigger for unknown comp: {trigger.CompId}");
                    return;
                }
                
                var props = registeredCompProperties[trigger.CompId];
                if (props.incident == null || trigger.Incident == null)
                    return;
                
                // 创建事件参数
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(props.incident.category, trigger.Target);
                
                // 执行事件
                if (props.incident.Worker.TryExecute(parms))
                {
                    trigger.Target.StoryState.Notify_IncidentFired(new FiringIncident(props.incident, null, parms));
                    
                    if (debugMode)
                    {
                        Log.Message($"[DaysAfterStartManager] Executed scheduled trigger for {trigger.CompId}, Incident: {trigger.Incident.defName}");
                    }
                }
                else
                {
                    Log.Warning($"[DaysAfterStartManager] Failed to execute scheduled trigger for {trigger.CompId}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DaysAfterStartManager] Error executing scheduled trigger: {ex}");
            }
        }
        
        /// <summary>
        /// 清理无效记录
        /// </summary>
        private void CleanupInvalidRecords()
        {
            // 移除没有对应注册组件的记录
            var invalidKeys = triggerRecords.Keys.Where(key => !registeredCompProperties.ContainsKey(key)).ToList();
            
            foreach (var key in invalidKeys)
            {
                triggerRecords.Remove(key);
            }
            
            if (invalidKeys.Count > 0 && debugMode)
            {
                Log.Message($"[DaysAfterStartManager] Cleaned up {invalidKeys.Count} invalid trigger records");
            }
        }
        
        /// <summary>
        /// 重置指定组件的触发记录
        /// </summary>
        public void ResetTriggerRecord(string compId)
        {
            if (triggerRecords.ContainsKey(compId))
            {
                triggerRecords.Remove(compId);
                
                if (debugMode)
                {
                    Log.Message($"[DaysAfterStartManager] Reset trigger record for {compId}");
                }
            }
        }
        
        /// <summary>
        /// 重置所有触发记录
        /// </summary>
        public void ResetAllTriggerRecords()
        {
            int count = triggerRecords.Count;
            triggerRecords.Clear();
            
            Log.Message($"[DaysAfterStartManager] Reset all {count} trigger records");
        }
        
        /// <summary>
        /// 获取管理器状态信息
        /// </summary>
        public string GetManagerStatus()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("=== DaysAfterStart Manager Status ===");
            sb.AppendLine($"Registered Components: {registeredCompProperties.Count}");
            sb.AppendLine($"Trigger Records: {triggerRecords.Count}");
            sb.AppendLine($"Enabled Components: {compEnabledStates.Count(kv => kv.Value)}");
            sb.AppendLine($"Scheduled Triggers: {scheduledTriggers.Count}");
            sb.AppendLine($"Debug Mode: {debugMode}");
            
            // 显示所有注册组件
            if (registeredCompProperties.Count > 0)
            {
                sb.AppendLine("\n=== Registered Components ===");
                foreach (var kvp in registeredCompProperties)
                {
                    var record = GetTriggerRecord(kvp.Key);
                    bool enabled = IsCompEnabled(kvp.Key);
                    
                    sb.AppendLine($"[{kvp.Key}]");
                    sb.AppendLine($"  Incident: {kvp.Value.incident?.defName ?? "null"}");
                    sb.AppendLine($"  Days: {kvp.Value.daysAfterStart}");
                    sb.AppendLine($"  Repeatable: {kvp.Value.repeatable}");
                    sb.AppendLine($"  Enabled: {enabled}");
                    sb.AppendLine($"  Triggered: {record.HasTriggered}");
                    sb.AppendLine($"  Count: {record.TriggerCount}");
                    sb.AppendLine($"  Last Trigger: {record.LastTriggerTick} ticks ago");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 记录管理器状态到日志
        /// </summary>
        public void LogManagerStatus()
        {
            Log.Message(GetManagerStatus());
        }
        
        /// <summary>
        /// 切换调试模式
        /// </summary>
        public void ToggleDebugMode()
        {
            debugMode = !debugMode;
            Log.Message($"[DaysAfterStartManager] Debug mode {(debugMode ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// 强制触发指定组件的事件
        /// </summary>
        public void ForceTrigger(string compId, IIncidentTarget target)
        {
            if (!registeredCompProperties.ContainsKey(compId) || target == null)
            {
                Log.Warning($"[DaysAfterStartManager] Cannot force trigger: comp {compId} not found or target null");
                return;
            }
            
            var props = registeredCompProperties[compId];
            if (props.incident == null)
                return;
            
            // 创建事件参数
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(props.incident.category, target);
            
            // 执行事件
            if (props.incident.Worker.TryExecute(parms))
            {
                target.StoryState.Notify_IncidentFired(new FiringIncident(props.incident, null, parms));
                
                // 记录触发
                RecordTrigger(compId, props.incident);
                
                Log.Message($"[DaysAfterStartManager] Force triggered {compId}, Incident: {props.incident.defName}");
            }
            else
            {
                Log.Warning($"[DaysAfterStartManager] Force trigger failed for {compId}");
            }
        }
        
        /// <summary>
        /// 获取组件的唯一ID
        /// </summary>
        public static string GenerateCompId(StorytellerCompProperties_DaysAfterStart props)
        {
            if (props == null)
                return "unknown";
            
            // 使用事件名称和天数生成唯一ID
            string incidentName = props.incident?.defName ?? "unknown_incident";
            return $"{incidentName}_{props.daysAfterStart}_{props.repeatable}_{props.repeatIntervalDays}";
        }
    }
    
    /// <summary>
    /// 触发记录类
    /// </summary>
    public class TriggerRecord : IExposable
    {
        // 使用字段而不是属性，以便与Scribe_Defs.Look配合
        public IncidentDef incidentDef;
        public int lastTriggerTick;
        public int triggerCount;
        public bool hasTriggered;
        
        public IncidentDef IncidentDef
        {
            get => incidentDef;
            set => incidentDef = value;
        }
        
        public int LastTriggerTick
        {
            get => lastTriggerTick;
            set => lastTriggerTick = value;
        }
        
        public int TriggerCount
        {
            get => triggerCount;
            set => triggerCount = value;
        }
        
        public bool HasTriggered
        {
            get => hasTriggered;
            set => hasTriggered = value;
        }
        
        public TriggerRecord()
        {
            incidentDef = null;
            lastTriggerTick = 0;
            triggerCount = 0;
            hasTriggered = false;
        }
        
        public void ExposeData()
        {
            // 直接使用字段进行序列化
            Scribe_Defs.Look(ref incidentDef, "incidentDef");
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", 0);
            Scribe_Values.Look(ref triggerCount, "triggerCount", 0);
            Scribe_Values.Look(ref hasTriggered, "hasTriggered", false);
        }
    }
    
    /// <summary>
    /// 延迟触发类
    /// </summary>
    public class ScheduledTrigger : IExposable
    {
        // 使用字段而不是属性
        public string compId;
        public IncidentDef incident;
        public int triggerTick;
        // 注意：IIncidentTarget 不能直接序列化，我们只存储临时数据
        
        public string CompId
        {
            get => compId;
            set => compId = value;
        }
        
        public IncidentDef Incident
        {
            get => incident;
            set => incident = value;
        }
        
        public int TriggerTick
        {
            get => triggerTick;
            set => triggerTick = value;
        }
        
        public IIncidentTarget Target { get; set; }
        
        public void ExposeData()
        {
            // 直接使用字段进行序列化
            Scribe_Values.Look(ref compId, "compId");
            Scribe_Defs.Look(ref incident, "incident");
            Scribe_Values.Look(ref triggerTick, "triggerTick", 0);
            // 注意：IIncidentTarget 不能直接序列化，我们只存储临时数据
        }
    }
}
