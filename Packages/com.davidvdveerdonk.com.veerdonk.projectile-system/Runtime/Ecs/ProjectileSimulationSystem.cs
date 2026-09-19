using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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
    }

    [DisableAutoCreation]
    partial class ProjectileSimulationSystem : SystemBase
    {
        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<TargetId> seenAreaTargets = new List<TargetId>();

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
            FlushAreaOverlaps(entity);
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
                FlushAreaOverlaps(entity);
                if (Bridge.ModifierContext.CancelDespawn)
                {
                    ContinueAfterHit(entity, hit);
                    return;
                }
            }

            RunStage(recipe.OnExpire, entity, hasHit: runOnHit, hit, dt: 0f);

            ReturnToPool(entity, em.GetComponentData<Projectile>(entity));
        }

        void ReturnToPool(Entity entity, in Projectile projectile)
        {
            Bridge.Pool.Return(projectile.RecipeIndex, entity);
            Bridge.Expired?.Invoke(new ExpireInfo { InstanceId = new ProjectileId(projectile.Id) });
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
                FlushAreaOverlaps(entity);
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

        void FlushAreaOverlaps(Entity entity)
        {
            IReadOnlyList<AreaOverlapRequest> requests = Bridge.ModifierContext.RequestedAreaOverlaps;
            if (requests.Count == 0)
                return;

            Projectile projectile = EntityManager.GetComponentData<Projectile>(entity);
            var instanceId = new ProjectileId(projectile.Id);

            for (int r = 0; r < requests.Count; r++)
            {
                AreaOverlapRequest request = requests[r];
                if (request.Radius <= 0f)
                    continue;

                overlapResults.Clear();
                seenAreaTargets.Clear();
                Bridge.HitQuery.Overlap(instanceId, request.Center, request.Radius, overlapResults);

                for (int i = 0; i < overlapResults.Count; i++)
                {
                    HitInfo areaHit = overlapResults[i];
                    if (areaHit.Kind != HitKind.Target || areaHit.Protection == ProtectionLevel.Strong)
                        continue;
                    if (ContainsTarget(seenAreaTargets, areaHit.TargetId))
                        continue;

                    seenAreaTargets.Add(areaHit.TargetId);
                    areaHit.InstanceId = instanceId;
                    areaHit.Source = HitSource.Area;
                    areaHit.Damage = request.Damage;
                    areaHit.CriticalChance = projectile.CriticalChance;
                    areaHit.EffectRadius = projectile.EffectRadius;
                    Bridge.Hit?.Invoke(areaHit);

                    if (request.Status == null)
                        continue;

                    Bridge.StatusApplied?.Invoke(new StatusInfo
                    {
                        InstanceId = instanceId,
                        TargetId = areaHit.TargetId,
                        Effect = request.Status
                    });
                }
            }
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
            Bridge.ModifierContext.Bind(
                new ProjectileId(projectile.Id),
                ToVector3(position.Value),
                ToVector3(velocity.Value),
                projectile.Speed,
                projectile.Gravity,
                projectile.RemainingLifetime,
                projectile.RemainingPierce,
                projectile.RemainingBounce,
                projectile.Radius,
                projectile.Damage,
                projectile.CriticalChance,
                projectile.ManaCost,
                projectile.EffectRadius,
                projectile.Spread,
                projectile.IntervalSeconds,
                projectile.MultiplyGenerationsLeft,
                hasHit,
                hit,
                dt,
                homing,
                ignored);
        }

        static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
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
            if (projectile.Gravity > 0f)
                velocity.Value += new float3(0f, -1f, 0f) * projectile.Gravity * Dt;

            candidate.Value = position.Value + velocity.Value * Dt;
        }
    }
}
