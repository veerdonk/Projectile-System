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
        public System.Action<HomingLockInfo> HomingLocked;
        public System.Action<StatusInfo> StatusApplied;
        public System.Action<AreaBurstInfo> AreaBurst;
        public GroundFieldSet GroundFields;
        public readonly List<ProjectileId> RetiredIds = new List<ProjectileId>();
    }

    [DisableAutoCreation]
    partial class ProjectileSimulationSystem : SystemBase
    {
        const int OccupancyOnTravel = 1;
        const int OccupancyOnTick = 2;
        const int OccupancyOnInterval = 3;
        const int OccupancyOnHit = 4;

        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<Vector3> beamPoints = new List<Vector3>();
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
                Despawn(expire[i], runOnHit: false, default, reason: ExpireReason.Lifetime);
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

            for (int i = 0; i < live.Length; i++)
            {
                Entity entity = live[i];
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    continue;

                PulseBeamIfDue(entity);
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

        internal void DespawnInstance(Entity entity)
        {
            Despawn(entity, runOnHit: false, default, reason: ExpireReason.Despawned);
        }

        /// <summary>
        /// One polyline of Radius SphereCasts, then pool. Homing on OnTick is max degrees of kink
        /// per segment (dt = 1). Unbounded length is 10 km.
        /// </summary>
        internal void ResolveHitscan(Entity entity)
        {
            ResolvePolyline(entity, keepInstance: false);
        }

        /// <summary>
        /// One Beam pulse, then restore the spawn pose. Pierce does not pool the instance.
        /// Homing steps along the polyline and turns at degrees per second (dt = distance / speed).
        /// </summary>
        internal void PulseBeam(Entity entity)
        {
            ResolvePolyline(entity, keepInstance: true);
        }

        internal void AnchorBeam(Entity entity)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            float3 direction = velocity.Value;
            if (math.lengthsq(direction) < 1e-12f)
                direction = new float3(0f, 0f, 1f);
            else
                direction = math.normalize(direction);

            projectile.BeamOrigin = em.GetComponentData<ProjectilePosition>(entity).Value;
            projectile.BeamDirection = direction;
            em.SetComponentData(entity, projectile);
        }

        internal void AimBeam(Entity entity, Vector3 position, Vector3 direction)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            if (projectile.Delivery != Delivery.Beam)
                return;

            float3 origin = new float3(position.x, position.y, position.z);
            float3 dir = new float3(direction.x, direction.y, direction.z);
            if (math.lengthsq(dir) < 1e-12f)
                dir = new float3(0f, 0f, 1f);
            else
                dir = math.normalize(dir);

            projectile.BeamOrigin = origin;
            projectile.BeamDirection = dir;
            em.SetComponentData(entity, projectile);
            ResolvePolyline(entity, keepInstance: true, raiseContacts: false);
        }

        void ResolvePolyline(Entity entity, bool keepInstance, bool raiseContacts = true)
        {
            const float maxRange = 10000f;
            const int maxSegments = 32;
            const int maxCurveSteps = 128;
            const float curveStep = 0.5f;
            beamPoints.Clear();
            if (keepInstance)
                PrepareBeamPulse(entity);

            float remaining = maxRange;
            EntityManager em = EntityManager;
            bool curve = false;
            if (keepInstance && em.Exists(entity) && em.IsEnabled(entity))
            {
                Projectile seeded = em.GetComponentData<Projectile>(entity);
                curve = HasHoming(Bridge.Recipes.Get(seeded.RecipeIndex));
            }

            int limit = curve ? maxCurveSteps : maxSegments;
            bool capturedMuzzleLock = false;
            bool pastHit = false;
            for (int segment = 0; segment < limit && remaining > 1e-4f; segment++)
            {
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    return;

                Projectile projectile = em.GetComponentData<Projectile>(entity);
                Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
                Vector3 velocityBefore = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                Vector3 directionBefore = velocityBefore.sqrMagnitude > 1e-12f
                    ? velocityBefore.normalized
                    : Vector3.forward;

                bool bend = curve && segment > 0;
                float step = curve ? Mathf.Min(curveStep, remaining) : remaining;
                float speed = projectile.Speed > 1e-3f ? projectile.Speed : 1f;
                float allowedTurn = bend ? Mathf.Max(0f, HomingTurnDegrees(recipe)) * (step / speed) : 0f;
                if (bend || !curve)
                    KinkHitscan(entity, recipe, curve ? step / speed : 1f);
                if (bend && !pastHit && !capturedMuzzleLock)
                {
                    RememberBeamLock(entity);
                    capturedMuzzleLock = true;
                }

                projectile = em.GetComponentData<Projectile>(entity);
                Vector3 origin = ToVector3(em.GetComponentData<ProjectilePosition>(entity).Value);
                Vector3 velocity = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                Vector3 direction = velocity.sqrMagnitude > 1e-12f ? velocity.normalized : Vector3.forward;
                float turned = Vector3.Angle(directionBefore, direction);
                bool reachedAim = !curve || (bend && (allowedTurn <= 0.05f || turned + 0.05f < allowedTurn));
                float castDistance = reachedAim || segment >= limit - 1 ? remaining : step;
                if (keepInstance)
                    AppendBeamPoint(origin);

                if (!Bridge.HitQuery.TryCast(
                        new ProjectileId(projectile.Id),
                        origin,
                        direction,
                        castDistance,
                        projectile.Radius,
                        out HitInfo hit))
                {
                    bool partial = curve && castDistance < remaining - 1e-3f;
                    if (partial)
                    {
                        Vector3 end = origin + direction * castDistance;
                        AppendBeamPoint(end);
                        AdvanceBeam(entity, end);
                        remaining -= castDistance;
                        continue;
                    }

                    if (keepInstance)
                    {
                        AppendBeamSpan(origin, origin + direction * remaining);
                        FinishBeamPulse(entity);
                    }
                    else
                        Despawn(entity, runOnHit: false, default, reason: ExpireReason.HitscanEnd);

                    return;
                }

                hit.Damage = projectile.Damage;
                hit.CriticalChance = projectile.CriticalChance;
                hit.EffectRadius = projectile.EffectRadius;
                hit.Source = HitSource.Direct;
                if (keepInstance)
                    AppendBeamSpan(origin, hit.Point);

                bool continuePolyline = raiseContacts
                    ? Despawn(entity, runOnHit: true, hit, expireInstance: !keepInstance)
                    : PreviewBeamContact(entity, hit);
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    return;
                if (!continuePolyline)
                {
                    if (keepInstance)
                        FinishBeamPulse(entity);
                    return;
                }

                pastHit = true;
                float traveled = Vector3.Dot(hit.Point - origin, direction);
                if (traveled < 0.01f)
                    traveled = 0.01f;
                remaining -= traveled;
            }

            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            if (keepInstance)
                FinishBeamPulse(entity);
            else
                Despawn(entity, runOnHit: false, default, reason: ExpireReason.HitscanEnd);
        }

        void AdvanceBeam(Entity entity, Vector3 position)
        {
            float3 point = new float3(position.x, position.y, position.z);
            EntityManager.SetComponentData(entity, new ProjectilePosition { Value = point });
            EntityManager.SetComponentData(entity, new ProjectileCandidate { Value = point });
        }

        static bool HasHoming(Recipe recipe)
        {
            IModifier[] onTick = recipe.OnTick;
            if (onTick == null)
                return false;

            for (int i = 0; i < onTick.Length; i++)
            {
                if (onTick[i] is Homing)
                    return true;
            }

            return false;
        }

        static float HomingTurnDegrees(Recipe recipe)
        {
            IModifier[] onTick = recipe.OnTick;
            if (onTick == null)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < onTick.Length; i++)
            {
                if (onTick[i] is Homing homing)
                    sum += homing.TurnRateDegreesPerSecond;
            }

            return sum;
        }

        void PulseBeamIfDue(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            if (projectile.Delivery != Delivery.Beam)
                return;

            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            float period = recipe.BeamPulseSeconds;
            if (period <= 0f)
            {
                PulseBeam(entity);
                return;
            }

            int guard = 0;
            while (em.Exists(entity) && em.IsEnabled(entity) && guard < 8)
            {
                projectile = em.GetComponentData<Projectile>(entity);
                if (projectile.BeamPulseElapsed < period)
                    break;

                PulseBeam(entity);
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    return;

                projectile = em.GetComponentData<Projectile>(entity);
                projectile.BeamPulseElapsed -= period;
                if (projectile.BeamPulseElapsed < 0f)
                    projectile.BeamPulseElapsed = 0f;
                em.SetComponentData(entity, projectile);
                guard++;
            }
        }

        void PrepareBeamPulse(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            ReadHitRemaining(recipe, out int remainingPierce, out int remainingBounce);
            projectile.RemainingPierce = remainingPierce;
            projectile.RemainingBounce = remainingBounce;
            projectile.HomingAimPointUsed = false;
            if (!projectile.HomingAssignedAtSpawn)
            {
                if (BeamLockStillAhead(projectile))
                {
                    projectile.HomingHasTarget = true;
                    projectile.HomingTargetId = projectile.BeamHomingLockId;
                    projectile.HomingHasIgnoredTarget = false;
                    projectile.HomingAimPointUsed = true;
                }
                else
                {
                    projectile.HomingHasTarget = false;
                    projectile.HomingHasIgnoredTarget = false;
                    projectile.BeamHomingLockSet = false;
                }
            }

            if (Bridge.HitQuery != null)
                Bridge.HitQuery.ClearIgnore(new ProjectileId(projectile.Id));

            em.SetComponentData(entity, projectile);
            float3 direction = projectile.BeamDirection;
            if (math.lengthsq(direction) < 1e-12f)
                direction = new float3(0f, 0f, 1f);
            float speed = projectile.Speed > 0f ? projectile.Speed : 1f;
            float3 velocity = direction * speed;
            em.SetComponentData(entity, new ProjectilePosition { Value = projectile.BeamOrigin });
            em.SetComponentData(entity, new ProjectileVelocity { Value = velocity });
            em.SetComponentData(entity, new ProjectileCandidate { Value = projectile.BeamOrigin });
        }

        void RememberBeamLock(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            if (!projectile.HomingHasTarget || projectile.HomingAssignedAtSpawn)
                return;

            projectile.BeamHomingLockSet = true;
            projectile.BeamHomingLockId = projectile.HomingTargetId;
            em.SetComponentData(entity, projectile);
        }

        bool BeamLockStillAhead(in Projectile projectile)
        {
            if (!projectile.BeamHomingLockSet || Bridge.ModifierContext == null)
                return false;

            if (!Bridge.ModifierContext.TryGetSteeringPosition(
                    HomingTarget.Assigned(projectile.BeamHomingLockId),
                    out Vector3 position))
                return false;

            Vector3 forward = ToVector3(projectile.BeamDirection);
            Vector3 to = position - ToVector3(projectile.BeamOrigin);
            if (forward.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f)
                return false;

            return Vector3.Dot(to.normalized, forward.normalized) > 0.5f;
        }

        void FinishBeamPulse(Entity entity)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            if (Bridge.Pool.TryGetView(entity, out ProjectileView view))
                view.SetBeamSegments(beamPoints, em.GetComponentData<Projectile>(entity).Radius);

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            float3 direction = projectile.BeamDirection;
            if (math.lengthsq(direction) < 1e-12f)
                direction = new float3(0f, 0f, 1f);
            float speed = projectile.Speed > 0f ? projectile.Speed : 1f;
            em.SetComponentData(entity, new ProjectilePosition { Value = projectile.BeamOrigin });
            em.SetComponentData(entity, new ProjectileVelocity { Value = direction * speed });
            em.SetComponentData(entity, new ProjectileCandidate { Value = projectile.BeamOrigin });
        }

        void AppendBeamSpan(Vector3 from, Vector3 to)
        {
            const float visualStep = 0.5f;
            const float visualCap = 40f;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f)
            {
                AppendBeamPoint(to);
                return;
            }

            float draw = length < visualCap ? length : visualCap;
            int steps = Mathf.Max(1, Mathf.CeilToInt(draw / visualStep));
            Vector3 step = delta / length * (draw / steps);
            for (int i = 1; i <= steps; i++)
                AppendBeamPoint(from + step * i);
        }

        bool PreviewBeamContact(Entity entity, in HitInfo hit)
        {
            if (hit.Protection == ProtectionLevel.Strong)
                return false;

            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            bool cancel = false;
            IModifier[] onHit = recipe.OnHit;
            if (onHit != null)
            {
                for (int i = 0; i < onHit.Length; i++)
                {
                    switch (onHit[i])
                    {
                        case Pierce when projectile.RemainingPierce > 0
                            && (hit.Kind == HitKind.Target || hit.Protection == ProtectionLevel.Basic):
                            projectile.RemainingPierce--;
                            cancel = true;
                            break;
                        case Bounce when projectile.RemainingBounce > 0:
                            projectile.RemainingBounce--;
                            Vector3 vel = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                            Vector3 reflected = Vector3.Reflect(vel, hit.Normal);
                            em.SetComponentData(entity, new ProjectileVelocity
                            {
                                Value = new float3(reflected.x, reflected.y, reflected.z)
                            });
                            cancel = true;
                            break;
                    }
                }
            }

            if (cancel)
                Homing.ReleaseLockOnTargetHit(ref projectile, hit);

            em.SetComponentData(entity, projectile);
            if (!cancel)
                return false;

            ContinueAfterHit(entity, hit);
            return true;
        }

        void AppendBeamPoint(Vector3 point)
        {
            if (beamPoints.Count > 0 && (beamPoints[beamPoints.Count - 1] - point).sqrMagnitude < 1e-8f)
                return;

            beamPoints.Add(point);
        }

        static void ReadHitRemaining(Recipe recipe, out int remainingPierce, out int remainingBounce)
        {
            remainingPierce = 0;
            remainingBounce = 0;
            IModifier[] onHit = recipe.OnHit;
            if (onHit == null)
                return;

            for (int i = 0; i < onHit.Length; i++)
            {
                switch (onHit[i])
                {
                    case Pierce pierce:
                        remainingPierce = pierce.Count > 0 ? pierce.Count : 0;
                        break;
                    case Bounce bounce:
                        remainingBounce = bounce.Count > 0 ? bounce.Count : 0;
                        break;
                }
            }
        }

        void KinkHitscan(Entity entity, Recipe recipe, float dt)
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

            BindContext(entity, hasHit: false, default, dt: dt);
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
            FlushHomingLocks();
        }

        void TryMoveOrHit(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            if (projectile.Delivery == Delivery.Beam)
                return;

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

        bool Despawn(
            Entity entity,
            bool runOnHit,
            in HitInfo hit,
            bool expireInstance = true,
            ExpireReason reason = ExpireReason.Other)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return false;

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);

            if (runOnHit && hit.Protection == ProtectionLevel.Strong)
            {
                if (expireInstance)
                    ReturnToPool(entity, projectile, ExpireReason.Absorbed);
                return false;
            }

            if (runOnHit)
            {
                RunStage(recipe.OnHit, entity, hasHit: true, hit, dt: 0f);
                Projectile afterHit = em.GetComponentData<Projectile>(entity);
                Homing.ReleaseLockOnTargetHit(ref afterHit, hit);
                em.SetComponentData(entity, afterHit);
                HitInfo raised = hit;
                raised.Outcome = ResolveOutcome(projectile, afterHit, Bridge.ModifierContext.CancelDespawn);
                if (!Bridge.ModifierContext.DirectHitSuppressed)
                    Bridge.Hit?.Invoke(raised);
                if (!Bridge.ModifierContext.DirectStatusSuppressed)
                    FlushStatusApplied(hit);
                if (!em.Exists(entity) || !em.IsEnabled(entity)
                    || em.GetComponentData<Projectile>(entity).Id != projectile.Id)
                    return false;
                FlushAreaOverlaps(entity, OccupancyOnHit);
                FlushGroundFields(entity);
                if (Bridge.ModifierContext.CancelDespawn)
                {
                    ContinueAfterHit(entity, hit);
                    return true;
                }

                if (!expireInstance)
                    return false;
            }

            RunStage(recipe.OnExpire, entity, hasHit: runOnHit, hit, dt: 0f);
            FlushGroundFields(entity);

            ReturnToPool(
                entity,
                em.GetComponentData<Projectile>(entity),
                runOnHit ? ExpireReason.Hit : reason);
            return false;
        }

        static HitOutcome ResolveOutcome(in Projectile before, in Projectile after, bool cancelDespawn)
        {
            if (!cancelDespawn)
                return HitOutcome.Ended;
            if (after.RemainingPierce < before.RemainingPierce)
                return HitOutcome.Pierced;
            if (after.RemainingBounce < before.RemainingBounce)
                return HitOutcome.Bounced;
            return HitOutcome.Continued;
        }

        void ReturnToPool(Entity entity, in Projectile projectile, ExpireReason reason)
        {
            EntityManager em = EntityManager;
            var instanceId = new ProjectileId(projectile.Id);
            Vector3 position = ToVector3(em.GetComponentData<ProjectilePosition>(entity).Value);
            Vector3 velocity = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
            Vector3 direction = velocity.sqrMagnitude > 1e-12f
                ? velocity.normalized
                : ToVector3(projectile.BeamDirection);
            ClearOccupancy(instanceId);
            Bridge.RetiredIds.Add(instanceId);
            Bridge.Pool.Return(projectile.RecipeIndex, entity);
            Bridge.Expired?.Invoke(new ExpireInfo
            {
                InstanceId = instanceId,
                Position = position,
                Direction = direction,
                Reason = reason
            });
        }

        void ContinueAfterHit(Entity entity, in HitInfo hit)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            Vector3 vel = ToVector3(velocity.Value);
            Vector3 travel = vel.sqrMagnitude > 1e-12f ? vel.normalized : hit.Normal;
            float clearance = projectile.Radius + (projectile.Delivery == Delivery.Beam ? 0.08f : 0.01f);
            Vector3 position = hit.Point + travel * clearance;
            float3 p = new float3(position.x, position.y, position.z);
            em.SetComponentData(entity, new ProjectilePosition { Value = p });
            em.SetComponentData(entity, new ProjectileCandidate { Value = p });
            if (projectile.Delivery == Delivery.Beam)
            {
                Bridge.HitQuery?.HoldIgnore(new ProjectileId(projectile.Id));
                return;
            }

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
                FlushHomingLocks();
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
                    FlushAreaRequest(request, instanceId, projectile, occupancyChannel);
                    continue;
                }

                FlushOccupancyRequest(request, instanceId, projectile, occupancyChannel, occupancySlot);
                occupancySlot++;
            }
        }

        void FlushAreaRequest(in AreaOverlapRequest request, ProjectileId instanceId, in Projectile projectile, int channel)
        {
            if (request.Radius <= 0f)
                return;

            RaiseAreaBurst(request, instanceId, channel);
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
                RaiseAreaBurst(request, instanceId, channel);
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

        void RaiseAreaBurst(in AreaOverlapRequest request, ProjectileId instanceId, int channel)
        {
            Bridge.AreaBurst?.Invoke(new AreaBurstInfo
            {
                InstanceId = instanceId,
                Center = request.Center,
                Radius = request.Radius,
                Stage = StageForChannel(channel),
                Occupancy = request.Occupancy
            });
        }

        static RecipeStage StageForChannel(int channel)
        {
            switch (channel)
            {
                case OccupancyOnTravel: return RecipeStage.OnTravel;
                case OccupancyOnTick: return RecipeStage.OnTick;
                case OccupancyOnInterval: return RecipeStage.OnInterval;
                default: return RecipeStage.OnHit;
            }
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
            if (hasHit)
                RecordPierceExit(hit);

            for (int i = 0; i < modifiers.Length; i++)
            {
                IModifier modifier = modifiers[i];
                if (modifier == null)
                    continue;
                if (skipMulticast && modifier is Multicast)
                    continue;
                if (modifier is Homing && EntityManager.GetComponentData<Projectile>(entity).Delivery == Delivery.Beam)
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
            FlushHomingLocks();
            SyncViewRadius(entity);
        }

        void FlushHomingLocks()
        {
            IReadOnlyList<HomingLockInfo> locks = Bridge.ModifierContext.AcquiredHomingLocks;
            for (int i = 0; i < locks.Count; i++)
                Bridge.HomingLocked?.Invoke(locks[i]);
            Bridge.ModifierContext.ClearHomingLocks();
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

        void RecordPierceExit(in HitInfo hit)
        {
            ModifierContext context = Bridge.ModifierContext;
            if (context.PierceAtContact <= 0)
                return;
            if (hit.Kind != HitKind.Target && hit.Protection != ProtectionLevel.Basic)
                return;
            if (Bridge.HitQuery == null)
                return;
            if (Bridge.HitQuery.TryPierceExit(context.InstanceId, context.ContactVelocity, out Vector3 surface))
                context.SetPierceExit(surface);
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
            if (projectile.Delivery == Delivery.Beam)
                return;

            if (projectile.Delivery == Delivery.Arc && projectile.Gravity > 0f)
                velocity.Value += new float3(0f, -1f, 0f) * projectile.Gravity * Dt;

            candidate.Value = position.Value + velocity.Value * Dt;
        }
    }
}
