using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class CompProperties_OutfitPreset : CompProperties
    {
        // 预设的服装/装备方案列表
        public List<OutfitPreset> availablePresets = new List<OutfitPreset>();

        // 默认选择的预设索引
        public int defaultPresetIndex = 0;
        public bool showStatusInGizmo = false;

        // Gizmo图标路径
        public string gizmoIconPath = "LegendaryBlackDragon/UI/Commands/LBD_OutfitPreset";

        public CompProperties_OutfitPreset()
        {
            compClass = typeof(CompOutfitPreset);
        }
    }

    public class OutfitPreset : IExposable
    {
        public string label;
        public string description = "";

        // 装备列表（武器、工具等）
        public List<ThingDef> equipmentDefs = new List<ThingDef>();

        // 服装列表（衣服、护甲等）
        public List<ThingDef> apparelDefs = new List<ThingDef>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label", "LBD_UnnamedPreset");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Collections.Look(ref equipmentDefs, "equipmentDefs", LookMode.Def);
            Scribe_Collections.Look(ref apparelDefs, "apparelDefs", LookMode.Def);
        }
    }

    public class CompOutfitPreset : ThingComp
    {
        public CompProperties_OutfitPreset Props => (CompProperties_OutfitPreset)props;
        
        private int currentPresetIndex = -1;
        
        public Pawn Pawn => parent as Pawn;
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            
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
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit && currentPresetIndex >= Props.availablePresets.Count)
            {
                currentPresetIndex = 0;
            }
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Pawn?.Faction == Faction.OfPlayer && Props.availablePresets.Count > 1)
            {
                var command = new Command_Action
                {
                    defaultLabel = Props.showStatusInGizmo && currentPresetIndex >= 0 && currentPresetIndex < Props.availablePresets.Count
                        ? (Props.availablePresets[currentPresetIndex].label ?? "LBD_UnnamedPreset").Translate()
                        : "LBD_SwitchPresets".Translate(),
                    defaultDesc = "LBD_SwitchPresetsDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false) ?? BaseContent.BadTex,
                    action = () => ShowPresetSelectionMenu(),
                    hotKey = KeyBindingDefOf.Misc2
                };

                if (Pawn.Downed)
                {
                    command.Disable("AbilityCantApplyDowned".Translate(Pawn));
                }

                yield return command;
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
                
                options.Add(new FloatMenuOption(
                    prefix + (preset.label ?? "LBD_UnnamedPreset").Translate(),
                    () => SimpleSwitchToPreset(index)
                )
                {
                    tooltip = (preset.description ?? "LBD_UnnamedPresetDesc").Translate()
                });
            }
            
            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        
        /// <summary>
        /// 简单的切换方法 - 只处理服装
        /// </summary>
        private void SimpleSwitchToPreset(int index)
        {
            if (index < 0 || index >= Props.availablePresets.Count || Pawn == null || Pawn.apparel == null)
                return;
                
            try
            {
                // 1. 清除当前所有服装
                ClearAllApparel();
                
                // 2. 穿戴新服装
                var preset = Props.availablePresets[index];
                WearPresetApparel(preset);
                
                // 3. 更新当前索引
                currentPresetIndex = index;
            }
            catch (System.Exception ex)
            {
                Log.Error($"切换方案时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ClearAllApparel()
        {
            if (Pawn.apparel == null)
                return;

            // 定义要清除的服装列表
            List<string> targetApparelDefNames = new List<string>
            {
                "LBD_Miraboreasu_Armor",
                "LBD_Miraboreasu_Base_Cloth"
            };

            // 使用临时列表存储要清除的服装
            List<Apparel> apparelToRemove = new List<Apparel>();

            // 收集特定服装
            foreach (var apparel in Pawn.apparel.WornApparel)
            {
                if (apparel != null && targetApparelDefNames.Contains(apparel.def.defName))
                {
                    apparelToRemove.Add(apparel);
                }
            }

            // 如果没有找到目标服装，直接返回
            if (apparelToRemove.Count == 0)
            {
                return;
            }

            // 清除服装（从后往前清除，避免索引问题）
            int removedCount = 0;
            for (int i = apparelToRemove.Count - 1; i >= 0; i--)
            {
                var apparel = apparelToRemove[i];
                try
                {
                    // 移除服装
                    if (Pawn.apparel.WornApparel.Contains(apparel))
                    {
                        Pawn.apparel.Remove(apparel);
                        removedCount++;

                        // 销毁服装
                        if (!apparel.Destroyed)
                        {
                            apparel.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"清除服装 {apparel?.LabelCap ?? "未知"} 时出错: {ex.Message}");
                }
            }

        }

        /// <summary>
        /// 穿戴预设服装
        /// </summary>
        private void WearPresetApparel(OutfitPreset preset)
        {
            if (Pawn.apparel == null)
                return;
                
            foreach (var thingDef in preset.apparelDefs)
            {
                if (thingDef == null) continue;
                
                try
                {
                    // 生成服装
                    Thing thing = ThingMaker.MakeThing(thingDef);
                    if (thing is Apparel apparel)
                    {
                        // 直接穿戴，让RimWorld处理冲突
                        Pawn.apparel.Wear(apparel, dropReplacedApparel: true, locked : true);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"穿戴服装 {thingDef.defName} 时出错: {ex.Message}");
                }
            }
        }
    }
}
