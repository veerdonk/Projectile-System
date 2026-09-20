using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance critical chance by <see cref="Scale"/>, then clamps to [0, 1].
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

        public float Scale
        {
            get => scale;
            set => scale = value;
        }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Scale <= 0f)
                return;

            context.CriticalChance = Mathf.Clamp01(context.CriticalChance * Scale);
        }
    }
}
