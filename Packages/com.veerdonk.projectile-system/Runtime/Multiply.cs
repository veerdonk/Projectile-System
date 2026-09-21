using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Spawns <see cref="Count"/> instances of an explicit child Recipe from OnHit, OnExpire, or OnInterval.
    /// Children keep the parent travel direction, or the hit normal on a hit. Positions come from
    /// <see cref="Pattern"/>. Pip spacing is the instance copy of
    /// <see cref="Spread"/> (authored here, scaled by the Spread modifier). 0 stacks.
    /// On OnInterval the period comes from <see cref="Interval"/> on the Recipe's OnSpawn list.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnInterval | RecipeStage.OnHit | RecipeStage.OnExpire)]
    public sealed class Multiply : IModifier
    {
        const float SurfaceSkin = 0.01f;

        [SerializeField] int count;
        [SerializeField] RecipeAsset childAsset;
        [SerializeField] float spread;
        [SerializeReference] IMultiplyPattern pattern;

        Recipe childRecipe;

        public Multiply()
        {
            pattern = new SquarePattern();
        }

        public Multiply(int count, Recipe childRecipe, float spread = 0f, IMultiplyPattern pattern = null)
        {
            this.count = count;
            this.childRecipe = childRecipe ?? throw new ArgumentNullException(nameof(childRecipe));
            this.spread = spread;
            this.pattern = pattern ?? new SquarePattern();
        }

        public Multiply(int count, RecipeAsset childAsset, float spread = 0f, IMultiplyPattern pattern = null)
            : this(count, new Recipe(), spread, pattern)
        {
            this.childAsset = childAsset ?? throw new ArgumentNullException(nameof(childAsset));
            childRecipe = null;
        }

        /// <summary>Children to spawn. Positions come from <see cref="Pattern"/>.</summary>
        public int Count
        {
            get => count;
            set => count = value;
        }

        /// <summary>Runtime child used by tests and <see cref="RecipeAsset.ToRecipe"/>.</summary>
        public Recipe ChildRecipe => childRecipe;

        /// <summary>Authored child asset. Resolved by <see cref="RecipeAsset.ToRecipe"/>.</summary>
        public RecipeAsset ChildAsset => childAsset;

        /// <summary>Meters between pips. Copied onto the instance at spawn so the Spread modifier can scale it. 0 stacks.</summary>
        public float Spread
        {
            get => spread;
            set => spread = value;
        }

        /// <summary>Layout of child positions. Null authors as <see cref="SquarePattern"/>.</summary>
        public IMultiplyPattern Pattern
        {
            get => pattern;
            set => pattern = value ?? new SquarePattern();
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int n = Count;
            if (n <= 0)
                return;

            if (Pattern is RandomCirclePattern randomCircle)
                randomCircle.MinDistance = ChildRecipe.Radius * 2f;

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
                Vector3 spawnPosition = origin + Pattern.GetOffset(context, forward, context.Spread, i, n);
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
