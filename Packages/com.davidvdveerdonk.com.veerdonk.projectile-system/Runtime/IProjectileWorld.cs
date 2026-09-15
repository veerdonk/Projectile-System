using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Façade for spawn, simulation, hits, and expire. Instances do not tick themselves.
    /// </summary>
    public interface IProjectileWorld
    {
        /// <summary>Rents an instance from the pool and starts it from <paramref name="request"/>.</summary>
        ProjectileId Spawn(in SpawnRequest request);

        /// <summary>Advances all live instances by <paramref name="dt"/> seconds.</summary>
        void Tick(float dt);

        /// <summary>Raised for each hit produced by the query pipeline.</summary>
        event Action<HitInfo> Hit;

        /// <summary>Raised when an instance is returned to the pool.</summary>
        event Action<ExpireInfo> Expired;
    }

    /// <summary>Input to <see cref="IProjectileWorld.Spawn"/>.</summary>
    public struct SpawnRequest
    {
        public Recipe Recipe;
        public Vector3 Position;
        public Vector3 Direction;
        public HomingTarget HomingTarget;
        internal ProjectileId InheritIgnoreFrom;
    }

    /// <summary>Opaque handle for a live instance.</summary>
    public readonly struct ProjectileId : IEquatable<ProjectileId>
    {
        readonly int value;

        public ProjectileId(int value)
        {
            this.value = value;
        }

        public bool Equals(ProjectileId other) => value == other.value;

        public override bool Equals(object obj) => obj is ProjectileId other && Equals(other);

        public override int GetHashCode() => value;

        public static bool operator ==(ProjectileId left, ProjectileId right) => left.Equals(right);

        public static bool operator !=(ProjectileId left, ProjectileId right) => !left.Equals(right);

        public override string ToString() => value.ToString();
    }

    /// <summary>Optional homing target on spawn. Ignored until phase 5.</summary>
    public struct HomingTarget
    {
        public bool IsAssigned;
        public int Id;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.Expired"/>.</summary>
    public struct ExpireInfo
    {
        public ProjectileId InstanceId;
    }
}
