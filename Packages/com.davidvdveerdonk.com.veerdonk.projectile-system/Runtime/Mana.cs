using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales the instance mana-cost copy by <see cref="Scale"/>. Mana(2) doubles, Mana(0.5) halves.
    /// Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>. Does not change
    /// <see cref="Recipe.ManaCost"/> or spend mana.
    /// </summary>
    public sealed class Mana : IModifier
    {
        public Mana(float scale)
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

            context.ManaCost *= Scale;
        }
    }
}
