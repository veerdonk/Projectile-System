using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Opaque handle for a planted ground field.</summary>
    public readonly struct GroundFieldId : IEquatable<GroundFieldId>
    {
        readonly int value;

        /// <summary>Wraps a world-assigned integer. Do not mint these from game code.</summary>
        public GroundFieldId(int value)
        {
            this.value = value;
        }

        public bool Equals(GroundFieldId other) => value == other.value;

        public override bool Equals(object obj) => obj is GroundFieldId other && Equals(other);

        public override int GetHashCode() => value;

        public static bool operator ==(GroundFieldId left, GroundFieldId right) => left.Equals(right);

        public static bool operator !=(GroundFieldId left, GroundFieldId right) => !left.Equals(right);

        public override string ToString() => value.ToString();
    }

    /// <summary>Why a ground field ended. See <see cref="GroundFieldInfo.Reason"/>.</summary>
    public enum GroundFieldEndReason
    {
        /// <summary>The lifetime ran out.</summary>
        Expired,
        /// <summary>The world was disposed while the field was live.</summary>
        Disposed
    }

    /// <summary>Payload for <see cref="IProjectileWorld.GroundFieldPlanted"/> and <see cref="IProjectileWorld.GroundFieldEnded"/>.</summary>
    public struct GroundFieldInfo
    {
        /// <summary>Handle assigned at plant. The same id appears on Planted, every Pulsed, and Ended.</summary>
        public GroundFieldId FieldId;
        /// <summary>Instance that planted the field.</summary>
        public ProjectileId SourceId;
        /// <summary>Disk center, already on the ground plane.</summary>
        public Vector3 Center;
        /// <summary>Disk radius in meters.</summary>
        public float Radius;
        /// <summary>Lifetime requested at plant.</summary>
        public float LifetimeSeconds;
        /// <summary>Pulse interval requested at plant. 0 means no pulses.</summary>
        public float IntervalSeconds;
        /// <summary>True when the field raises enter/exit hits instead of pulses.</summary>
        public bool Occupancy;
        /// <summary>Presentation key passed to the view factory. May be null.</summary>
        public string PresentationId;
        /// <summary>Meaningful on Ended only. Planted leaves it at <see cref="GroundFieldEndReason.Expired"/>.</summary>
        public GroundFieldEndReason Reason;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.GroundFieldPulsed"/>.</summary>
    public struct GroundFieldPulseInfo
    {
        /// <summary>Field that pulsed.</summary>
        public GroundFieldId FieldId;
        /// <summary>Instance that planted the field.</summary>
        public ProjectileId SourceId;
        /// <summary>Disk center.</summary>
        public Vector3 Center;
        /// <summary>Disk radius in meters.</summary>
        public float Radius;
        /// <summary>Distinct Targets that raised an Area <see cref="HitInfo"/> this pulse. 0 for an empty disk.</summary>
        public int TargetsHit;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.AreaBurst"/>.</summary>
    public struct AreaBurstInfo
    {
        /// <summary>Instance whose AreaEffect ran.</summary>
        public ProjectileId InstanceId;
        /// <summary>Overlap center: contact point on OnHit, instance position otherwise.</summary>
        public Vector3 Center;
        /// <summary>Overlap radius (<c>EffectRadius</c>).</summary>
        public float Radius;
        /// <summary>Stage that requested the overlap: OnHit, OnTravel, OnInterval, or OnTick.</summary>
        public RecipeStage Stage;
        /// <summary>True for an occupancy overlap, which raises this only when it is first made.</summary>
        public bool Occupancy;
    }
}
