using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Named behavior on a Recipe stage list.</summary>
    public interface IModifier
    {
        /// <summary>Runs in list order for the current stage.</summary>
        void Apply(ModifierContext context);
    }

    /// <summary>
    /// Per-stage access to one instance. Modifiers must not touch Unity objects or the pool.
    /// </summary>
    public sealed class ModifierContext
    {
        readonly List<SpawnRequest> pendingSpawns;
        readonly List<IStatusEffect> requestedStatuses = new List<IStatusEffect>();
        readonly List<HomingLockInfo> acquiredHomingLocks = new List<HomingLockInfo>();
        readonly List<AreaOverlapRequest> requestedAreas = new List<AreaOverlapRequest>();
        readonly List<GroundFieldPlantRequest> requestedGroundFields = new List<GroundFieldPlantRequest>();
        bool suppressDirectHit;
        bool suppressDirectStatus;
        Unity.Mathematics.Random random;

        internal ModifierContext(
            List<SpawnRequest> pendingSpawns,
            HomingCatalog homingCatalog,
            uint seed = 1u)
        {
            this.pendingSpawns = pendingSpawns ?? throw new ArgumentNullException(nameof(pendingSpawns));
            HomingCatalog = homingCatalog ?? throw new ArgumentNullException(nameof(homingCatalog));
            random = Unity.Mathematics.Random.CreateFromIndex(seed);
        }

        internal HitTargetCatalog HitTargets { get; set; }

        internal bool TryGetSteeringPosition(in HomingTarget target, out Vector3 position)
        {
            if (!target.IsAssigned)
            {
                position = default;
                return false;
            }

            if (HitTargets != null && HitTargets.TryGetPosition(target.Id, out position))
                return true;

            return HomingCatalog.TryGetPosition(target, out position);
        }

        /// <summary>Live instance this stage is running for.</summary>
        public ProjectileId InstanceId { get; internal set; }

        /// <summary>World-space position at the start of this stage.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Current velocity. Speed modifiers rewrite this and <see cref="Speed"/> together.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>Current speed in meters per second.</summary>
        public float Speed { get; set; }

        /// <summary>
        /// Instance gravity in m/s², copied from <see cref="Recipe.Gravity"/> at spawn.
        /// Burst applies it each tick; the Gravity modifier scales this copy, not the Recipe.
        /// </summary>
        public float Gravity { get; set; }

        /// <summary>Seconds until expire. Lifetime system writes this down each tick.</summary>
        public float RemainingLifetime { get; set; }

        /// <summary>Pierce hits still available. Pierce decrements this and may cancel despawn.</summary>
        public int RemainingPierce { get; set; }

        /// <summary>Pierce charges at the start of this stage, before <see cref="Pierce"/> decrements <see cref="RemainingPierce"/>.</summary>
        internal int PierceAtContact { get; private set; }

        /// <summary>Velocity when this stage was bound, before OnHit modifiers rewrite it.</summary>
        internal Vector3 ContactVelocity { get; private set; }

        /// <summary>True when this hit resolved a far surface for a pierce-through multiply.</summary>
        internal bool PierceExitSet { get; private set; }

        /// <summary>Far surface of the body just pierced, along travel. Meaningful when <see cref="PierceExitSet"/> is true.</summary>
        internal Vector3 PierceExit { get; private set; }

        internal void SetPierceExit(Vector3 surface)
        {
            PierceExit = surface;
            PierceExitSet = true;
        }

        internal void ClearPierceExit()
        {
            PierceExitSet = false;
            PierceExit = default;
        }

        /// <summary>Bounce hits still available. Bounce decrements this and may cancel despawn.</summary>
        public int RemainingBounce { get; set; }

        /// <summary>Instance collision radius. <see cref="Size"/> scales it. SphereCast and the view follow this value.</summary>
        public float Radius { get; set; }

        /// <summary>Instance damage, copied from the Recipe at spawn. Sampled onto <see cref="HitInfo"/> at contact.</summary>
        public float Damage { get; set; }

        /// <summary>Instance critical chance 0–1, copied from the Recipe at spawn. The package does not roll it.</summary>
        public float CriticalChance { get; set; }

        /// <summary>
        /// Instance mana-cost copy, taken from <see cref="Recipe.ManaCost"/> at spawn.
        /// <see cref="Mana"/> mutates this; the Recipe field stays the authored fire-time cost.
        /// </summary>
        public float ManaCost { get; set; }

        /// <summary>Instance area-effect radius, copied from the Recipe at spawn. Scaled by the EffectRadius modifier.</summary>
        public float EffectRadius { get; set; }

        /// <summary>
        /// Instance copy of <see cref="Multiply.Spread"/>, taken from the Recipe's Multiply modifiers at spawn.
        /// The Spread modifier scales this; patterns read it rather than only the modifier field.
        /// </summary>
        public float Spread { get; set; }

        /// <summary>Seconds between <c>OnInterval</c> runs. <see cref="Interval"/> writes it on <c>OnSpawn</c>; 0 never fires.</summary>
        public float IntervalSeconds { get; set; }

        /// <summary>True when this stage is OnHit (or expire-from-hit) with a sampled contact.</summary>
        public bool HasHit { get; internal set; }

        /// <summary>Sampled contact. Only meaningful when <see cref="HasHit"/> is true.</summary>
        public HitInfo Hit { get; internal set; }

        /// <summary>When true after OnHit, the instance stays live. Pierce and Bounce set this.</summary>
        public bool CancelDespawn { get; set; }

        /// <summary>Seconds this stage is advancing.</summary>
        public float DeltaTime { get; internal set; }

        /// <summary>Lock for this instance. Homing writes here after a successful acquire.</summary>
        public HomingTarget HomingTarget { get; set; }

        /// <summary>Last Target hit. Acquisition skips it while it stays in front of travel.</summary>
        public HomingTarget HomingIgnoredTarget { get; set; }

        /// <summary>The spawn's <see cref="SpawnRequest.IgnoreTarget"/>, when set. Acquisition never locks it.</summary>
        public HomingTarget ShooterTarget { get; internal set; }

        /// <summary>
        /// True when spawn stored a fire-time aim point. Homing’s first lock uses
        /// <see cref="ClosestToPointAcquisition"/> against <see cref="HomingAcquirePoint"/>.
        /// </summary>
        public bool HomingAcquirePointSet { get; internal set; }

        /// <summary>World point captured at fire. Ignored unless <see cref="HomingAcquirePointSet"/> is true.</summary>
        public Vector3 HomingAcquirePoint { get; internal set; }

        internal bool HomingAimPointUsed { get; set; }

        /// <summary>Published homing poses for this world.</summary>
        public HomingCatalog HomingCatalog { get; }

        /// <summary>Queues an instance of an explicit child Recipe. Spawned after the current instance finishes this stage.</summary>
        public void RequestSpawn(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, default, default, SpawnCause.Other);
        }

        /// <summary>
        /// Requests a status on the current Target hit. Each request is flushed as
        /// <see cref="IProjectileWorld.StatusApplied"/> after OnHit, in list order,
        /// unless <see cref="SuppressDirectStatus"/> was set.
        /// </summary>
        public void RequestStatus(IStatusEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            requestedStatuses.Add(effect);
        }

        /// <summary>
        /// Queues an overlap sphere at <paramref name="center"/> using the current
        /// <see cref="EffectRadius"/>. Flushed after the stage as Area hits.
        /// </summary>
        public void RequestAreaOverlap(Vector3 center, float damage, IStatusEffect status, bool occupancy = false)
        {
            requestedAreas.Add(new AreaOverlapRequest(center, EffectRadius, damage, status, occupancy));
        }

        /// <summary>
        /// Queues a lingering horizontal disk at <paramref name="center"/>. Flushed after the
        /// stage as a field owned by <see cref="IProjectileWorld"/> (ADR-022).
        /// </summary>
        public void RequestGroundField(
            Vector3 center,
            float radius,
            float lifetimeSeconds,
            float damage,
            IStatusEffect status,
            float intervalSeconds,
            string presentationId = null,
            bool occupancy = false)
        {
            requestedGroundFields.Add(
                new GroundFieldPlantRequest(
                    center,
                    radius,
                    lifetimeSeconds,
                    damage,
                    status,
                    intervalSeconds,
                    presentationId,
                    occupancy));
        }

        /// <summary>Skip the Direct contact <see cref="IProjectileWorld.Hit"/> after this OnHit list.</summary>
        public void SuppressDirectHit()
        {
            suppressDirectHit = true;
        }

        /// <summary>Skip flushing Direct <see cref="IProjectileWorld.StatusApplied"/> after this OnHit list.</summary>
        public void SuppressDirectStatus()
        {
            suppressDirectStatus = true;
        }

        internal IReadOnlyList<IStatusEffect> RequestedStatuses => requestedStatuses;

        internal IReadOnlyList<HomingLockInfo> AcquiredHomingLocks => acquiredHomingLocks;

        internal void NoteHomingAcquired(HomingTarget target)
        {
            acquiredHomingLocks.Add(new HomingLockInfo
            {
                InstanceId = InstanceId,
                TargetId = target.Id,
                Position = Position,
                Kind = HomingLockKind.Acquired
            });
        }

        internal void ClearHomingLocks()
        {
            acquiredHomingLocks.Clear();
        }

        internal IReadOnlyList<AreaOverlapRequest> RequestedAreaOverlaps => requestedAreas;

        internal IReadOnlyList<GroundFieldPlantRequest> RequestedGroundFields => requestedGroundFields;

        internal bool DirectHitSuppressed => suppressDirectHit;

        internal bool DirectStatusSuppressed => suppressDirectStatus;

        internal void ClearRequestedStatus()
        {
            requestedStatuses.Clear();
        }

        internal void ClearAreaRequests()
        {
            requestedAreas.Clear();
            suppressDirectHit = false;
            suppressDirectStatus = false;
        }

        internal void ClearGroundFieldRequests()
        {
            requestedGroundFields.Clear();
        }

        /// <summary>Multicast expansions the extras queued from this stage may still run. Set before <c>OnSpawn</c>.</summary>
        internal int MulticastGenerationsForExtras { get; set; }

        /// <summary>Multiply expansions this instance may still run, itself included. Bound per stage.</summary>
        internal int MultiplyGenerationsLeft { get; set; }

        internal bool AllowsMultiply => MultiplyGenerationsLeft > 0;

        internal void RequestMulticastExtra(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, ChainBudget.Inherit(MulticastGenerationsForExtras), default, SpawnCause.Multicast, copyAimPoint: true);
        }

        internal void RequestMultiplyChild(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, default, ChainBudget.Inherit(MultiplyGenerationsLeft - 1), SpawnCause.Multiply);
        }

        void Enqueue(
            Recipe recipe,
            Vector3 position,
            Vector3 direction,
            ChainBudget multicast,
            ChainBudget multiply,
            SpawnCause cause,
            bool copyAimPoint = false)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            pendingSpawns.Add(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction,
                InheritIgnoreFrom = InstanceId,
                ParentId = InstanceId,
                Cause = cause,
                MulticastChain = multicast,
                MultiplyChain = multiply,
                HomingAcquirePointSet = copyAimPoint && HomingAcquirePointSet,
                HomingAcquirePoint = copyAimPoint ? HomingAcquirePoint : default
            });
        }

        /// <summary>Draws from the world seed. Same seed and same call order give the same sequence.</summary>
        public float NextFloat(float minInclusive, float maxExclusive)
        {
            Unity.Mathematics.Random rng = random;
            float value = rng.NextFloat(minInclusive, maxExclusive);
            random = rng;
            return value;
        }

        internal void Bind(in ModifierBindState state, bool hasHit, in HitInfo hit, float deltaTime)
        {
            MultiplyGenerationsLeft = state.MultiplyGenerationsLeft;
            InstanceId = state.InstanceId;
            Position = state.Position;
            Velocity = state.Velocity;
            Speed = state.Speed;
            Gravity = state.Gravity;
            RemainingLifetime = state.RemainingLifetime;
            RemainingPierce = state.RemainingPierce;
            PierceAtContact = state.RemainingPierce;
            ContactVelocity = state.Velocity;
            ClearPierceExit();
            RemainingBounce = state.RemainingBounce;
            Radius = state.Radius;
            Damage = state.Damage;
            CriticalChance = state.CriticalChance;
            ManaCost = state.ManaCost;
            EffectRadius = state.EffectRadius;
            Spread = state.Spread;
            IntervalSeconds = state.IntervalSeconds;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
            requestedStatuses.Clear();
            acquiredHomingLocks.Clear();
            requestedAreas.Clear();
            requestedGroundFields.Clear();
            suppressDirectHit = false;
            suppressDirectStatus = false;
            DeltaTime = deltaTime;
            HomingTarget = state.HomingTarget;
            HomingIgnoredTarget = state.HomingIgnoredTarget;
            ShooterTarget = HitTargets != null && HitTargets.TryGetShooter(state.InstanceId, out TargetId shooter)
                ? HomingTarget.Assigned(shooter)
                : default;
            HomingAcquirePointSet = state.HomingAcquirePointSet;
            HomingAcquirePoint = state.HomingAcquirePoint;
            HomingAimPointUsed = state.HomingAimPointUsed;
        }

        internal void ApplyTo(ref Projectile projectile, ref ProjectileVelocity velocity)
        {
            projectile.Speed = Speed;
            projectile.Gravity = Gravity;
            projectile.Radius = Radius;
            projectile.RemainingLifetime = RemainingLifetime;
            projectile.RemainingPierce = RemainingPierce;
            projectile.RemainingBounce = RemainingBounce;
            projectile.Damage = Damage;
            projectile.CriticalChance = CriticalChance;
            projectile.ManaCost = ManaCost;
            projectile.EffectRadius = EffectRadius;
            projectile.Spread = Spread;
            projectile.IntervalSeconds = IntervalSeconds;
            projectile.HomingHasTarget = HomingTarget.IsAssigned;
            projectile.HomingTargetId = HomingTarget.Id;
            projectile.HomingHasIgnoredTarget = HomingIgnoredTarget.IsAssigned;
            projectile.HomingIgnoredTargetId = HomingIgnoredTarget.Id;
            projectile.HomingAcquirePointSet = HomingAcquirePointSet;
            projectile.HomingAcquirePoint = new Unity.Mathematics.float3(
                HomingAcquirePoint.x,
                HomingAcquirePoint.y,
                HomingAcquirePoint.z);
            projectile.HomingAimPointUsed = HomingAimPointUsed;
            velocity.Value = new Unity.Mathematics.float3(Velocity.x, Velocity.y, Velocity.z);
        }
    }

    readonly struct AreaOverlapRequest
    {
        public AreaOverlapRequest(Vector3 center, float radius, float damage, IStatusEffect status, bool occupancy = false)
        {
            Center = center;
            Radius = radius;
            Damage = damage;
            Status = status;
            Occupancy = occupancy;
        }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float Damage { get; }

        public IStatusEffect Status { get; }

        public bool Occupancy { get; }
    }

    readonly struct GroundFieldPlantRequest
    {
        public GroundFieldPlantRequest(
            Vector3 center,
            float radius,
            float lifetimeSeconds,
            float damage,
            IStatusEffect status,
            float intervalSeconds,
            string presentationId,
            bool occupancy = false)
        {
            Center = center;
            Radius = radius;
            LifetimeSeconds = lifetimeSeconds;
            Damage = damage;
            Status = status;
            IntervalSeconds = intervalSeconds;
            PresentationId = presentationId;
            Occupancy = occupancy;
        }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float LifetimeSeconds { get; }

        public float Damage { get; }

        public IStatusEffect Status { get; }

        public float IntervalSeconds { get; }

        public string PresentationId { get; }

        public bool Occupancy { get; }
    }

    internal struct ModifierBindState
    {
        public ProjectileId InstanceId;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Speed;
        public float Gravity;
        public float RemainingLifetime;
        public int RemainingPierce;
        public int RemainingBounce;
        public float Radius;
        public float Damage;
        public float CriticalChance;
        public float ManaCost;
        public float EffectRadius;
        public float Spread;
        public float IntervalSeconds;
        public int MultiplyGenerationsLeft;
        public HomingTarget HomingTarget;
        public HomingTarget HomingIgnoredTarget;
        public bool HomingAcquirePointSet;
        public Vector3 HomingAcquirePoint;
        public bool HomingAimPointUsed;
    }
}
