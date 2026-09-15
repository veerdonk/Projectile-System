using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Spawns <see cref="Count"/> instances of an explicit child Recipe from OnHit, OnExpire, or OnInterval.
    /// On a hit, children start at the hit point along the hit normal. Count &gt; 1 uses a small random cone.
    /// </summary>
    public sealed class Multiply : IModifier
    {
        const float SpreadDegrees = 12f;
        const float SurfaceSkin = 0.01f;

        public Multiply(int count, Recipe childRecipe, float intervalSeconds = 0f)
        {
            Count = count;
            ChildRecipe = childRecipe ?? throw new ArgumentNullException(nameof(childRecipe));
            IntervalSeconds = intervalSeconds;
        }

        public int Count { get; }

        public Recipe ChildRecipe { get; }

        public float IntervalSeconds { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int n = Count;
            if (n <= 0)
                return;

            Vector3 position;
            Vector3 direction;
            if (context.HasHit)
            {
                position = context.Hit.Point;
                direction = context.Hit.Normal;
            }
            else
            {
                position = context.Position;
                direction = context.Velocity;
            }

            bool spread = n > 1;
            for (int i = 0; i < n; i++)
            {
                Vector3 spawnDirection = spread
                    ? RandomConePattern.Sample(context, direction, SpreadDegrees)
                    : direction;
                Vector3 spawnPosition = context.HasHit
                    ? OffsetOffSurface(position, spawnDirection, ChildRecipe.Radius)
                    : position;
                context.RequestSpawn(ChildRecipe, spawnPosition, spawnDirection);
            }
        }

        static Vector3 OffsetOffSurface(Vector3 point, Vector3 direction, float radius)
        {
            Vector3 along = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            return point + along * (radius + SurfaceSkin);
        }
    }
}
