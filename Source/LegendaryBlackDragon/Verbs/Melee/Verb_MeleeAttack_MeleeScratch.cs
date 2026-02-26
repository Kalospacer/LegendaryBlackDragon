using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LegendaryBlackDragon
{
    public class Verb_MeleeAttack_MeleeScratch : Verb_MeleeAttack
    {
        private const int DefaultScratchKnockbackCells = 2;
        private const int DefaultScratchStunTicks = 45;

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            return CasterPawn?.equipment?.Primary == null;
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            return Execute(this, target);
        }

        public static DamageWorker.DamageResult Execute(Verb verb, LocalTargetInfo target)
        {
            DamageWorker.DamageResult result = new DamageWorker.DamageResult();
            Pawn caster = verb.CasterPawn;
            if (caster == null || !target.HasThing)
            {
                return result;
            }

            Thing mainTarget = target.Thing;
            result = mainTarget.TakeDamage(MakeDamageInfo(verb, caster, mainTarget));
            if (mainTarget is Pawn pawn)
            {
                TryKnockbackPawn(verb, caster, pawn);
            }

            return result;
        }

        private static DamageInfo MakeDamageInfo(Verb verb, Pawn caster, Thing target)
        {
            float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb, caster);
            float armorPen = verb.verbProps.AdjustedArmorPenetration(verb, caster);
            DamageDef damageDef = verb.verbProps.meleeDamageDef ?? DamageDefOf.Blunt;

            DamageInfo dinfo = new DamageInfo(
                damageDef,
                damage,
                armorPen,
                -1f,
                caster,
                null,
                verb.EquipmentSource?.def ?? caster.def);

            dinfo.SetTool(verb.tool);
            dinfo.SetAngle((target.Position - caster.Position).ToVector3());
            return dinfo;
        }

        /// <summary>
        /// === 新增：击飞位置的提前校验 ===
        /// </summary>
        private static bool IsValidKnockbackDestination(IntVec3 destination, Map map, Pawn victim)
        {
            // 基本空值检查
            if (destination == null || map == null || victim == null)
            {
                Log.Warning("[MeleeScratch] Invalid knockback destination: null reference");
                return false;
            }

            // 检查目的地是否在游戏世界内
            if (!destination.IsValid)
            {
                Log.Warning($"[MeleeScratch] Destination {destination} is invalid");
                return false;
            }

            // 检查目的地是否在地图范围内
            if (!destination.InBounds(map))
            {
                Log.Warning($"[MeleeScratch] Destination {destination} is out of map bounds");
                return false;
            }

            // 检查目的地是否可站立
            if (!destination.Standable(map))
            {
                Log.Warning($"[MeleeScratch] Destination {destination} is not standable");
                return false;
            }

            // 检查目的地是否有其他pawn（避免重叠）
            Pawn existingPawn = destination.GetFirstPawn(map);
            if (existingPawn != null && existingPawn != victim)
            {
                Log.Warning($"[MeleeScratch] Destination {destination} already occupied by {existingPawn.Label}");
                return false;
            }

            // 检查目的地是否有不可穿过的建筑或障碍物
            Building building = destination.GetEdifice(map);
            if (building != null && building.def.passability == Traversability.Impassable)
            {
                Log.Warning($"[MeleeScratch] Destination {destination} has impassable building: {building.Label}");
                return false;
            }

            // 检查目的地是否在水体或其他危险地形中
            TerrainDef terrain = destination.GetTerrain(map);
            if (terrain != null && terrain.IsWater)
            {
                Log.Warning($"[MeleeScratch] Destination {destination} is in water terrain: {terrain.defName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// === 新增：安全创建 PawnFlyer ===
        /// </summary>
        private static bool TryCreateAndSpawnPawnFlyer(ThingDef flyerDef, Pawn victim, IntVec3 destination, 
            Map map, EffecterDef flightEffecter, SoundDef landingSound)
        {
            try
            {
                // 最终校验：在创建PawnFlyer前再次检查目的地
                if (!IsValidKnockbackDestination(destination, map, victim))
                {
                    Log.Error($"[MeleeScratch] Final destination validation failed for {victim.Label} at {destination}");
                    return false;
                }

                // 创建PawnFlyer
                PawnFlyer flyer = PawnFlyer.MakeFlyer(
                    flyerDef,
                    victim,
                    destination,
                    flightEffecter,
                    landingSound,
                    flyWithCarriedThing: false,
                    overrideStartVec: null,
                    triggeringAbility: null,
                    target: new LocalTargetInfo(destination));

                if (flyer == null)
                {
                    Log.Error($"[MeleeScratch] Failed to create PawnFlyer for {victim.Label}");
                    return false;
                }

                // 检查PawnFlyer是否有效
                if (flyer.DestinationPos == null)
                {
                    Log.Error($"[MeleeScratch] Created PawnFlyer has invalid destination: {flyer.DestinationPos}");
                    flyer.Destroy(DestroyMode.Vanish);
                    return false;
                }

                // 生成PawnFlyer
                GenSpawn.Spawn(flyer, destination, map);
                Log.Message($"[MeleeScratch] Successfully created and spawned PawnFlyer for {victim.Label} to {destination}");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[MeleeScratch] Exception creating PawnFlyer for {victim?.Label ?? "null"}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// === 修改后的击退方法，添加提前校验 ===
        /// </summary>
        private static void TryKnockbackPawn(Verb verb, Pawn caster, Pawn victim)
        {
            // 基础检查
            if (caster.MapHeld == null || victim.MapHeld != caster.MapHeld || victim.Dead || !victim.Spawned)
            {
                Log.Warning($"[MeleeScratch] Knockback pre-check failed for {victim.Label}");
                return;
            }

            VerbProperties_MeleeMode props = verb.verbProps as VerbProperties_MeleeMode;
            int knockbackCells = Mathf.Max(0, props?.scratchKnockbackCells ?? DefaultScratchKnockbackCells);
            int stunTicks = Mathf.Max(0, props?.scratchStunTicks ?? DefaultScratchStunTicks);
            ThingDef flyerDef = props?.scratchKnockbackFlyerDef ?? ThingDefOf.PawnFlyer;
            SoundDef landingSound = props?.scratchLandingSound ?? DefDatabase<SoundDef>.GetNamedSilentFail("Longjump_Land");
            EffecterDef flightEffecter = props?.scratchFlightEffecterDef;

            IntVec3 from = caster.Position;
            IntVec3 victimPos = victim.Position;
            int dx = Mathf.Clamp(victimPos.x - from.x, -1, 1);
            int dz = Mathf.Clamp(victimPos.z - from.z, -1, 1);
            
            // 如果攻击者和受害者在同一位置，不执行击退
            if (dx == 0 && dz == 0)
            {
                Log.Warning($"[MeleeScratch] Caster and victim at same position: {from}");
                return;
            }

            IntVec3 step = new IntVec3(dx, 0, dz);
            IntVec3 destination = victimPos;
            
            // 寻找有效的击退位置
            bool foundValidDestination = false;
            
            for (int i = 1; i <= knockbackCells; i++)
            {
                IntVec3 cell = victimPos + step * i;
                
                // === 新增：每一步都进行严格的校验 ===
                if (!IsValidKnockbackDestination(cell, caster.MapHeld, victim))
                {
                    // 当前位置无效，停止继续寻找
                    break;
                }
                
                destination = cell;
                foundValidDestination = true;
                
                // 可选：如果找到有效位置，可以继续寻找更远的有效位置
                // 或者直接使用第一个有效位置（这里我们选择第一个有效位置）
                if (foundValidDestination)
                {
                    break;
                }
            }

            // 如果找到有效目的地，创建PawnFlyer
            if (foundValidDestination && destination != victimPos)
            {
                Log.Message($"[MeleeScratch] Found valid knockback destination for {victim.Label}: {destination}");
                
                bool flyerCreated = TryCreateAndSpawnPawnFlyer(flyerDef, victim, destination, 
                    caster.MapHeld, flightEffecter, landingSound);
                
                if (!flyerCreated)
                {
                    Log.Warning($"[MeleeScratch] Failed to create PawnFlyer for {victim.Label}, applying stun only");
                    // 如果击退失败，仍然应用击晕效果
                }
            }
            else
            {
                Log.Warning($"[MeleeScratch] No valid knockback destination found for {victim.Label}");
                // 不执行击退，只执行击晕（如果有）
            }

            // 应用击晕效果
            if (stunTicks > 0)
            {
                if (victim.stances?.stunner != null)
                {
                    victim.stances.stunner.StunFor(stunTicks, caster, addBattleLog: false, showMote: false);
                    Log.Message($"[MeleeScratch] Applied {stunTicks} ticks stun to {victim.Label}");
                }
                else
                {
                    Log.Warning($"[MeleeScratch] Cannot apply stun to {victim.Label}: stunner component not found");
                }
            }
        }
    }
}
