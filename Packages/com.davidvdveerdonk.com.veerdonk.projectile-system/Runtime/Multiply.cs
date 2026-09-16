using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Spawns <see cref="Count"/> instances of an explicit child Recipe from OnHit, OnExpire, or OnInterval.
    /// Children keep the parent travel direction, or the hit normal on a hit. Positions come from
    /// <see cref="Pattern"/>; <see cref="Spread"/> is meters between adjacent pips (0 stacks).
    /// On OnInterval the period comes from <see cref="Interval"/> on the Recipe's OnSpawn list.
    /// </summary>
    public sealed class Multiply : IModifier
    {
        const float SurfaceSkin = 0.01f;

        public Multiply(int count, Recipe childRecipe, float spread = 0f, IMultiplyPattern pattern = null)
        {
            Count = count;
            ChildRecipe = childRecipe ?? throw new ArgumentNullException(nameof(childRecipe));
            Spread = spread;
            Pattern = pattern ?? new SquarePattern();
        }

        public int Count { get; }

        public Recipe ChildRecipe { get; }

        public float Spread { get; }

        public IMultiplyPattern Pattern { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int n = Count;
            if (n <= 0)
                return;

            Vector3 origin;
            Vector3 forward;
            if (context.HasHit)
            {
                origin = context.Hit.Point;
                forward = context.Hit.Normal;
            }
            else
            {
                origin = context.Position;
                forward = context.Velocity;
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 spawnPosition = origin + Pattern.GetOffset(context, forward, Spread, i, n);
                if (context.HasHit)
                    spawnPosition = OffsetOffSurface(spawnPosition, forward, ChildRecipe.Radius);
                context.RequestMultiplyChild(ChildRecipe, spawnPosition, forward);
            }
        }

        static Vector3 OffsetOffSurface(Vector3 point, Vector3 direction, float radius)
        {
            Vector3 along = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            return point + along * (radius + SurfaceSkin);
        }
    }
}
