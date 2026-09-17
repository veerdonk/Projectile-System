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

        public float RemainingLifetime { get; set; }

        public int RemainingPierce { get; set; }

        public int RemainingBounce { get; set; }

        public float Radius { get; internal set; }

        /// <summary>Instance damage, copied from the Recipe at spawn. Sampled onto <see cref="HitInfo"/> at contact.</summary>
        public float Damage { get; set; }

        /// <summary>Instance critical chance 0–1, copied from the Recipe at spawn. The package does not roll it.</summary>
        public float CriticalChance { get; set; }

        /// <summary>
        /// Instance mana-cost copy, taken from <see cref="Recipe.ManaCost"/> at spawn.
        /// <see cref="Mana"/> mutates this; the Recipe field stays the authored fire-time cost.
        /// </summary>
        public float ManaCost { get; set; }

        /// <summary>Instance area-effect radius, copied from the Recipe at spawn.</summary>
        public float EffectRadius { get; set; }

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
        /// <see cref="IProjectileWorld.StatusApplied"/> after OnHit, in list order.
        /// </summary>
        public void RequestStatus(IStatusEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            requestedStatuses.Add(effect);
        }

        internal IReadOnlyList<IStatusEffect> RequestedStatuses => requestedStatuses;

        internal void ClearRequestedStatus()
        {
            requestedStatuses.Clear();
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
            float remainingLifetime,
            int remainingPierce,
            int remainingBounce,
            float radius,
            float damage,
            float criticalChance,
            float manaCost,
            float effectRadius,
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
            RemainingLifetime = remainingLifetime;
            RemainingPierce = remainingPierce;
            RemainingBounce = remainingBounce;
            Radius = radius;
            Damage = damage;
            CriticalChance = criticalChance;
            ManaCost = manaCost;
            EffectRadius = effectRadius;
            IntervalSeconds = intervalSeconds;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
            requestedStatuses.Clear();
            DeltaTime = deltaTime;
            HomingTarget = homingTarget;
            HomingIgnoredTarget = homingIgnoredTarget;
        }

        internal void ApplyTo(ref Projectile projectile, ref ProjectileVelocity velocity)
        {
            projectile.Speed = Speed;
            projectile.RemainingLifetime = RemainingLifetime;
            projectile.RemainingPierce = RemainingPierce;
            projectile.RemainingBounce = RemainingBounce;
            projectile.Damage = Damage;
            projectile.CriticalChance = CriticalChance;
            projectile.ManaCost = ManaCost;
            projectile.EffectRadius = EffectRadius;
            projectile.IntervalSeconds = IntervalSeconds;
            projectile.HomingHasTarget = HomingTarget.IsAssigned;
            projectile.HomingTargetId = HomingTarget.Id;
            projectile.HomingHasIgnoredTarget = HomingIgnoredTarget.IsAssigned;
            projectile.HomingIgnoredTargetId = HomingIgnoredTarget.Id;
            velocity.Value = new Unity.Mathematics.float3(Velocity.x, Velocity.y, Velocity.z);
        }
    }
}
