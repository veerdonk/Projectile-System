using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Façade for spawn, simulation, hits, and expire. Instances do not tick themselves.
    /// </summary>
    public interface IProjectileWorld
    {
        /// <summary>
        /// Rents an instance from the pool and starts it from <paramref name="request"/>.
        /// <see cref="Delivery.Arc"/> with <see cref="SpawnRequest.HomingAcquirePointSet"/> calls
        /// <see cref="BallisticAim.TrySolve"/> before OnSpawn. Unreachable returns <c>default</c>
        /// and does not raise <see cref="Expired"/>.
        /// <see cref="Delivery.Hitscan"/> runs its polyline inside this call and returns the instance
        /// to the pool before returning, after <see cref="Hit"/> and <see cref="Expired"/> have been raised.
        /// <see cref="Delivery.Beam"/> runs the first pulse inside this call and stays live.
        /// </summary>
        ProjectileId Spawn(in SpawnRequest request);

        /// <summary>Advances all live instances by <paramref name="dt"/> seconds. Beam pulses that are due run here.</summary>
        void Tick(float dt);

        /// <summary>
        /// Runs OnExpire, returns the instance to the pool, and raises <see cref="Expired"/>.
        /// Unknown or already pooled ids do nothing. Allowed on any live instance, including Beam.
        /// </summary>
        void Despawn(ProjectileId id);

        /// <summary>
        /// Moves a live Beam's origin and direction and redraws the polyline. Does not raise
        /// <see cref="Hit"/>. Damage still follows <see cref="Recipe.BeamPulseSeconds"/>.
        /// Other deliveries and unknown ids do nothing. The package does not read the mouse;
        /// the caller copies the current muzzle pose.
        /// </summary>
        void AimBeam(ProjectileId id, Vector3 position, Vector3 direction);

        /// <summary>Publishes a live pose Homing can lock onto and steer toward. Call again to move it.</summary>
        void SetHomingCandidate(TargetId id, Vector3 position);

        /// <summary>Drops a previously published homing pose. Locked instances that used it fly straight.</summary>
        void RemoveHomingCandidate(TargetId id);

        /// <summary>
        /// Publishes a sphere that Direct casts, <c>AreaEffect</c>, and <c>GroundField</c> treat as
        /// <see cref="HitKind.Target"/>. Call again to move, resize, or change <paramref name="protection"/>.
        /// <paramref name="radius"/> &lt;= 0 removes it. Homing also reads this pose. The same id in
        /// <see cref="SetHomingCandidate"/> is one candidate, and this position wins.
        /// <see cref="ProtectionLevel.Basic"/> matches a Basic collider. <see cref="ProtectionLevel.Strong"/> eats the shot.
        /// </summary>
        void SetHitTarget(TargetId id, Vector3 position, float radius, ProtectionLevel protection = ProtectionLevel.None);

        /// <summary>Drops a previously published hit volume.</summary>
        void RemoveHitTarget(TargetId id);

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
        /// <summary>
        /// When true, Homing’s first lock is the catalog candidate closest to <see cref="HomingAcquirePoint"/>.
        /// The game copies a world point (the package does not read the mouse). Later empty locks still use travel direction.
        /// </summary>
        public bool HomingAcquirePointSet;
        /// <summary>Fire-time world point. Ignored unless <see cref="HomingAcquirePointSet"/> is true.</summary>
        public Vector3 HomingAcquirePoint;
        /// <summary>
        /// Collider that fired this shot. Direct overlap and sweep skip it until the instance body no longer overlaps it.
        /// That skip is not a hit. After the body is clear, a later contact is a normal hit. Null is no skip.
        /// Multicast extras and multiply children copy the skip. Area and ground queries are unchanged.
        /// </summary>
        public Collider IgnoreCollider;
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
