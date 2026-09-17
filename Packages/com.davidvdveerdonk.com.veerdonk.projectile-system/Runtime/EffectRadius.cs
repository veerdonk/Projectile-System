using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance effect radius by <see cref="Scale"/>. EffectRadius(2) doubles,
    /// EffectRadius(0.5) halves. Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>.
    /// Does not run an overlap query.
    /// </summary>
    public sealed class EffectRadius : IModifier
    {
        public EffectRadius(float scale)
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

            context.EffectRadius *= Scale;
        }
    }
}
