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

        internal ModifierContext(List<SpawnRequest> pendingSpawns)
        {
            this.pendingSpawns = pendingSpawns ?? throw new ArgumentNullException(nameof(pendingSpawns));
        }

        public ProjectileId InstanceId { get; internal set; }

        public Vector3 Position { get; internal set; }

        public Vector3 Velocity { get; set; }

        public float Speed { get; set; }

        public float RemainingLifetime { get; set; }

        public float Radius { get; internal set; }

        public bool HasHit { get; internal set; }

        public HitInfo Hit { get; internal set; }

        public bool CancelDespawn { get; set; }

        /// <summary>Queues an instance of an explicit child Recipe. Spawned after the current instance finishes this stage.</summary>
        public void RequestSpawn(Recipe recipe, Vector3 position, Vector3 direction)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            pendingSpawns.Add(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction
            });
        }

        internal void Bind(
            ProjectileId instanceId,
            Vector3 position,
            Vector3 velocity,
            float speed,
            float remainingLifetime,
            float radius,
            bool hasHit,
            in HitInfo hit)
        {
            InstanceId = instanceId;
            Position = position;
            Velocity = velocity;
            Speed = speed;
            RemainingLifetime = remainingLifetime;
            Radius = radius;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
        }

        internal void ApplyTo(ref Projectile projectile, ref ProjectileVelocity velocity)
        {
            projectile.Speed = Speed;
            projectile.RemainingLifetime = RemainingLifetime;
            velocity.Value = new Unity.Mathematics.float3(Velocity.x, Velocity.y, Velocity.z);
        }
    }
}
