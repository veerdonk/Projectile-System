using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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

        public float Scale => scale;

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
