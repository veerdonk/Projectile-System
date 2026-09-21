using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Scales instance damage by <see cref="Scale"/>. Damage(2) doubles, Damage(0.5) halves.
    /// Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>: OnTravel start, OnInterval
    /// progressive, OnHit after bounce/pierce. The package does not apply HP.
    /// OnHit scales do not rewrite the sampled Direct <see cref="HitInfo"/> for this contact.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Damage : IModifier
    {
        [SerializeField] float scale;

        public Damage()
        {
        }

        public Damage(float scale)
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

            context.Damage *= Scale;
        }
    }
}
