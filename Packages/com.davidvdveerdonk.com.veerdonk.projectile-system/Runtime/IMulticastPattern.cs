using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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

    /// <summary>Random cone around the spawn direction. Uses the world seed, so the same seed gives the same cone.</summary>
    public sealed class RandomConePattern : IMulticastPattern
    {
        public const float DefaultHalfAngleDegrees = 15f;

        public RandomConePattern(float halfAngleDegrees = DefaultHalfAngleDegrees)
        {
            HalfAngleDegrees = halfAngleDegrees;
        }

        public float HalfAngleDegrees { get; }

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
