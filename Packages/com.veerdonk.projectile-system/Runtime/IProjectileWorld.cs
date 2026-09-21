using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
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

        /// <summary>Publishes a live pose Homing can lock onto and steer toward. Call again to move it.</summary>
        void SetHomingCandidate(TargetId id, Vector3 position);

        /// <summary>Drops a previously published homing pose. Locked instances that used it fly straight.</summary>
        void RemoveHomingCandidate(TargetId id);

        /// <summary>Raised for each hit produced by the query pipeline.</summary>
        event Action<HitInfo> Hit;

        /// <summary>Raised when an instance is returned to the pool.</summary>
        event Action<ExpireInfo> Expired;

        /// <summary>
        /// Raised after the OnHit list (and after area flush) when a status was requested.
        /// The package does not apply the effect.
        /// </summary>
        event Action<StatusInfo> StatusApplied;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.StatusApplied"/>.</summary>
    public struct StatusInfo
    {
        /// <summary>Instance that requested the status.</summary>
        public ProjectileId InstanceId;
        /// <summary>Target that was hit.</summary>
        public TargetId TargetId;
        /// <summary>Effect to apply. The package does not call <see cref="IStatusReceiver"/>.</summary>
        public IStatusEffect Effect;
    }

    /// <summary>Input to <see cref="IProjectileWorld.Spawn"/>.</summary>
    public struct SpawnRequest
    {
        /// <summary>Definition for this instance. Not mutated by the world.</summary>
        public Recipe Recipe;
        /// <summary>World-space spawn position.</summary>
        public Vector3 Position;
        /// <summary>Launch direction. Does not need to be normalized.</summary>
        public Vector3 Direction;
        /// <summary>Optional lock. When assigned, Homing skips acquisition.</summary>
        public HomingTarget HomingTarget;
        internal ProjectileId InheritIgnoreFrom;
        internal ChainBudget MulticastChain;
        internal ChainBudget MultiplyChain;
    }

    /// <summary>Opaque handle for a live instance.</summary>
    public readonly struct ProjectileId : IEquatable<ProjectileId>
    {
        readonly int value;

        /// <summary>Wraps a world-assigned integer. Do not mint these from game code.</summary>
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

    /// <summary>
    /// Homing lock for one instance. When <see cref="IsAssigned"/>, Homing uses this id and does not acquire.
    /// The pose still comes from <see cref="IProjectileWorld.SetHomingCandidate"/>.
    /// </summary>
    public struct HomingTarget
    {
        /// <summary>False means no lock; Homing may acquire.</summary>
        public bool IsAssigned;
        /// <summary>Published through <see cref="IProjectileWorld.SetHomingCandidate"/>.</summary>
        public TargetId Id;

        /// <summary>Assigned lock on <paramref name="id"/>.</summary>
        public static HomingTarget Assigned(TargetId id)
        {
            return new HomingTarget { IsAssigned = true, Id = id };
        }
    }

    /// <summary>Payload for <see cref="IProjectileWorld.Expired"/>.</summary>
    public struct ExpireInfo
    {
        /// <summary>Instance that returned to the pool.</summary>
        public ProjectileId InstanceId;
    }
}
