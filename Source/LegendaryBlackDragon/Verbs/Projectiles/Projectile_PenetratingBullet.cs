using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LegendaryBlackDragon
{
    public class PathPierce_Extension : DefModExtension
    {
        // 原有的穿透属性
        public int maxHits = 3;
        public float damageFalloff = 0.25f;
        public bool preventFriendlyFire = false;
        public FleckDef tailFleckDef;
        public int fleckDelayTicks = 10;
        // 新增的击中特效属性
        public EffecterDef impactEffecter;
        
        // 新增：反向Mote相关属性
        public ThingDef reverseMoteDef;           // 反向Mote定义
        public float reverseMoteSpeed = 2f;       // 反向Mote速度
        public int reverseMoteLifetimeTicks = 60; // 反向Mote存活时间
        public float reverseMoteScale = 1f;       // 反向Mote缩放
        public Vector3 reverseMoteOffset = Vector3.zero; // 反向Mote位置偏移
        public bool spawnReverseMoteOnLaunch = true;     // 是否在发射时生成反向Mote
    }

    public class Projectile_LineAttack : Bullet
    {
        private int hitCounter = 0;
        private List<Thing> alreadyDamaged = new List<Thing>();
        private Vector3 lastTickPosition;
        private int Fleck_MakeFleckTick;
        public int Fleck_MakeFleckTickMax = 1;
        public IntRange Fleck_MakeFleckNum = new IntRange(1, 1);
        public FloatRange Fleck_Angle = new FloatRange(-180f, 180f);
        public FloatRange Fleck_Scale = new FloatRange(1f, 1f);
        public FloatRange Fleck_Speed = new FloatRange(0f, 0f);
        public FloatRange Fleck_Rotation = new FloatRange(-180f, 180f);

        // 新增：反向Mote已发射标记
        private bool reverseMoteSpawned = false;

        private PathPierce_Extension Props => def.GetModExtension<PathPierce_Extension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hitCounter, "hitCounter", 0);
            Scribe_Collections.Look(ref alreadyDamaged, "alreadyDamaged", LookMode.Reference);
            Scribe_Values.Look(ref lastTickPosition, "lastTickPosition");
            Scribe_Values.Look(ref reverseMoteSpawned, "reverseMoteSpawned", false);

            if (alreadyDamaged == null)
            {
                alreadyDamaged = new List<Thing>();
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            this.lastTickPosition = origin;
            this.alreadyDamaged.Clear();
            this.hitCounter = 0;
            this.reverseMoteSpawned = false;
            this.preventFriendlyFire = preventFriendlyFire || (Props?.preventFriendlyFire ?? false);

            // 发射反向Mote
            SpawnReverseMote(origin, usedTarget);
        }

        /// <summary>
        /// 生成反向Mote（仅发射时一次）
        /// </summary>
        private void SpawnReverseMote(Vector3 origin, LocalTargetInfo target)
        {
            try
            {
                // 检查是否应该生成反向Mote
                if (Props == null || !Props.spawnReverseMoteOnLaunch || Props.reverseMoteDef == null || reverseMoteSpawned)
                    return;

                Map map = launcher?.Map ?? this.Map;
                if (map == null)
                    return;

                // 计算飞行方向
                Vector3 launchDirection = (target.Cell.ToVector3() - origin).normalized;

                // 如果方向为零向量，则跳过
                if (launchDirection.sqrMagnitude < 0.001f)
                    return;

                // 计算反向方向（180度相反）
                Vector3 reverseDirection = -launchDirection;

                // 计算反向Mote的位置（考虑偏移）
                Vector3 spawnPosition = origin + Props.reverseMoteOffset;

                // 创建Mote
                Mote mote = (Mote)ThingMaker.MakeThing(Props.reverseMoteDef);

                if (mote is MoteThrown moteThrown)
                {
                    // 设置初始位置
                    moteThrown.exactPosition = spawnPosition;

                    // 计算反向角度（注意：角度需要从向量转换为0-360度）
                    float reverseAngle = Mathf.Atan2(reverseDirection.x, reverseDirection.z) * Mathf.Rad2Deg;

                    // 设置速度方向为反向
                    moteThrown.SetVelocity(reverseAngle, Props.reverseMoteSpeed);

                    // 设置旋转（与飞行方向一致）
                    moteThrown.exactRotation = reverseAngle;
                    moteThrown.rotationRate = 0f;

                    // 设置缩放
                    moteThrown.Scale = Props.reverseMoteScale;

                    // 设置存活时间
                    moteThrown.airTimeLeft = Props.reverseMoteLifetimeTicks;

                    // 添加到地图
                    GenSpawn.Spawn(moteThrown, spawnPosition.ToIntVec3(), map);

                    // 标记已生成
                    reverseMoteSpawned = true;

                    // 调试日志
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Projectile_LineAttack] 反向Mote已生成: " +
                                   $"位置={spawnPosition}, " +
                                   $"方向={reverseDirection}, " +
                                   $"角度={reverseAngle}, " +
                                   $"速度={Props.reverseMoteSpeed}");
                    }
                }
                else
                {
                    // 不是MoteThrown类型，使用基础设置
                    mote.exactPosition = spawnPosition;
                    mote.Scale = Props.reverseMoteScale;
                    GenSpawn.Spawn(mote, spawnPosition.ToIntVec3(), map);
                    reverseMoteSpawned = true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Projectile_LineAttack] 生成反向Mote时出错: {ex}");
            }
        }

        protected override void Tick()
        {
            Vector3 startPos = this.lastTickPosition;
            base.Tick();
            if (this.Destroyed) return;

            this.Fleck_MakeFleckTick++;
            if (this.Fleck_MakeFleckTick >= Props.fleckDelayTicks)
            {
                if (this.Fleck_MakeFleckTick >= (Props.fleckDelayTicks + this.Fleck_MakeFleckTickMax))
                {
                    this.Fleck_MakeFleckTick = Props.fleckDelayTicks;
                }

                Map map = base.Map;
                int randomInRange = this.Fleck_MakeFleckNum.RandomInRange;
                Vector3 currentPosition = this.ExactPosition;

                for (int i = 0; i < randomInRange; i++)
                {
                    float currentBulletAngle = ExactRotation.eulerAngles.y;
                    float fleckRotationAngle = currentBulletAngle;
                    float velocityAngle = this.Fleck_Angle.RandomInRange + currentBulletAngle;
                    float randomInRange2 = this.Fleck_Scale.RandomInRange;
                    float randomInRange3 = this.Fleck_Speed.RandomInRange;
                    if (Props?.tailFleckDef != null)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(currentPosition, map, Props.tailFleckDef, randomInRange2);
                        dataStatic.rotation = fleckRotationAngle;
                        dataStatic.rotationRate = this.Fleck_Rotation.RandomInRange;
                        dataStatic.velocityAngle = velocityAngle;
                        dataStatic.velocitySpeed = randomInRange3;
                        map.flecks.CreateFleck(dataStatic);
                    }
                }
            }

            if (this.Destroyed) return;

            Vector3 endPos = this.ExactPosition;
            CheckPathForDamage(startPos, endPos);
            this.lastTickPosition = endPos;
        }
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 原有的穿透检测
            CheckPathForDamage(lastTickPosition, this.ExactPosition);
            if (hitThing != null && alreadyDamaged.Contains(hitThing))
            {
                base.Impact(null, blockedByShield);
            }
            else
            {
                base.Impact(hitThing, blockedByShield);
            }
            // 新增：触发击中特效
            if (Props?.impactEffecter != null)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(this.ExactPosition.ToIntVec3(), this.launcher.Map, false), this.launcher);
            }
        }
    private void CheckPathForDamage(Vector3 startPos, Vector3 endPos)
    {
      if (startPos == endPos) return;
      int maxHits = Props?.maxHits ?? 1;
      bool infinitePenetration = maxHits < 0;
      if (!infinitePenetration && hitCounter >= maxHits) return;
      Map map = this.Map;
      float distance = Vector3.Distance(startPos, endPos);
      Vector3 direction = (endPos - startPos).normalized;
      for (float i = 0; i < distance; i += 0.8f)
      {
        if (!infinitePenetration && hitCounter >= maxHits) break;
        Vector3 checkPos = startPos + direction * i;
        var thingsInCell = new HashSet<Thing>(map.thingGrid.ThingsListAt(checkPos.ToIntVec3()));
        foreach (Thing thing in thingsInCell)
        {
          if (thing is Pawn pawn && pawn != this.launcher && !alreadyDamaged.Contains(pawn))
          {
            bool shouldDamage = false;
            if (this.intendedTarget.Thing == pawn)
            {
              shouldDamage = true;
            }
            else if (pawn.HostileTo(this.launcher))
            {
              shouldDamage = true;
            }
            else if (!this.preventFriendlyFire)
            {
              shouldDamage = true;
            }
            if (shouldDamage)
            {
              ApplyPathDamage(pawn);
              if (!infinitePenetration && hitCounter >= maxHits) break;
            }
          }
        }
      }
    }
    private void ApplyPathDamage(Pawn pawn)
    {
      PathPierce_Extension props = Props;
      float falloff = props?.damageFalloff ?? 0.25f;

      float damageMultiplier = Mathf.Pow(1f - falloff, hitCounter);

      int damageAmount = (int)(this.DamageAmount * damageMultiplier);
      if (damageAmount <= 0) return;
      var dinfo = new DamageInfo(
          this.def.projectile.damageDef,
          damageAmount,
          this.ArmorPenetration * damageMultiplier,
          this.ExactRotation.eulerAngles.y,
          this.launcher,
          null,
          this.equipmentDef,
          DamageInfo.SourceCategory.ThingOrUnknown,
          this.intendedTarget.Thing);

      pawn.TakeDamage(dinfo);
      alreadyDamaged.Add(pawn);
      hitCounter++;
    }
  }
}