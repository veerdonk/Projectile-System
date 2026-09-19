using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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
        readonly List<AreaOverlapRequest> requestedAreas = new List<AreaOverlapRequest>();
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

        public ProjectileId InstanceId { get; internal set; }

        public Vector3 Position { get; internal set; }

        public Vector3 Velocity { get; set; }

        public float Speed { get; set; }

        /// <summary>
        /// Instance gravity in m/s², copied from <see cref="Recipe.Gravity"/> at spawn.
        /// Burst applies it each tick; a later scale modifier writes this copy, not the Recipe.
        /// </summary>
        public float Gravity { get; set; }

        public float RemainingLifetime { get; set; }

        public int RemainingPierce { get; set; }

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

        public bool HasHit { get; internal set; }

        public HitInfo Hit { get; internal set; }

        public bool CancelDespawn { get; set; }

        /// <summary>Seconds this stage is advancing.</summary>
        public float DeltaTime { get; internal set; }

        /// <summary>Lock for this instance. Homing writes here after a successful acquire.</summary>
        public HomingTarget HomingTarget { get; set; }

        /// <summary>Last Target hit. Acquisition skips it while it stays in front of travel.</summary>
        public HomingTarget HomingIgnoredTarget { get; set; }

        public HomingCatalog HomingCatalog { get; }

        /// <summary>Queues an instance of an explicit child Recipe. Spawned after the current instance finishes this stage.</summary>
        public void RequestSpawn(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, default, default);
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
        public void RequestAreaOverlap(Vector3 center, float damage, IStatusEffect status)
        {
            requestedAreas.Add(new AreaOverlapRequest(center, EffectRadius, damage, status));
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

        internal IReadOnlyList<AreaOverlapRequest> RequestedAreaOverlaps => requestedAreas;

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

        /// <summary>Multicast expansions the extras queued from this stage may still run. Set before <c>OnSpawn</c>.</summary>
        internal int MulticastGenerationsForExtras { get; set; }

        /// <summary>Multiply expansions this instance may still run, itself included. Bound per stage.</summary>
        internal int MultiplyGenerationsLeft { get; set; }

        internal bool AllowsMultiply => MultiplyGenerationsLeft > 0;

        internal void RequestMulticastExtra(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, ChainBudget.Inherit(MulticastGenerationsForExtras), default);
        }

        internal void RequestMultiplyChild(Recipe recipe, Vector3 position, Vector3 direction)
        {
            Enqueue(recipe, position, direction, default, ChainBudget.Inherit(MultiplyGenerationsLeft - 1));
        }

        void Enqueue(Recipe recipe, Vector3 position, Vector3 direction, ChainBudget multicast, ChainBudget multiply)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            pendingSpawns.Add(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction,
                InheritIgnoreFrom = InstanceId,
                MulticastChain = multicast,
                MultiplyChain = multiply
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

        internal void Bind(
            ProjectileId instanceId,
            Vector3 position,
            Vector3 velocity,
            float speed,
            float gravity,
            float remainingLifetime,
            int remainingPierce,
            int remainingBounce,
            float radius,
            float damage,
            float criticalChance,
            float manaCost,
            float effectRadius,
            float spread,
            float intervalSeconds,
            int multiplyGenerationsLeft,
            bool hasHit,
            in HitInfo hit,
            float deltaTime,
            HomingTarget homingTarget,
            HomingTarget homingIgnoredTarget)
        {
            MultiplyGenerationsLeft = multiplyGenerationsLeft;
            InstanceId = instanceId;
            Position = position;
            Velocity = velocity;
            Speed = speed;
            Gravity = gravity;
            RemainingLifetime = remainingLifetime;
            RemainingPierce = remainingPierce;
            RemainingBounce = remainingBounce;
            Radius = radius;
            Damage = damage;
            CriticalChance = criticalChance;
            ManaCost = manaCost;
            EffectRadius = effectRadius;
            Spread = spread;
            IntervalSeconds = intervalSeconds;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
            requestedStatuses.Clear();
            requestedAreas.Clear();
            suppressDirectHit = false;
            suppressDirectStatus = false;
            DeltaTime = deltaTime;
            HomingTarget = homingTarget;
            HomingIgnoredTarget = homingIgnoredTarget;
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
            velocity.Value = new Unity.Mathematics.float3(Velocity.x, Velocity.y, Velocity.z);
        }
    }

    readonly struct AreaOverlapRequest
    {
        public AreaOverlapRequest(Vector3 center, float radius, float damage, IStatusEffect status)
        {
            Center = center;
            Radius = radius;
            Damage = damage;
            Status = status;
        }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float Damage { get; }

        public IStatusEffect Status { get; }
    }
}
