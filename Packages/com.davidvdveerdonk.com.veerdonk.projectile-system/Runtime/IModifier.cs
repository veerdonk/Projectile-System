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

        internal void Bind(LiveProjectile projectile, bool hasHit, in HitInfo hit)
        {
            InstanceId = projectile.Id;
            Position = projectile.Position;
            Velocity = projectile.Velocity;
            Speed = projectile.Speed;
            RemainingLifetime = projectile.RemainingLifetime;
            Radius = projectile.Radius;
            HasHit = hasHit;
            Hit = hit;
            CancelDespawn = false;
        }

        internal void ApplyTo(LiveProjectile projectile)
        {
            projectile.Velocity = Velocity;
            projectile.Speed = Speed;
            projectile.RemainingLifetime = RemainingLifetime;
        }
    }
}
