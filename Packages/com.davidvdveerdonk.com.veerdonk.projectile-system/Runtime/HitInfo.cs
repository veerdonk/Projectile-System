using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>Classification of a hit from the query pipeline.</summary>
    public enum HitKind
    {
        Target,
        World
    }

    /// <summary>How a <see cref="HitInfo"/> was produced. Direct is the movement cast; Area is an overlap.</summary>
    public enum HitSource
    {
        Direct,
        Area
    }

    /// <summary>Opaque sandbox/game id stored on a <see cref="HitKind.Target"/> hit.</summary>
    public readonly struct TargetId : IEquatable<TargetId>
    {
        readonly int value;

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
        public ProjectileId InstanceId;
        public HitKind Kind;
        public HitSource Source;
        public Vector3 Point;
        public Vector3 Normal;
        public TargetId TargetId;
        public float Damage;
        public float CriticalChance;
        public float EffectRadius;
    }
}
