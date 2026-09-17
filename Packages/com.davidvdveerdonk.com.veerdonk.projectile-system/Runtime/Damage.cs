using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance damage by <see cref="Scale"/>. Damage(2) doubles, Damage(0.5) halves.
    /// Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>: OnTravel start, OnInterval
    /// progressive, OnHit after bounce/pierce. The package does not apply HP.
    /// OnHit scales do not rewrite the sampled Direct <see cref="HitInfo"/> for this contact.
    /// </summary>
    public sealed class Damage : IModifier
    {
        public Damage(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; }

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
