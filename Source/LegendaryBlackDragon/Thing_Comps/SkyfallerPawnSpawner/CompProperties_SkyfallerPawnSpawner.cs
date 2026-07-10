using Verse;
using RimWorld;
using System.Linq;

namespace LegendaryBlackDragon
{
    public class CompProperties_SkyfallerPawnSpawner : CompProperties
    {
        public PawnKindDef pawnKind;
        public FactionDef faction;
        public bool spawnDrafted = false;
        public bool spawnHostile = false;
        public bool preventDuplicatePawnKind = false;

        public CompProperties_SkyfallerPawnSpawner()
        {
            compClass = typeof(CompSkyfallerPawnSpawner);
        }
    }

    public class CompSkyfallerPawnSpawner : ThingComp
    {
        public CompProperties_SkyfallerPawnSpawner Props => (CompProperties_SkyfallerPawnSpawner)props;

        public void SpawnPawn(Map map, IntVec3 position)
        {
            if (Props.pawnKind == null)
            {
                return;
            }

            if (Props.preventDuplicatePawnKind &&
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Any(pawn => pawn.kindDef == Props.pawnKind))
            {
                return;
            }

            // 创建 Pawn
            PawnGenerationRequest request = new PawnGenerationRequest(
                Props.pawnKind,
                faction: GetFaction(),
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: true
            );

            Pawn pawn = PawnGenerator.GeneratePawn(request);

            // 设置阵营关系
            if (Props.spawnHostile)
            {
                pawn.SetFaction(Faction.OfAncientsHostile);
            }

            // 生成 Pawn
            GenSpawn.Spawn(pawn, position, map);

            // 如果需要，设置为征召状态
            if (Props.spawnDrafted && pawn.drafter != null)
            {
                pawn.drafter.Drafted = true;
            }
        }

        private Faction GetFaction()
        {
            if (Props.faction != null)
            {
                return FactionUtility.DefaultFactionFrom(Props.faction);
            }
            
            if (Props.spawnHostile)
            {
                return Faction.OfAncientsHostile;
            }
            
            return Faction.OfAncients;
        }
    }
}
