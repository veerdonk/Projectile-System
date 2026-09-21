using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Classification of a hit from the query pipeline.</summary>
    public enum HitKind
    {
        /// <summary>Collider marked <see cref="IDamageable"/>.</summary>
        Target,
        /// <summary>Anything else the SphereCast hit (walls, unmarked colliders).</summary>
        World
    }

    /// <summary>How a <see cref="HitInfo"/> was produced. Direct is the movement cast; Area is an AreaEffect or GroundField overlap.</summary>
    public enum HitSource
    {
        /// <summary>Movement SphereCast contact.</summary>
        Direct,
        /// <summary>AreaEffect sphere or GroundField disk overlap.</summary>
        Area
    }

    /// <summary>
    /// Collider marker level from <see cref="IProjectileProtection"/>. None is an unmarked collider.
    /// Basic still runs OnHit; Strong eats the instance.
    /// </summary>
    public enum ProtectionLevel
    {
        /// <summary>No protection marker.</summary>
        None,
        /// <summary>OnHit still runs; Direct Hit is raised; ApplyStatus for this contact is skipped.</summary>
        Basic,
        /// <summary>Instance returns to the pool immediately. No Direct Hit, OnHit, or OnExpire.</summary>
        Strong
    }

    /// <summary>Opaque sandbox/game id stored on a <see cref="HitKind.Target"/> hit.</summary>
    public readonly struct TargetId : IEquatable<TargetId>
    {
        readonly int value;

        /// <summary>Wraps a game-chosen integer. Equality is by value.</summary>
        public TargetId(int value)
        {
            this.value = value;
        }

        public bool Equals(TargetId other) => value == other.value;

        public override bool Equals(object obj) => obj is TargetId other && Equals(other);

        public override int GetHashCode() => value;

        public static bool operator ==(TargetId left, TargetId right) => left.Equals(right);

        public static bool operator !=(TargetId left, TargetId right) => !left.Equals(right);

        public override string ToString() => value.ToString();
    }

    /// <summary>One contact from SphereCast / overlap. Combat stats are the instance values at contact.</summary>
    public struct HitInfo
    {
        /// <summary>Instance that produced this contact.</summary>
        public ProjectileId InstanceId;
        /// <summary>Target vs World classification.</summary>
        public HitKind Kind;
        /// <summary>Direct cast vs area overlap.</summary>
        public HitSource Source;
        /// <summary>From <see cref="IProjectileProtection"/>, else None.</summary>
        public ProtectionLevel Protection;
        /// <summary>World-space contact or overlap point.</summary>
        public Vector3 Point;
        /// <summary>Surface normal at the contact. Bounce reflects around this.</summary>
        public Vector3 Normal;
        /// <summary>From <see cref="IDamageable"/> when <see cref="Kind"/> is Target.</summary>
        public TargetId TargetId;
        /// <summary>Instance damage at sample time (Direct) or modifier damage (Area).</summary>
        public float Damage;
        /// <summary>Instance critical chance at sample time. The package does not roll it.</summary>
        public float CriticalChance;
        /// <summary>Instance effect radius at sample time.</summary>
        public float EffectRadius;
    }
}
