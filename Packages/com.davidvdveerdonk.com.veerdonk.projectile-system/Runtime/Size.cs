using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance <see cref="ModifierContext.Radius"/> by <see cref="Scale"/>. Size(2) doubles,
    /// Size(0.5) halves. Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>. There is no
    /// separate size field. SphereCast and the view follow the new radius.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Size : IModifier
    {
        [SerializeField] float scale;

        public Size()
        {
        }

        public Size(float scale)
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

            context.Radius *= Scale;
        }
    }
}
