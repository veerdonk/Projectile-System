using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Uniform samples in a disk of radius <c>spread</c>. Count does not grow the disk.
    /// One child sits at the origin; otherwise samples are uniform in the disk, then rejected
    /// if closer than <see cref="MinDistance"/> to an already placed child.
    /// <see cref="Multiply"/> sets that to the child diameter. Randomness uses the world seed.
    /// </summary>
    public sealed class RandomCirclePattern : IMultiplyPattern
    {
        const int MaxAttempts = 64;
        readonly List<Vector3> placed = new List<Vector3>();

        /// <summary>Meters. <see cref="Multiply"/> writes the child diameter (<c>2 * Radius</c>). 0 disables the check.</summary>
        internal float MinDistance { get; set; }

        public Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (index == 0)
                placed.Clear();
            if (spread <= 0f || count <= 1 || index < 0 || index >= count)
                return Vector3.zero;

            float minSqr = MinDistance * MinDistance;
            Vector3 best = default;
            float bestNearestSqr = -1f;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Vector3 candidate = SampleDisk(context, forward, spread);
                float nearestSqr = NearestSqr(candidate);
                if (nearestSqr >= minSqr)
                {
                    placed.Add(candidate);
                    return candidate;
                }

                if (nearestSqr > bestNearestSqr)
                {
                    bestNearestSqr = nearestSqr;
                    best = candidate;
                }
            }

            placed.Add(best);
            return best;
        }

        static Vector3 SampleDisk(ModifierContext context, Vector3 forward, float spread)
        {
            float radius = spread * Mathf.Sqrt(context.NextFloat(0f, 1f));
            float theta = context.NextFloat(0f, 2f * Mathf.PI);
            return MultiplyPatternPlane.ToWorld(forward, radius * Mathf.Sin(theta), radius * Mathf.Cos(theta));
        }

        float NearestSqr(Vector3 candidate)
        {
            if (placed.Count == 0)
                return float.PositiveInfinity;

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < placed.Count; i++)
            {
                float sqr = (candidate - placed[i]).sqrMagnitude;
                if (sqr < nearest)
                    nearest = sqr;
            }

            return nearest;
        }
    }
}
