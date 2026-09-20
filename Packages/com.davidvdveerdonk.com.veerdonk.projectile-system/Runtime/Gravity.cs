using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance <see cref="ModifierContext.Gravity"/> by <see cref="Scale"/>.
    /// Gravity(2) doubles drop, Gravity(0.5) halves. Scale &lt;= 0 is a no-op.
    /// Same stages as <see cref="Speed"/>. Burst still applies gravity each tick;
    /// this modifier does not add g * dt itself.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Gravity : IModifier
    {
        [SerializeField] float scale;

        public Gravity()
        {
        }

        public Gravity(float scale)
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

            context.Gravity *= Scale;
        }
    }
}
