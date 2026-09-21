using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Scales instance effect radius by <see cref="Scale"/>. EffectRadius(2) doubles,
    /// EffectRadius(0.5) halves. Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>.
    /// Does not run an overlap query.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class EffectRadius : IModifier
    {
        [SerializeField] float scale;

        public EffectRadius()
        {
        }

        public EffectRadius(float scale)
        {
            this.scale = scale;
        }

        /// <summary>Multiplier applied to the instance stat. Values &lt;= 0 are a no-op.</summary>
        public float Scale
        {
            get => scale;
            set => scale = value;
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Scale <= 0f)
                return;

            context.EffectRadius *= Scale;
        }
    }
}
