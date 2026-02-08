using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class LBD_DragonSkyNukeStrikeController : ThingWithComps
    {
        private enum StrikeStage
        {
            None,
            Delay,
            Phase1,
            Phase2Preload,
            Phase2,
            Finished
        }

        private Pawn caster;
        private bool casterWasDrafted;
        private bool casterWasDespawned;
        private bool casterReturned;
        private IntVec3 casterReturnCell = IntVec3.Invalid;

        private StrikeStage stage;
        private int initTick = -1;
        private int startTick = -1;
        private int stageStartTick = -1;

        private int phase1DelayTicks = 240;
        private int phase1DurationTicks = 300;
        private int phase1WaveIntervalTicks = 30;
        private int nextPhase1WaveTick;

        private int phase2IntervalTicks = 60;
        private int phase2PulseCount = 20;
        private int phase2PulsesDone;
        private int nextPhase2PulseTick;
        private int phase2IgniteCellsPerPulse = -1;
        private float phase2IgniteFireSize = 0.16f;
        private int phase2IgniteCursor;
        private float phase2ExplosionRadius = 1.9f;
        private int phase2ExplosionDamage = 0;
        private float phase2ExplosionChanceToStartFire = 0.35f;
        private int phase2ExplosionVisualsPerPulse = 24;
        private bool phase2PreloadQueued;
        private bool phase2PreloadDone;

        private int damageAmount = 16;
        private float armorPenetration;
        private DamageDef damageDef;

        private EffecterDef phase1ItemEffecterDef;
        private int phase1EffecterMaintainTicks = 20;
        private float phase1IgniteFireSize = 0.2f;

        private int incomingWaveCells = 10;
        private int incomingWaveWidth = 2;
        private IntVec3 fireEntryCell = IntVec3.Invalid;

        private int skyFadeInTicks = 20;
        private int skyFadeOutTicks = 40;
        private const string Phase2PreloadLongEventTextKey = "LBD_DragonSkyNuke_Preloading";

        private List<Thing> phase1Targets = new List<Thing>();
        private List<Thing> phase2Targets = new List<Thing>();
        private List<IntVec3> phase2OutdoorCells = new List<IntVec3>();

        public Pawn Caster => caster;

        public bool Finished => stage == StrikeStage.Finished;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref casterWasDrafted, "casterWasDrafted", false);
            Scribe_Values.Look(ref casterWasDespawned, "casterWasDespawned", false);
            Scribe_Values.Look(ref casterReturned, "casterReturned", false);
            Scribe_Values.Look(ref casterReturnCell, "casterReturnCell", IntVec3.Invalid);

            Scribe_Values.Look(ref stage, "stage", StrikeStage.None);
            Scribe_Values.Look(ref initTick, "initTick", -1);
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref stageStartTick, "stageStartTick", -1);

            Scribe_Values.Look(ref phase1DelayTicks, "phase1DelayTicks", 240);
            Scribe_Values.Look(ref phase1DurationTicks, "phase1DurationTicks", 300);
            Scribe_Values.Look(ref phase1WaveIntervalTicks, "phase1WaveIntervalTicks", 30);
            Scribe_Values.Look(ref nextPhase1WaveTick, "nextPhase1WaveTick", 0);

            Scribe_Values.Look(ref phase2IntervalTicks, "phase2IntervalTicks", 60);
            Scribe_Values.Look(ref phase2PulseCount, "phase2PulseCount", 20);
            Scribe_Values.Look(ref phase2PulsesDone, "phase2PulsesDone", 0);
            Scribe_Values.Look(ref nextPhase2PulseTick, "nextPhase2PulseTick", 0);
            Scribe_Values.Look(ref phase2IgniteCellsPerPulse, "phase2IgniteCellsPerPulse", -1);
            Scribe_Values.Look(ref phase2IgniteFireSize, "phase2IgniteFireSize", 0.16f);
            Scribe_Values.Look(ref phase2IgniteCursor, "phase2IgniteCursor", 0);
            Scribe_Values.Look(ref phase2ExplosionRadius, "phase2ExplosionRadius", 1.9f);
            Scribe_Values.Look(ref phase2ExplosionDamage, "phase2ExplosionDamage", 0);
            Scribe_Values.Look(ref phase2ExplosionChanceToStartFire, "phase2ExplosionChanceToStartFire", 0.35f);
            Scribe_Values.Look(ref phase2ExplosionVisualsPerPulse, "phase2ExplosionVisualsPerPulse", 24);
            Scribe_Values.Look(ref phase2PreloadQueued, "phase2PreloadQueued", false);
            Scribe_Values.Look(ref phase2PreloadDone, "phase2PreloadDone", false);

            Scribe_Values.Look(ref damageAmount, "damageAmount", 16);
            Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0f);
            Scribe_Defs.Look(ref damageDef, "damageDef");

            Scribe_Defs.Look(ref phase1ItemEffecterDef, "phase1ItemEffecterDef");
            Scribe_Values.Look(ref phase1EffecterMaintainTicks, "phase1EffecterMaintainTicks", 20);
            Scribe_Values.Look(ref phase1IgniteFireSize, "phase1IgniteFireSize", 0.2f);

            Scribe_Values.Look(ref incomingWaveCells, "incomingWaveCells", 10);
            Scribe_Values.Look(ref incomingWaveWidth, "incomingWaveWidth", 2);
            Scribe_Values.Look(ref fireEntryCell, "fireEntryCell", IntVec3.Invalid);

            Scribe_Values.Look(ref skyFadeInTicks, "skyFadeInTicks", 20);
            Scribe_Values.Look(ref skyFadeOutTicks, "skyFadeOutTicks", 40);

            Scribe_Collections.Look(ref phase1Targets, "phase1Targets", LookMode.Reference);
            Scribe_Collections.Look(ref phase2Targets, "phase2Targets", LookMode.Reference);
            Scribe_Collections.Look(ref phase2OutdoorCells, "phase2OutdoorCells", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                phase1Targets ??= new List<Thing>();
                phase2Targets ??= new List<Thing>();
                phase2OutdoorCells ??= new List<IntVec3>();
            }
        }

        public void Initialize(Pawn caster, CompProperties_AbilityDragonSkyNuke props)
        {
            this.caster = caster;
            casterWasDrafted = caster?.drafter?.Drafted == true;
            casterReturnCell = caster?.Position ?? Position;

            phase1DelayTicks = props.phase1DelayTicksRange.RandomInRange;
            phase1DurationTicks = Mathf.Max(1, props.phase1DurationTicks);
            phase1WaveIntervalTicks = Mathf.Max(1, props.phase1WaveIntervalTicks);

            phase2IntervalTicks = Mathf.Max(1, props.phase2IntervalTicks);
            phase2PulseCount = Mathf.Max(1, props.phase2PulseCount);
            phase2IgniteCellsPerPulse = props.phase2IgniteCellsPerPulse;
            phase2IgniteFireSize = Mathf.Max(0.01f, props.phase2IgniteFireSize);
            phase2ExplosionRadius = Mathf.Max(0.1f, props.phase2ExplosionRadius);
            phase2ExplosionDamage = Mathf.Max(0, props.phase2ExplosionDamage);
            phase2ExplosionChanceToStartFire = Mathf.Clamp01(props.phase2ExplosionChanceToStartFire);
            phase2ExplosionVisualsPerPulse = Mathf.Max(0, props.phase2ExplosionVisualsPerPulse);

            damageDef = props.damageDef ?? DamageDefOf.Burn;
            damageAmount = Mathf.Max(1, props.damageAmount);
            armorPenetration = props.armorPenetration;

            phase1ItemEffecterDef = props.phase1ItemEffecterDef;
            phase1EffecterMaintainTicks = Mathf.Max(1, props.phase1EffecterMaintainTicks);
            phase1IgniteFireSize = Mathf.Max(0.01f, props.phase1IgniteFireSize);

            incomingWaveCells = Mathf.Max(1, props.incomingWaveCells);
            incomingWaveWidth = Mathf.Max(0, props.incomingWaveWidth);

            skyFadeInTicks = Mathf.Max(0, props.skyFadeInTicks);
            skyFadeOutTicks = Mathf.Max(0, props.skyFadeOutTicks);

            initTick = Find.TickManager.TicksGame;
            stage = StrikeStage.Delay;
            fireEntryCell = RandomEdgeCell(Map);
            phase2IgniteCursor = 0;

            SendCasterOffMap();
        }

        protected override void Tick()
        {
            base.Tick();

            if (Map == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            switch (stage)
            {
                case StrikeStage.Delay:
                    if (startTick < 0)
                    {
                        if (!casterWasDespawned && (caster == null || !caster.Spawned))
                        {
                            casterWasDespawned = true;
                        }

                        if (!casterWasDespawned && currentTick >= initTick + 120 && caster != null && caster.Spawned && caster.Map == Map)
                        {
                            caster.DeSpawn();
                            casterWasDespawned = true;
                        }

                        if (caster == null || casterWasDespawned)
                        {
                            startTick = currentTick;
                            StartSkyEffect();
                        }
                        break;
                    }

                    if (currentTick >= startTick + phase1DelayTicks)
                    {
                        StartPhase1();
                    }
                    break;
                case StrikeStage.Phase1:
                    TickPhase1(currentTick);
                    break;
                case StrikeStage.Phase2Preload:
                    TickPhase2Preload();
                    break;
                case StrikeStage.Phase2:
                    TickPhase2(currentTick);
                    break;
                case StrikeStage.Finished:
                    Destroy();
                    break;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Comps_PostDraw();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            ReturnCasterToMap();
            base.Destroy(mode);
        }

        private void StartSkyEffect()
        {
            CompAffectsSky affectsSky = GetComp<CompAffectsSky>();
            if (affectsSky == null)
            {
                return;
            }

            int totalTicks = phase1DelayTicks + phase1DurationTicks + phase2IntervalTicks * phase2PulseCount;
            int holdTicks = Mathf.Max(1, totalTicks - skyFadeInTicks - skyFadeOutTicks);
            affectsSky.StartFadeInHoldFadeOut(skyFadeInTicks, holdTicks, skyFadeOutTicks);
        }

        private void SendCasterOffMap()
        {
            if (caster == null || !caster.Spawned || caster.Map != Map)
            {
                return;
            }

            IntVec3 leaveCell = caster.Position;
            caster.pather?.StopDead();
            caster.DeSpawn();
            casterWasDespawned = true;

            TrySpawnLeavingFlyer(leaveCell);
        }

        private void StartPhase1()
        {
            stage = StrikeStage.Phase1;
            stageStartTick = Find.TickManager.TicksGame;
            nextPhase1WaveTick = stageStartTick;

            CachePhase1Targets();
            TriggerPhase1ItemEffects();
            IgniteOutdoorItemsGuaranteed();
        }

        private void TickPhase1(int currentTick)
        {
            if (currentTick >= nextPhase1WaveTick)
            {
                SpawnIncomingSmallFireWave();
                nextPhase1WaveTick = currentTick + phase1WaveIntervalTicks;
            }

            if (currentTick >= stageStartTick + phase1DurationTicks)
            {
                StartPhase2();
            }
        }

        private void StartPhase2()
        {
            stage = StrikeStage.Phase2Preload;
            QueuePhase2PreloadLongEvent();
        }

        private void TickPhase2Preload()
        {
            if (!phase2PreloadDone)
            {
                if (!phase2PreloadQueued)
                {
                    QueuePhase2PreloadLongEvent();
                }
                return;
            }

            BeginPhase2();
        }

        private void BeginPhase2()
        {
            stage = StrikeStage.Phase2;
            stageStartTick = Find.TickManager.TicksGame;
            nextPhase2PulseTick = stageStartTick;
            phase2PulsesDone = 0;
        }

        private void TickPhase2(int currentTick)
        {
            if (phase2PulsesDone >= phase2PulseCount)
            {
                FinishStrike();
                return;
            }

            if (currentTick < nextPhase2PulseTick)
            {
                return;
            }

            ApplyPhase2PulseDamage();
            IgnitePhase2PulseFire();
            phase2PulsesDone++;
            nextPhase2PulseTick += phase2IntervalTicks;

            if (phase2PulsesDone >= phase2PulseCount)
            {
                FinishStrike();
            }
        }

        private void FinishStrike()
        {
            stage = StrikeStage.Finished;
            ReturnCasterToMap();
        }

        private void ReturnCasterToMap()
        {
            if (casterReturned || caster == null || Map == null)
            {
                return;
            }

            if (casterWasDespawned && !caster.Spawned)
            {
                if (!caster.Destroyed && !caster.Dead)
                {
                    IntVec3 spawnCell = ResolveReturnCell();
                    if (!TrySpawnArrivalFlyer(spawnCell) && !GenPlace.TryPlaceThing(caster, spawnCell, Map, ThingPlaceMode.Near))
                    {
                        GenSpawn.Spawn(caster, spawnCell, Map);
                    }
                    caster.Rotation = Rot4.East;
                }
            }

            if (caster.Spawned && caster.drafter != null)
            {
                caster.drafter.Drafted = casterWasDrafted;
            }

            casterReturned = true;
        }

        private IntVec3 ResolveReturnCell()
        {
            if (casterReturnCell.IsValid && casterReturnCell.InBounds(Map) && casterReturnCell.Standable(Map) && casterReturnCell.GetFirstPawn(Map) == null)
            {
                return casterReturnCell;
            }

            if (CellFinder.TryRandomClosewalkCellNear(casterReturnCell.IsValid ? casterReturnCell : Position, Map, 8, out IntVec3 found))
            {
                return found;
            }

            return Position.InBounds(Map) ? Position : CellFinder.RandomCell(Map);
        }

        private bool TrySpawnLeavingFlyer(IntVec3 leaveCell)
        {
            ThingDef leavingDef = LBD_DefOf.LBD_DragonSkyNukeFlyerLeaving;
            if (leavingDef == null || caster == null || caster.Destroyed || caster.Dead || caster.holdingOwner != null)
            {
                return false;
            }

            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(leavingDef, caster);
            if (skyfaller == null)
            {
                return false;
            }

            GenSpawn.Spawn(skyfaller, leaveCell, Map);
            bool flip = Rand.Bool;
            skyfaller.OverrideFlightFlippedHorizontal = flip;
            caster.Rotation = flip ? Rot4.West : Rot4.East;
            return true;
        }

        private bool TrySpawnArrivalFlyer(IntVec3 spawnCell)
        {
            ThingDef arrivalDef = LBD_DefOf.LBD_DragonSkyNukeFlyerArrival;
            if (arrivalDef == null || caster == null || caster.Destroyed || caster.Dead || caster.holdingOwner != null)
            {
                return false;
            }

            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(arrivalDef, caster);
            if (skyfaller == null)
            {
                return false;
            }

            GenSpawn.Spawn(skyfaller, spawnCell, Map);
            caster.Rotation = Rot4.East;
            return true;
        }

        private void CachePhase1Targets()
        {
            phase1Targets.Clear();
            List<Thing> allThings = Map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing == this)
                {
                    continue;
                }

                if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Mote || thing.def.category == ThingCategory.Ethereal)
                {
                    continue;
                }

                phase1Targets.Add(thing);
            }
        }

        private void TriggerPhase1ItemEffects()
        {
            if (phase1ItemEffecterDef == null)
            {
                return;
            }

            for (int i = 0; i < phase1Targets.Count; i++)
            {
                Thing thing = phase1Targets[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != Map)
                {
                    continue;
                }

                Effecter effecter = phase1ItemEffecterDef.Spawn(thing, Map);
                Map.effecterMaintainer.AddEffecterToMaintain(effecter, thing, phase1EffecterMaintainTicks);
            }
        }

        private void IgniteOutdoorItemsGuaranteed()
        {
            for (int i = 0; i < phase1Targets.Count; i++)
            {
                Thing thing = phase1Targets[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != Map)
                {
                    continue;
                }

                if (thing.def.category != ThingCategory.Item)
                {
                    continue;
                }

                if (!thing.FlammableNow || thing.Position.Roofed(Map))
                {
                    continue;
                }

                IgniteCellGuaranteed(thing.Position, phase1IgniteFireSize);
            }
        }

        private void SpawnIncomingSmallFireWave()
        {
            if (!fireEntryCell.IsValid || !fireEntryCell.InBounds(Map))
            {
                fireEntryCell = RandomEdgeCell(Map);
            }

            IntVec3 target = casterReturnCell.IsValid ? casterReturnCell : Position;
            if (!target.InBounds(Map))
            {
                target = Position;
            }

            Vector2 from = new Vector2(fireEntryCell.x, fireEntryCell.z);
            Vector2 to = new Vector2(target.x, target.z);
            Vector2 dir = (to - from).normalized;
            if (dir.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            int count = Mathf.Max(1, incomingWaveCells);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 1f : (float)i / (count - 1);
                Vector2 basePos = Vector2.Lerp(from, to, t);
                float lateralOffset = Rand.Range(-incomingWaveWidth, incomingWaveWidth + 1);
                Vector2 offsetPos = basePos + perpendicular * lateralOffset;

                IntVec3 cell = new IntVec3(Mathf.RoundToInt(offsetPos.x), 0, Mathf.RoundToInt(offsetPos.y));
                if (!cell.InBounds(Map) || cell.Roofed(Map))
                {
                    continue;
                }

                IgniteCellGuaranteed(cell, 0.12f);
            }
        }

        private void PreloadPhase2Targets()
        {
            phase2Targets.Clear();
            List<Thing> allThings = Map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing == this)
                {
                    continue;
                }

                if (thing.def.category == ThingCategory.Mote || thing.def.category == ThingCategory.Ethereal)
                {
                    continue;
                }

                if (!thing.Position.InBounds(Map) || thing.Position.Roofed(Map))
                {
                    continue;
                }

                phase2Targets.Add(thing);
            }
        }

        private void PreloadPhase2OutdoorCells()
        {
            phase2OutdoorCells.Clear();
            foreach (IntVec3 cell in Map.AllCells)
            {
                if (cell.Roofed(Map) || !cell.Standable(Map))
                {
                    continue;
                }

                if (FireUtility.ChanceToStartFireIn(cell, Map) <= 0f)
                {
                    continue;
                }

                phase2OutdoorCells.Add(cell);
            }

            if (phase2IgniteCellsPerPulse <= 0)
            {
                int autoIgnite = Mathf.Max(1, Mathf.CeilToInt(phase2OutdoorCells.Count / (float)Mathf.Max(1, phase2PulseCount)));
                phase2IgniteCellsPerPulse = Mathf.Min(autoIgnite, 300);
            }
            else
            {
                phase2IgniteCellsPerPulse = Mathf.Max(1, phase2IgniteCellsPerPulse);
            }

            phase2IgniteCursor = 0;
        }

        private void QueuePhase2PreloadLongEvent()
        {
            if (phase2PreloadQueued || phase2PreloadDone || Map == null)
            {
                return;
            }

            phase2PreloadQueued = true;
            LongEventHandler.QueueLongEvent(delegate
            {
                if (Destroyed || Map == null)
                {
                    return;
                }

                BuildPhase2Caches();
            }, Phase2PreloadLongEventTextKey, doAsynchronously: false, null);
        }

        private void BuildPhase2Caches()
        {
            if (phase2PreloadDone || Map == null)
            {
                return;
            }

            PreloadPhase2Targets();
            PreloadPhase2OutdoorCells();
            phase2PreloadDone = true;
        }

        private void ApplyPhase2PulseDamage()
        {
            DamageDef pulseDamageDef = damageDef ?? DamageDefOf.Burn;
            for (int i = 0; i < phase2Targets.Count; i++)
            {
                Thing thing = phase2Targets[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != Map)
                {
                    continue;
                }

                DamageInfo damageInfo = new DamageInfo(
                    pulseDamageDef,
                    damageAmount,
                    armorPenetration,
                    -1f,
                    caster);

                thing.TakeDamage(damageInfo);
            }
        }

        private void IgnitePhase2PulseFire()
        {
            if (phase2OutdoorCells == null || phase2OutdoorCells.Count == 0)
            {
                return;
            }

            int igniteCount = Mathf.Max(1, phase2IgniteCellsPerPulse);
            int explosionBudget = Mathf.Min(phase2ExplosionVisualsPerPulse, igniteCount);
            int cellCount = phase2OutdoorCells.Count;
            for (int i = 0; i < igniteCount; i++)
            {
                if (phase2IgniteCursor >= cellCount)
                {
                    phase2IgniteCursor = 0;
                }

                IntVec3 cell = phase2OutdoorCells[phase2IgniteCursor];
                phase2IgniteCursor++;
                IgniteCellGuaranteed(cell, phase2IgniteFireSize);

                if (explosionBudget > 0)
                {
                    DoPhase2ExplosionVisual(cell);
                    explosionBudget--;
                }
            }
        }

        private void IgniteCellGuaranteed(IntVec3 cell, float fireSize)
        {
            if (!cell.InBounds(Map))
            {
                return;
            }

            TrySpawnAsh(cell);

            if (cell.ContainsStaticFire(Map))
            {
                return;
            }

            if (!FireUtility.TryStartFireIn(cell, Map, fireSize, caster))
            {
                Fire fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                fire.fireSize = fireSize;
                fire.instigator = caster;
                GenSpawn.Spawn(fire, cell, Map);
            }
        }

        private void TrySpawnAsh(IntVec3 cell)
        {
            if (Rand.Chance(0.75f))
            {
                FilthMaker.TryMakeFilth(cell, Map, ThingDefOf.Filth_Ash);
            }
        }

        private void DoPhase2ExplosionVisual(IntVec3 cell)
        {
            if (!cell.InBounds(Map))
            {
                return;
            }

            DamageDef explosionDamageDef = damageDef ?? DamageDefOf.Flame;
            GenExplosion.DoExplosion(
                cell,
                Map,
                phase2ExplosionRadius,
                explosionDamageDef,
                caster,
                damAmount: phase2ExplosionDamage,
                armorPenetration: armorPenetration,
                chanceToStartFire: phase2ExplosionChanceToStartFire,
                damageFalloff: false,
                doVisualEffects: true,
                doSoundEffects: false);
        }

        private static IntVec3 RandomEdgeCell(Map map)
        {
            if (map == null)
            {
                return IntVec3.Invalid;
            }

            return CellFinder.RandomEdgeCell(map);
        }
    }
}
