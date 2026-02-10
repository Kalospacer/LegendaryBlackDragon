using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine; 


namespace LegendaryBlackDragon
{
    public class CompProperties_OutfitPreset : CompProperties
    {
        // 预设的服装/装备方案列表
        public List<OutfitPreset> availablePresets = new List<OutfitPreset>();
        
        // 默认选择的预设索引
        public int defaultPresetIndex = 0;
        
        // Gizmo图标路径
        public string gizmoIconPath = "UI/Commands/Default";
        
        public CompProperties_OutfitPreset()
        {
            compClass = typeof(CompOutfitPreset);
        }
    }
    
    public class OutfitPreset : IExposable
    {
        public string label = "未命名方案";
        public string description = "";
        
        // 装备列表（武器、工具等）
        public List<ThingDef> equipmentDefs = new List<ThingDef>();
        
        // 服装列表（衣服、护甲等）
        public List<ThingDef> apparelDefs = new List<ThingDef>();
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label", "未命名方案");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Collections.Look(ref equipmentDefs, "equipmentDefs", LookMode.Def);
            Scribe_Collections.Look(ref apparelDefs, "apparelDefs", LookMode.Def);
        }
    }
    
    public class CompOutfitPreset : ThingComp
    {
        public CompProperties_OutfitPreset Props => (CompProperties_OutfitPreset)props;
        
        // 当前选择的预设索引
        private int currentPresetIndex = -1;
        
        public Pawn Pawn => parent as Pawn;
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            
            // 初始化当前选择
            if (currentPresetIndex == -1 && Props.availablePresets.Count > 0)
            {
                currentPresetIndex = Props.defaultPresetIndex;
                if (currentPresetIndex >= Props.availablePresets.Count)
                    currentPresetIndex = 0;
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref currentPresetIndex, "currentPresetIndex", -1);
            
            // 加载后验证索引有效性
            if (Scribe.mode == LoadSaveMode.PostLoadInit && currentPresetIndex >= Props.availablePresets.Count)
            {
                currentPresetIndex = 0;
            }
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 只有玩家派系的pawn才显示Gizmo，并且有多个预设可选
            if (Pawn?.Faction == Faction.OfPlayer && Props.availablePresets.Count > 1)
            {
                yield return new Command_Action
                {
                    defaultLabel = "切换服装/装备",
                    defaultDesc = "在预设的服装和装备方案之间切换",
                    icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false) ?? BaseContent.BadTex,
                    action = () => ShowPresetSelectionMenu(),
                    hotKey = KeyBindingDefOf.Misc2
                };
            }
        }
        
        private void ShowPresetSelectionMenu()
        {
            if (Pawn == null)
                return;
                
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            for (int i = 0; i < Props.availablePresets.Count; i++)
            {
                int index = i;
                var preset = Props.availablePresets[i];
                
                string prefix = (i == currentPresetIndex) ? "✓ " : "   ";
                string tooltip = preset.description + "\n\n包含物品:\n";
                
                bool hasItems = false;
                
                // 添加装备信息
                if (preset.equipmentDefs.Count > 0)
                {
                    hasItems = true;
                    tooltip += "装备:\n";
                    foreach (var thingDef in preset.equipmentDefs)
                    {
                        tooltip += $"  {thingDef?.LabelCap ?? "未知物品"}\n";
                    }
                    tooltip += "\n";
                }
                
                // 添加服装信息
                if (preset.apparelDefs.Count > 0)
                {
                    hasItems = true;
                    tooltip += "服装:\n";
                    foreach (var thingDef in preset.apparelDefs)
                    {
                        tooltip += $"  {thingDef?.LabelCap ?? "未知物品"}\n";
                    }
                }
                
                if (!hasItems)
                {
                    tooltip += "  (无物品)";
                }
                
                options.Add(new FloatMenuOption(
                    prefix + preset.label,
                    () => SwitchToPreset(index)
                )
                {
                    tooltip = tooltip.TrimEndNewlines()
                });
            }
            
            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        
        /// <summary>
        /// 切换到指定预设
        /// </summary>
        private void SwitchToPreset(int index)
        {
            if (index < 0 || index >= Props.availablePresets.Count || Pawn == null)
                return;
                
            try
            {
                // 1. 清除当前装备和服装
                ClearCurrentOutfit();
                
                // 2. 应用新预设
                var preset = Props.availablePresets[index];
                ApplyPresetOutfit(preset);
                
                // 3. 更新当前索引
                currentPresetIndex = index;
                
                // 4. 发送消息通知
                Messages.Message($"已切换到方案: {preset.label}", Pawn, MessageTypeDefOf.SilentInput);
            }
            catch (System.Exception ex)
            {
                Log.Error($"切换方案时出错: {ex.Message}\n{ex.StackTrace}");
                Messages.Message($"切换方案失败: {ex.Message}", Pawn, MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 清除当前所有装备和服装
        /// </summary>
        private void ClearCurrentOutfit()
        {
            // 清除装备（武器、工具等）
            if (Pawn.equipment != null)
            {
                var equipmentList = Pawn.equipment.AllEquipmentListForReading;
                foreach (var equipment in equipmentList)
                {
                    if (Pawn.equipment.TryDropEquipment(equipment, out var dropped, Pawn.PositionHeld))
                    {
                        if (dropped != null && !dropped.Destroyed)
                        {
                            dropped.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
            
            // 清除服装（衣服、护甲等）
            if (Pawn.apparel != null)
            {
                var apparelList = Pawn.apparel.WornApparel;
                foreach (var apparel in apparelList)
                {
                    if (Pawn.apparel.TryDrop(apparel, out var dropped, Pawn.PositionHeld))
                    {
                        if (dropped != null && !dropped.Destroyed)
                        {
                            dropped.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 应用预设的装备和服装
        /// </summary>
        private void ApplyPresetOutfit(OutfitPreset preset)
        {
            // 1. 应用装备（武器、工具等）
            if (Pawn.equipment != null)
            {
                foreach (var thingDef in preset.equipmentDefs)
                {
                    if (thingDef == null) continue;
                    
                    Thing thing = ThingMaker.MakeThing(thingDef);
                    if (thing is ThingWithComps thingWithComps)
                    {
                        try
                        {
                            // 如果是主要武器，需要特殊处理
                            if (thingWithComps.def.equipmentType == EquipmentType.Primary)
                            {
                                // 如果有主武器，先卸下
                                if (Pawn.equipment.Primary != null)
                                {
                                    if (Pawn.equipment.TryDropEquipment(Pawn.equipment.Primary, out var dropped, Pawn.PositionHeld))
                                    {
                                        if (dropped != null && !dropped.Destroyed)
                                        {
                                            dropped.Destroy(DestroyMode.Vanish);
                                        }
                                    }
                                }
                                
                                Pawn.equipment.AddEquipment(thingWithComps);
                            }
                            else
                            {
                                // 其他装备直接添加
                                Pawn.equipment.AddEquipment(thingWithComps);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"添加装备 {thingDef.defName} 到 {Pawn.LabelShort} 时出错: {ex.Message}");
                        }
                    }
                }
            }
            
            // 2. 应用服装（衣服、护甲等）
            if (Pawn.apparel != null)
            {
                foreach (var thingDef in preset.apparelDefs)
                {
                    if (thingDef == null) continue;
                    
                    Thing thing = ThingMaker.MakeThing(thingDef);
                    if (thing is Apparel apparel)
                    {
                        try
                        {
                            // 穿戴服装（自动处理冲突，替换不兼容的服装）
                            Pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"穿戴服装 {thingDef.defName} 到 {Pawn.LabelShort} 时出错: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Error($"预设中的物品 {thingDef.defName} 不是服装类型");
                    }
                }
            }
        }
    }
}
