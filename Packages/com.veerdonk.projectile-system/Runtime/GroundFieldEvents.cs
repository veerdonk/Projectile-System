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
        /// <summary>
        /// Disk center on the ground: the point <see cref="IProjectileWorld.GroundFieldSurface"/> returned,
        /// or the plant point at Y = 0 without one. For a rectangle or sector, the anchor (the middle of the near edge, or
        /// the apex): the plant point moved straight up or down onto the plane the surface returned for the shape's middle.
        /// </summary>
        public Vector3 Center;
        /// <summary>Unit ground normal the field lies across. <see cref="Vector3.up"/> without a ground surface.</summary>
        public Vector3 Normal;
        /// <summary>
        /// Disk radius in meters, measured in the ground plane. For a rectangle or sector, the distance from
        /// <see cref="Center"/> to the shape's farthest point.
        /// </summary>
        public float Radius;
        /// <summary>Height margin requested at plant (<see cref="GroundField.HeightMargin"/>). 0 means height does not count.</summary>
        public float HeightMargin;
        /// <summary>Lifetime requested at plant.</summary>
        public float LifetimeSeconds;
        /// <summary>Pulse period used: the requested value, raised to 0.01 s when below. 0 means no pulses.</summary>
        public float IntervalSeconds;
        /// <summary>True when the field raises enter/exit hits instead of pulses.</summary>
        public bool Occupancy;
        /// <summary>Presentation key passed to the view factory. May be null.</summary>
        public string PresentationId;
        /// <summary>Meaningful on Ended only. Planted leaves it at <see cref="GroundFieldEndReason.Expired"/>.</summary>
        public GroundFieldEndReason Reason;
        /// <summary>Outline of the field (<see cref="GroundField.Shape"/>). Disk for every field planted before shapes.</summary>
        public GroundFieldShape Shape;
        /// <summary>
        /// Unit direction in the field's plane that a rectangle or sector points along; its horizontal part is the planted
        /// direction. A disk reports it too, though it has no orientation.
        /// </summary>
        public Vector3 Direction;
        /// <summary>Rectangle length or sector radius in meters, in the plane. 0 for a disk.</summary>
        public float Length;
        /// <summary>Rectangle width in meters. 0 for a disk or sector.</summary>
        public float Width;
        /// <summary>Sector total angle in degrees, capped at 360. 0 for a disk or rectangle.</summary>
        public float AngleDegrees;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.GroundFieldPulsed"/>.</summary>
    public struct GroundFieldPulseInfo
    {
        /// <summary>Field that pulsed.</summary>
        public GroundFieldId FieldId;
        /// <summary>Instance that planted the field.</summary>
        public ProjectileId SourceId;
        /// <summary>Field center or anchor on the ground, the same as <see cref="GroundFieldInfo.Center"/>.</summary>
        public Vector3 Center;
        /// <summary>Unit ground normal, the same as <see cref="GroundFieldInfo.Normal"/>.</summary>
        public Vector3 Normal;
        /// <summary>Disk radius in meters, or the shape's reach from <see cref="Center"/> (<see cref="GroundFieldInfo.Radius"/>).</summary>
        public float Radius;
        /// <summary>Distinct Targets that raised an Area <see cref="HitInfo"/> this pulse. 0 for an empty field.</summary>
        public int TargetsHit;
        /// <summary>Outline of the field the same as <see cref="GroundFieldInfo.Shape"/>.</summary>
        public GroundFieldShape Shape;
        /// <summary>
        /// Unit direction in the field's plane that a rectangle or sector points along; its horizontal part is the planted
        /// direction. A disk reports it too, though it has no orientation.
        /// </summary>
        public Vector3 Direction;
        /// <summary>Rectangle length or sector radius in meters, in the plane. 0 for a disk.</summary>
        public float Length;
        /// <summary>Rectangle width in meters. 0 for a disk or sector.</summary>
        public float Width;
        /// <summary>Sector total angle in degrees, capped at 360. 0 for a disk or rectangle.</summary>
        public float AngleDegrees;
    }

    /// <summary>Payload for <see cref="IProjectileWorld.AreaBurst"/>.</summary>
    public struct AreaBurstInfo
    {
        /// <summary>Instance whose AreaEffect ran.</summary>
        public ProjectileId InstanceId;
        /// <summary>Overlap center: contact point on OnHit, instance position otherwise.</summary>
        public Vector3 Center;
        /// <summary>
        /// Overlap radius: the AreaEffect's own <see cref="AreaEffect.Radius"/> times the instance's EffectRadius scales,
        /// or the instance <c>EffectRadius</c> when that Radius is 0.
        /// </summary>
        public float Radius;
        /// <summary>Stage that requested the overlap: OnHit, OnTravel, OnInterval, OnTick, or OnExpire.</summary>
        public RecipeStage Stage;
        /// <summary>True for an occupancy overlap, which raises this only when it is first made.</summary>
        public bool Occupancy;
    }
}
