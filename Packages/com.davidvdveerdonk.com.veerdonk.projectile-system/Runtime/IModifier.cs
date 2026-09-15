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
        Unity.Mathematics.Random random;

        internal ModifierContext(List<SpawnRequest> pendingSpawns, uint seed = 1u)
        {
            this.pendingSpawns = pendingSpawns ?? throw new ArgumentNullException(nameof(pendingSpawns));
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

        public bool HasHit { get; internal set; }

        public HitInfo Hit { get; internal set; }

        public bool CancelDespawn { get; set; }

        /// <summary>Queues an instance of an explicit child Recipe. Spawned after the current instance finishes this stage.</summary>
        public void RequestSpawn(Recipe recipe, Vector3 position, Vector3 direction)
        {
            RequestSpawn(recipe, position, direction, suppressMulticast: false);
        }

        internal void RequestSpawn(Recipe recipe, Vector3 position, Vector3 direction, bool suppressMulticast)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            pendingSpawns.Add(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction,
                InheritIgnoreFrom = InstanceId,
                SuppressMulticast = suppressMulticast
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
            bool hasHit,
            in HitInfo hit)
        {
            InstanceId = instanceId;
            Position = position;
            Velocity = velocity;
            Speed = speed;
            RemainingLifetime = remainingLifetime;
            RemainingPierce = remainingPierce;
            RemainingBounce = remainingBounce;
            Radius = radius;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
        }

        internal void ApplyTo(ref Projectile projectile, ref ProjectileVelocity velocity)
        {
            projectile.Speed = Speed;
            projectile.RemainingLifetime = RemainingLifetime;
            projectile.RemainingPierce = RemainingPierce;
            projectile.RemainingBounce = RemainingBounce;
            velocity.Value = new Unity.Mathematics.float3(Velocity.x, Velocity.y, Velocity.z);
        }
    }
}
