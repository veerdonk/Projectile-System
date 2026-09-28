using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class SimulationBridge
    {
        public IHitQuery HitQuery;
        public RecipeStore Recipes;
        public ProjectilePool Pool;
        public ModifierContext ModifierContext;
        public System.Action<HitInfo> Hit;
        public System.Action<ExpireInfo> Expired;
        public System.Action<StatusInfo> StatusApplied;
        public GroundFieldSet GroundFields;
    }

    [DisableAutoCreation]
    partial class ProjectileSimulationSystem : SystemBase
    {
        const int OccupancyOnTravel = 1;
        const int OccupancyOnTick = 2;
        const int OccupancyOnInterval = 3;
        const int OccupancyOnHit = 4;

        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<TargetId> seenAreaTargets = new List<TargetId>();
        readonly List<TargetId> occupancyCurrent = new List<TargetId>();
        readonly List<OccupancyKey> occupancyRemove = new List<OccupancyKey>();
        readonly Dictionary<OccupancyKey, List<TargetId>> occupancyInside = new Dictionary<OccupancyKey, List<TargetId>>();

        public SimulationBridge Bridge;

        protected override void OnUpdate()
        {
            Dependency.Complete();
            if (Bridge == null)
                return;

            float dt = SystemAPI.GetSingleton<SimDeltaTime>().Value;
            EntityManager em = EntityManager;

            var expire = new NativeList<Entity>(Allocator.Temp);
            foreach (var (projectile, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
            {
                if (projectile.ValueRO.RemainingLifetime <= 0f)
                    expire.Add(entity);
            }

            for (int i = 0; i < expire.Length; i++)
                Despawn(expire[i], runOnHit: false, default);
            expire.Dispose();

            var live = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
                live.Add(entity);

            for (int i = 0; i < live.Length; i++)
            {
                Entity entity = live[i];
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    continue;

                Projectile projectile = em.GetComponentData<Projectile>(entity);
                Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
                RunStage(recipe.OnTick, entity, hasHit: false, default, dt);
                FlushAreaOverlaps(entity, OccupancyOnTick);
                RunIntervals(entity, dt);
            }

            new IntegrateJob { Dt = dt }.ScheduleParallel();
            Dependency.Complete();

            for (int i = 0; i < live.Length; i++)
            {
                Entity entity = live[i];
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    continue;
                TryMoveOrHit(entity);
            }

            live.Dispose();
        }

        internal void RunSpawnStages(Entity entity, int multicastGenerationsLeft)
        {
            Projectile projectile = EntityManager.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            Bridge.ModifierContext.MulticastGenerationsForExtras = multicastGenerationsLeft - 1;
            RunStage(recipe.OnSpawn, entity, hasHit: false, default, dt: 0f, skipMulticast: multicastGenerationsLeft <= 0);
            RunStage(recipe.OnTravel, entity, hasHit: false, default, dt: 0f);
            FlushAreaOverlaps(entity, OccupancyOnTravel);
            FlushGroundFields(entity);
        }

        /// <summary>
        /// One polyline of Radius SphereCasts, then pool. Homing on OnTick is max degrees of kink
        /// per segment (applied with a one-second step so <see cref="Homing.TurnRateDegreesPerSecond"/>
        /// is degrees, not degrees per second). Unbounded length is 10 km.
        /// </summary>
        internal void ResolveHitscan(Entity entity)
        {
            const float maxRange = 10000f;
            const int maxSegments = 32;
            float remaining = maxRange;
            EntityManager em = EntityManager;

            for (int segment = 0; segment < maxSegments && remaining > 1e-4f; segment++)
            {
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    return;

                Projectile projectile = em.GetComponentData<Projectile>(entity);
                Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
                KinkHitscan(entity, recipe);

                projectile = em.GetComponentData<Projectile>(entity);
                Vector3 origin = ToVector3(em.GetComponentData<ProjectilePosition>(entity).Value);
                Vector3 velocity = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                Vector3 direction = velocity.sqrMagnitude > 1e-12f ? velocity.normalized : Vector3.forward;

                if (!Bridge.HitQuery.TryCast(
                        new ProjectileId(projectile.Id),
                        origin,
                        direction,
                        remaining,
                        projectile.Radius,
                        out HitInfo hit))
                {
                    Despawn(entity, runOnHit: false, default);
                    return;
                }

                hit.Damage = projectile.Damage;
                hit.CriticalChance = projectile.CriticalChance;
                hit.EffectRadius = projectile.EffectRadius;
                hit.Source = HitSource.Direct;
                Despawn(entity, runOnHit: true, hit);
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    return;

                float traveled = Vector3.Dot(hit.Point - origin, direction);
                if (traveled < 0.01f)
                    traveled = 0.01f;
                remaining -= traveled;
            }

            if (em.Exists(entity) && em.IsEnabled(entity))
                Despawn(entity, runOnHit: false, default);
        }

        void KinkHitscan(Entity entity, Recipe recipe)
        {
            IModifier[] onTick = recipe.OnTick;
            if (onTick == null || onTick.Length == 0)
                return;

            bool any = false;
            for (int i = 0; i < onTick.Length; i++)
            {
                if (onTick[i] is Homing)
                {
                    any = true;
                    break;
                }
            }

            if (!any)
                return;

            BindContext(entity, hasHit: false, default, dt: 1f);
            for (int i = 0; i < onTick.Length; i++)
            {
                if (onTick[i] is Homing homing)
                    homing.Apply(Bridge.ModifierContext);
            }

            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            Bridge.ModifierContext.ApplyTo(ref projectile, ref velocity);
            em.SetComponentData(entity, projectile);
            em.SetComponentData(entity, velocity);
        }

        void TryMoveOrHit(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectilePosition position = em.GetComponentData<ProjectilePosition>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            ProjectileCandidate candidate = em.GetComponentData<ProjectileCandidate>(entity);

            Vector3 from = ToVector3(position.Value);
            Vector3 to = ToVector3(candidate.Value);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            Vector3 direction = distance > 1e-8f ? delta / distance : ToVector3(velocity.Value);

            if (Bridge.HitQuery.TryCast(
                    new ProjectileId(projectile.Id),
                    from,
                    direction,
                    distance,
                    projectile.Radius,
                    out HitInfo hit))
            {
                hit.Damage = projectile.Damage;
                hit.CriticalChance = projectile.CriticalChance;
                hit.EffectRadius = projectile.EffectRadius;
                hit.Source = HitSource.Direct;
                Despawn(entity, runOnHit: true, hit);
                return;
            }

            em.SetComponentData(entity, new ProjectilePosition { Value = candidate.Value });
            if (Bridge.Pool.TryGetView(entity, out ProjectileView view))
                view.SetPose(to, ToVector3(velocity.Value));
        }

        void Despawn(Entity entity, bool runOnHit, in HitInfo hit)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);

            if (runOnHit && hit.Protection == ProtectionLevel.Strong)
            {
                ReturnToPool(entity, projectile);
                return;
            }

            if (runOnHit)
            {
                RunStage(recipe.OnHit, entity, hasHit: true, hit, dt: 0f);
                Projectile afterHit = em.GetComponentData<Projectile>(entity);
                Homing.ReleaseLockOnTargetHit(ref afterHit, hit);
                em.SetComponentData(entity, afterHit);
                if (!Bridge.ModifierContext.DirectHitSuppressed)
                    Bridge.Hit?.Invoke(hit);
                if (!Bridge.ModifierContext.DirectStatusSuppressed)
                    FlushStatusApplied(hit);
                FlushAreaOverlaps(entity, OccupancyOnHit);
                FlushGroundFields(entity);
                if (Bridge.ModifierContext.CancelDespawn)
                {
                    ContinueAfterHit(entity, hit);
                    return;
                }
            }

            RunStage(recipe.OnExpire, entity, hasHit: runOnHit, hit, dt: 0f);
            FlushGroundFields(entity);

            ReturnToPool(entity, em.GetComponentData<Projectile>(entity));
        }

        void ReturnToPool(Entity entity, in Projectile projectile)
        {
            var instanceId = new ProjectileId(projectile.Id);
            ClearOccupancy(instanceId);
            Bridge.Pool.Return(projectile.RecipeIndex, entity);
            Bridge.Expired?.Invoke(new ExpireInfo { InstanceId = instanceId });
        }

        void ContinueAfterHit(Entity entity, in HitInfo hit)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            Vector3 vel = ToVector3(velocity.Value);
            Vector3 travel = vel.sqrMagnitude > 1e-12f ? vel.normalized : hit.Normal;
            Vector3 position = hit.Point + travel * (projectile.Radius + 0.01f);
            float3 p = new float3(position.x, position.y, position.z);
            em.SetComponentData(entity, new ProjectilePosition { Value = p });
            em.SetComponentData(entity, new ProjectileCandidate { Value = p });
            if (Bridge.Pool.TryGetView(entity, out ProjectileView view))
            {
                view.SetPose(position, vel);
                view.SetRadius(projectile.Radius);
            }
        }

        void RunIntervals(Entity entity, float dt)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            IModifier[] modifiers = recipe.OnInterval;
            if (modifiers == null || modifiers.Length == 0)
                return;

            float elapsed = projectile.ElapsedSeconds;
            int fires = IntervalCrossings(elapsed - dt, elapsed, projectile.IntervalSeconds);
            if (fires <= 0)
                return;

            BindContext(entity, hasHit: false, default, dt);
            for (int f = 0; f < fires; f++)
            {
                Bridge.ModifierContext.ClearAreaRequests();
                for (int i = 0; i < modifiers.Length; i++)
                {
                    IModifier modifier = modifiers[i];
                    if (modifier == null)
                        continue;
                    if (modifier is Multiply && !Bridge.ModifierContext.AllowsMultiply)
                        continue;
                    modifier.Apply(Bridge.ModifierContext);
                }

                ProjectileVelocity intervalVelocity = em.GetComponentData<ProjectileVelocity>(entity);
                Bridge.ModifierContext.ApplyTo(ref projectile, ref intervalVelocity);
                em.SetComponentData(entity, projectile);
                em.SetComponentData(entity, intervalVelocity);
                FlushAreaOverlaps(entity, OccupancyOnInterval);
            }

            SyncViewRadius(entity);
        }

        static int IntervalCrossings(float previousElapsed, float elapsed, float interval)
        {
            if (interval <= 0f || elapsed <= previousElapsed)
                return 0;

            int before = (int)math.floor(previousElapsed / interval);
            int after = (int)math.floor(elapsed / interval);
            int fires = after - before;
            return fires > 0 ? fires : 0;
        }

        void FlushStatusApplied(in HitInfo hit)
        {
            if (hit.Kind != HitKind.Target || hit.Protection != ProtectionLevel.None)
                return;

            IReadOnlyList<IStatusEffect> effects = Bridge.ModifierContext.RequestedStatuses;
            for (int i = 0; i < effects.Count; i++)
            {
                IStatusEffect effect = effects[i];
                if (effect == null)
                    continue;

                Bridge.StatusApplied?.Invoke(new StatusInfo
                {
                    InstanceId = hit.InstanceId,
                    TargetId = hit.TargetId,
                    Effect = effect
                });
            }
        }

        void FlushAreaOverlaps(Entity entity, int occupancyChannel)
        {
            IReadOnlyList<AreaOverlapRequest> requests = Bridge.ModifierContext.RequestedAreaOverlaps;
            if (requests.Count == 0)
                return;

            Projectile projectile = EntityManager.GetComponentData<Projectile>(entity);
            var instanceId = new ProjectileId(projectile.Id);
            int occupancySlot = 0;
            for (int r = 0; r < requests.Count; r++)
            {
                AreaOverlapRequest request = requests[r];
                if (!request.Occupancy)
                {
                    FlushAreaRequest(request, instanceId, projectile);
                    continue;
                }

                FlushOccupancyRequest(request, instanceId, projectile, occupancyChannel, occupancySlot);
                occupancySlot++;
            }
        }

        void FlushAreaRequest(in AreaOverlapRequest request, ProjectileId instanceId, in Projectile projectile)
        {
            if (request.Radius <= 0f)
                return;

            overlapResults.Clear();
            seenAreaTargets.Clear();
            Bridge.HitQuery.Overlap(instanceId, request.Center, request.Radius, overlapResults);
            for (int i = 0; i < overlapResults.Count; i++)
                TryEmitAreaHit(overlapResults[i], request, instanceId, projectile);
        }

        void FlushOccupancyRequest(
            in AreaOverlapRequest request,
            ProjectileId instanceId,
            in Projectile projectile,
            int channel,
            int slot)
        {
            if (request.Radius <= 0f)
                return;

            var key = new OccupancyKey(instanceId, channel, slot);
            if (!occupancyInside.TryGetValue(key, out List<TargetId> previous))
            {
                previous = new List<TargetId>();
                occupancyInside[key] = previous;
            }

            overlapResults.Clear();
            Bridge.HitQuery.Overlap(instanceId, request.Center, request.Radius, overlapResults);
            OccupancyExchange.Exchange(
                previous,
                overlapResults,
                occupancyCurrent,
                instanceId,
                request.Damage,
                projectile.CriticalChance,
                projectile.EffectRadius,
                request.Status,
                Bridge.Hit,
                Bridge.StatusApplied);
        }

        void ClearOccupancy(ProjectileId instanceId)
        {
            occupancyRemove.Clear();
            foreach (KeyValuePair<OccupancyKey, List<TargetId>> pair in occupancyInside)
            {
                if (pair.Key.Instance == instanceId)
                    occupancyRemove.Add(pair.Key);
            }

            for (int i = 0; i < occupancyRemove.Count; i++)
                occupancyInside.Remove(occupancyRemove[i]);
        }

        void TryEmitAreaHit(
            HitInfo areaHit,
            in AreaOverlapRequest request,
            ProjectileId instanceId,
            in Projectile projectile)
        {
            if (areaHit.Kind != HitKind.Target || areaHit.Protection == ProtectionLevel.Strong)
                return;
            if (ContainsTarget(seenAreaTargets, areaHit.TargetId))
                return;

            seenAreaTargets.Add(areaHit.TargetId);
            areaHit.InstanceId = instanceId;
            areaHit.Source = HitSource.Area;
            areaHit.Damage = request.Damage;
            areaHit.CriticalChance = projectile.CriticalChance;
            areaHit.EffectRadius = projectile.EffectRadius;
            Bridge.Hit?.Invoke(areaHit);
            if (request.Status == null)
                return;

            Bridge.StatusApplied?.Invoke(new StatusInfo
            {
                InstanceId = instanceId,
                TargetId = areaHit.TargetId,
                Effect = request.Status
            });
        }

        void FlushGroundFields(Entity entity)
        {
            IReadOnlyList<GroundFieldPlantRequest> requests = Bridge.ModifierContext.RequestedGroundFields;
            if (requests.Count == 0 || Bridge.GroundFields == null)
                return;

            Projectile projectile = EntityManager.GetComponentData<Projectile>(entity);
            var instanceId = new ProjectileId(projectile.Id);
            for (int i = 0; i < requests.Count; i++)
                Bridge.GroundFields.Plant(instanceId, requests[i]);
        }

        static bool ContainsTarget(List<TargetId> ids, TargetId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }

        void RunStage(IModifier[] modifiers, Entity entity, bool hasHit, in HitInfo hit, float dt, bool skipMulticast = false)
        {
            Bridge.ModifierContext.CancelDespawn = false;
            Bridge.ModifierContext.ClearRequestedStatus();
            Bridge.ModifierContext.ClearAreaRequests();
            Bridge.ModifierContext.ClearGroundFieldRequests();
            if (modifiers == null || modifiers.Length == 0)
                return;

            BindContext(entity, hasHit, hit, dt);

            for (int i = 0; i < modifiers.Length; i++)
            {
                IModifier modifier = modifiers[i];
                if (modifier == null)
                    continue;
                if (skipMulticast && modifier is Multicast)
                    continue;
                if (modifier is Multiply && !Bridge.ModifierContext.AllowsMultiply)
                    continue;
                modifier.Apply(Bridge.ModifierContext);
            }

            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            Bridge.ModifierContext.ApplyTo(ref projectile, ref velocity);
            em.SetComponentData(entity, projectile);
            em.SetComponentData(entity, velocity);
            SyncViewRadius(entity);
        }

        void SyncViewRadius(Entity entity)
        {
            if (!Bridge.Pool.TryGetView(entity, out ProjectileView view))
                return;

            view.SetRadius(EntityManager.GetComponentData<Projectile>(entity).Radius);
        }

        void BindContext(Entity entity, bool hasHit, in HitInfo hit, float dt)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectilePosition position = em.GetComponentData<ProjectilePosition>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            HomingTarget homing = projectile.HomingHasTarget
                ? HomingTarget.Assigned(projectile.HomingTargetId)
                : default;
            HomingTarget ignored = projectile.HomingHasIgnoredTarget
                ? HomingTarget.Assigned(projectile.HomingIgnoredTargetId)
                : default;
            var state = new ModifierBindState
            {
                InstanceId = new ProjectileId(projectile.Id),
                Position = ToVector3(position.Value),
                Velocity = ToVector3(velocity.Value),
                Speed = projectile.Speed,
                Gravity = projectile.Gravity,
                RemainingLifetime = projectile.RemainingLifetime,
                RemainingPierce = projectile.RemainingPierce,
                RemainingBounce = projectile.RemainingBounce,
                Radius = projectile.Radius,
                Damage = projectile.Damage,
                CriticalChance = projectile.CriticalChance,
                ManaCost = projectile.ManaCost,
                EffectRadius = projectile.EffectRadius,
                Spread = projectile.Spread,
                IntervalSeconds = projectile.IntervalSeconds,
                MultiplyGenerationsLeft = projectile.MultiplyGenerationsLeft,
                HomingTarget = homing,
                HomingIgnoredTarget = ignored,
                HomingAcquirePointSet = projectile.HomingAcquirePointSet,
                HomingAcquirePoint = ToVector3(projectile.HomingAcquirePoint),
                HomingAimPointUsed = projectile.HomingAimPointUsed
            };
            Bridge.ModifierContext.Bind(state, hasHit, hit, dt);
        }

        static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        readonly struct OccupancyKey : IEquatable<OccupancyKey>
        {
            public OccupancyKey(ProjectileId instance, int channel, int slot)
            {
                Instance = instance;
                Channel = channel;
                Slot = slot;
            }

            public ProjectileId Instance { get; }
            public int Channel { get; }
            public int Slot { get; }

            public bool Equals(OccupancyKey other)
            {
                return Instance == other.Instance && Channel == other.Channel && Slot == other.Slot;
            }

            public override bool Equals(object obj) => obj is OccupancyKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Instance.GetHashCode();
                    hash = (hash * 397) ^ Channel;
                    return (hash * 397) ^ Slot;
                }
            }
        }
    }

    [BurstCompile]
    partial struct IntegrateJob : IJobEntity
    {
        public float Dt;

        void Execute(
            in Projectile projectile,
            in ProjectilePosition position,
            ref ProjectileVelocity velocity,
            ref ProjectileCandidate candidate)
        {
            if (projectile.Delivery == Delivery.Arc && projectile.Gravity > 0f)
                velocity.Value += new float3(0f, -1f, 0f) * projectile.Gravity * Dt;

            candidate.Value = position.Value + velocity.Value * Dt;
        }
    }
}
