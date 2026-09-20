using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales the instance copy of <see cref="Multiply.Spread"/> by <see cref="Scale"/>.
    /// Spread(2) doubles pip spacing, Spread(0.5) halves. Scale &lt;= 0 is a no-op.
    /// Same stages as <see cref="Speed"/>. Does not jitter the original shot.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Spread : IModifier
    {
        [SerializeField] float scale;

        public Spread()
        {
        }

        public Spread(float scale)
        {
            this.scale = scale;
        }

        public float Scale => scale;

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Scale <= 0f)
                return;

            context.Spread *= Scale;
        }
    }
}
