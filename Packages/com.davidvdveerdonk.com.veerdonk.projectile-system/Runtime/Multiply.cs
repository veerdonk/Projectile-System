using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Spawns <see cref="Count"/> instances of an explicit child Recipe.
    /// On a hit, children start at the hit point with velocity along the hit normal.
    /// </summary>
    public sealed class Multiply : IModifier
    {
        public Multiply(int count, Recipe childRecipe)
        {
            Count = count;
            ChildRecipe = childRecipe ?? throw new ArgumentNullException(nameof(childRecipe));
        }

        public int Count { get; }

        public Recipe ChildRecipe { get; }

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

            for (int i = 0; i < n; i++)
                context.RequestSpawn(ChildRecipe, position, direction);
        }
    }
}
