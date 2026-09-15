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
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return context.RandomInCone(forward, HalfAngleDegrees);
        }
    }
}
