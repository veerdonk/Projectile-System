using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Scales instance critical chance by <see cref="Scale"/>, then raises a result below 0 to 0. No upper clamp.
    /// Critical(2) doubles, Critical(0.5) halves. Scale &lt;= 0 is a no-op.
    /// Same stages as <see cref="Speed"/>. The package does not roll a crit.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Critical : IModifier
    {
        [SerializeField] float scale;

        public Critical()
        {
        }

        public Critical(float scale)
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

            context.CriticalChance = Mathf.Max(0f, context.CriticalChance * Scale);
        }
    }
}
