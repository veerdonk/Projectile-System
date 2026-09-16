using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Uniform samples in a disk of radius <c>spread</c>. Count does not grow the disk.
    /// One child sits at the origin; otherwise samples are uniform in the disk.
    /// Randomness uses the world seed.
    /// </summary>
    public sealed class RandomCirclePattern : IMultiplyPattern
    {
        public Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (spread <= 0f || count <= 1 || index < 0 || index >= count)
                return Vector3.zero;

            float radius = spread * Mathf.Sqrt(context.NextFloat(0f, 1f));
            float theta = context.NextFloat(0f, 2f * Mathf.PI);
            return MultiplyPatternPlane.ToWorld(forward, radius * Mathf.Sin(theta), radius * Mathf.Cos(theta));
        }
    }
}
