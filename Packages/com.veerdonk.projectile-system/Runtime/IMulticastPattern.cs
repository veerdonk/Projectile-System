using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Aims the instances a <see cref="Multicast"/> produces.</summary>
    public interface IMulticastPattern
    {
        /// <summary>
        /// Direction for instance <paramref name="index"/> of <paramref name="count"/> total, around
        /// <paramref name="forward"/>. Index 0 is the instance that is already flying; it is re-aimed at this direction.
        /// </summary>
        Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count);
    }

    /// <summary>
    /// A <see cref="IMulticastPattern"/> that also moves where each instance starts. Patterns without it spawn every
    /// instance at the shot's position.
    /// The original's <see cref="IProjectileWorld.Spawned"/> reports the request position, from before OnSpawn moves it,
    /// just as it reports the direction from before the re-aim; each extra's reports its offset position.
    /// A Hitscan or Beam starts its polyline at the offset. Either <see cref="IProjectileWorld.AimBeam(ProjectileId, Vector3, Vector3)"/>
    /// overload sets the Beam's pose and so replaces the offset (the game owns that pose).
    /// The move to the offset is not swept, so nothing between the muzzle and a lane is hit, and an instance can start
    /// inside a wall or a target beside the muzzle. Its first cast starts there, and the hit query decides what a body
    /// it starts inside does.
    /// An <see cref="Delivery.Arc"/> shot with an aim point (<see cref="SpawnRequest.HomingAcquirePointSet"/>) keeps its
    /// loft and moves, so it lands at the aim point plus its offset. Each Arc extra solves its own loft, with its own
    /// Recipe's speed and gravity, from its lane to the aim point plus its offset, then turns it by the pattern's deviation
    /// from the shot's direction; a lane that cannot reach takes the pattern direction around the shot's loft. Homing's
    /// first lock still reads the original aim point. Patterns without this interface keep solving every Arc extra to the
    /// aim point itself.
    /// Every <see cref="IMulticastPattern.GetDirection"/> and <see cref="GetOffset"/> call sees the shot's position and
    /// velocity from before Multicast; the original is re-aimed and moved after the last call.
    /// </summary>
    public interface IMulticastOffsetPattern : IMulticastPattern
    {
        /// <summary>
        /// World-space offset from the shot's position for instance <paramref name="index"/> of <paramref name="count"/>
        /// total. <paramref name="forward"/> is the shot's travel direction before the pattern re-aims anything. Index 0 is
        /// the instance that is already flying; it moves to this offset at OnSpawn.
        /// </summary>
        Vector3 GetOffset(ModifierContext context, Vector3 forward, int index, int count);
    }

    /// <summary>Random cone around the spawn direction. Uses the world seed, so the same seed gives the same cone.</summary>
    [Serializable]
    public sealed class RandomConePattern : IMulticastPattern
    {
        /// <summary>Default half-angle of the cone, in degrees.</summary>
        public const float DefaultHalfAngleDegrees = 15f;

        [SerializeField] float halfAngleDegrees;

        /// <summary>Cone of <see cref="DefaultHalfAngleDegrees"/>.</summary>
        public RandomConePattern()
            : this(DefaultHalfAngleDegrees)
        {
        }

        /// <summary>Cone of <paramref name="halfAngleDegrees"/>.</summary>
        public RandomConePattern(float halfAngleDegrees)
        {
            this.halfAngleDegrees = halfAngleDegrees;
        }

        /// <summary>Half-angle of the cone, in degrees. <c>&lt;= 0</c> keeps every instance on the spawn direction.</summary>
        public float HalfAngleDegrees => halfAngleDegrees;

        /// <inheritdoc />
        public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
        {
            return Sample(context, forward, HalfAngleDegrees);
        }

        /// <summary>Direction inside a cone of <paramref name="halfAngleDegrees"/> around <paramref name="forward"/>.</summary>
        public static Vector3 Sample(ModifierContext context, Vector3 forward, float halfAngleDegrees)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (forward.sqrMagnitude < 1e-12f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            if (halfAngleDegrees <= 0f)
                return forward;

            Vector3 tangent = Vector3.Cross(forward, Vector3.up);
            if (tangent.sqrMagnitude < 1e-8f)
                tangent = Vector3.Cross(forward, Vector3.right);
            tangent.Normalize();

            float angle = context.NextFloat(0f, halfAngleDegrees);
            float spin = context.NextFloat(0f, 360f);
            Quaternion rotation = Quaternion.AngleAxis(spin, forward) * Quaternion.AngleAxis(angle, tangent);
            return rotation * forward;
        }
    }
}
