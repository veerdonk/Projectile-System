using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Why an instance came into the world. Zero is <see cref="Game"/>.</summary>
    public enum SpawnCause
    {
        /// <summary>The game called <see cref="IProjectileWorld.Spawn"/>.</summary>
        Game,
        /// <summary>A Multicast extra.</summary>
        Multicast,
        /// <summary>A Multiply child.</summary>
        Multiply,
        /// <summary>Any other package-made spawn, such as <see cref="ModifierContext.RequestSpawn"/>.</summary>
        Other
    }

    /// <summary>Why an instance returned to the pool.</summary>
    public enum ExpireReason
    {
        /// <summary>Lifetime ran out.</summary>
        Lifetime,
        /// <summary>A Direct hit ended the instance, including a Hitscan ray that ended on a hit.</summary>
        Hit,
        /// <summary>A <see cref="ProtectionLevel.Strong"/> contact ate it. No Direct Hit is raised.</summary>
        Absorbed,
        /// <summary>The game called <see cref="IProjectileWorld.Despawn"/>.</summary>
        Despawned,
        /// <summary>A Hitscan ray finished without a killing hit.</summary>
        HitscanEnd,
        /// <summary>Released any other way.</summary>
        Other
    }

    /// <summary>What happened to the shot after a Direct <see cref="HitInfo"/>. Area, occupancy, and ground hits are <see cref="None"/>.</summary>
    public enum HitOutcome
    {
        /// <summary>Not a Direct hit.</summary>
        None,
        /// <summary>The shot ends (or, for a Beam pulse, the pulse ends).</summary>
        Ended,
        /// <summary>Pierce spent a charge and the shot flies on.</summary>
        Pierced,
        /// <summary>Bounce spent a charge and reflected the shot.</summary>
        Bounced,
        /// <summary>Another modifier cancelled the despawn and the shot continues.</summary>
        Continued
    }

    /// <summary>How a Homing lock was obtained.</summary>
    public enum HomingLockKind
    {
        /// <summary>Assigned at spawn through <see cref="SpawnRequest.HomingTarget"/>, or set on a live Beam through the four-argument <c>AimBeam</c>.</summary>
        Assigned,
        /// <summary>Acquired or re-acquired by Homing.</summary>
        Acquired
    }

    /// <summary>Payload for <see cref="IProjectileWorld.Spawned"/>.</summary>
    public struct SpawnInfo
    {
        /// <summary>The new instance.</summary>
        public ProjectileId InstanceId;
        /// <summary>The instance whose modifier queued this spawn. Default for a game spawn.</summary>
        public ProjectileId ParentId;
        /// <summary>Why it spawned.</summary>
        public SpawnCause Cause;
        /// <summary>World-space spawn position.</summary>
        public Vector3 Position;
        /// <summary>Normalized launch direction after the Arc solve, before OnSpawn modifiers run.</summary>
        public Vector3 Direction;
        /// <summary>Definition for this instance (see <see cref="Recipe.Id"/> and <see cref="Recipe.PresentationId"/>).</summary>
        public Recipe Recipe;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.HomingLocked"/>.</summary>
    public struct HomingLockInfo
    {
        /// <summary>Instance that got the lock.</summary>
        public ProjectileId InstanceId;
        /// <summary>Locked target.</summary>
        public TargetId TargetId;
        /// <summary>The shot's position when the lock was obtained.</summary>
        public Vector3 Position;
        /// <summary>Assigned at spawn or by the four-argument <c>AimBeam</c>, or acquired.</summary>
        public HomingLockKind Kind;
    }
}
