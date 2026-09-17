using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales the instance copy of <see cref="Multiply.Spread"/> by <see cref="Scale"/>.
    /// Spread(2) doubles pip spacing, Spread(0.5) halves. Scale &lt;= 0 is a no-op.
    /// Same stages as <see cref="Speed"/>. Does not jitter the original shot.
    /// </summary>
    public sealed class Spread : IModifier
    {
        public Spread(float scale)
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

            context.Spread *= Scale;
        }
    }
}
