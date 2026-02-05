using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    [StaticConstructorOnStartup]
    public class LBDMod : Mod
    {
        public LBDMod(ModContentPack content) : base(content)
        {
            // 初始化Harmony
            var harmony = new Harmony("tourswen.EndfieldPerlica");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
