using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales instance <see cref="ModifierContext.Radius"/> by <see cref="Scale"/>. Size(2) doubles,
    /// Size(0.5) halves. Scale &lt;= 0 is a no-op. Same stages as <see cref="Speed"/>. There is no
    /// separate size field. SphereCast and the view follow the new radius.
    /// </summary>
    public sealed class Size : IModifier
    {
        public Size(float scale)
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

            context.Radius *= Scale;
        }
    }
}
