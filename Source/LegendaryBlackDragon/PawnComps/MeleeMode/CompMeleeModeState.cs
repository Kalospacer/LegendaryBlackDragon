using Verse;

namespace LegendaryBlackDragon
{
    public class CompMeleeModeState : ThingComp
    {
        private bool scratchMode;

        public CompProperties_MeleeModeState Props => (CompProperties_MeleeModeState)props;

        public bool ScratchMode
        {
            get => scratchMode;
            set => scratchMode = value;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                scratchMode = Props.defaultScratchMode;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref scratchMode, "scratchMode", Props.defaultScratchMode);
        }
    }
}
