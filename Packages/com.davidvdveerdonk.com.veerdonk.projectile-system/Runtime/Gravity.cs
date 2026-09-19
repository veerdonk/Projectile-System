using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance <see cref="ModifierContext.Gravity"/> by <see cref="Scale"/>.
    /// Gravity(2) doubles drop, Gravity(0.5) halves. Scale &lt;= 0 is a no-op.
    /// Same stages as <see cref="Speed"/>. Burst still applies gravity each tick;
    /// this modifier does not add g * dt itself.
    /// </summary>
    public sealed class Gravity : IModifier
    {
        public Gravity(float scale)
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

            context.Gravity *= Scale;
        }
    }
}
