using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.Target"/> hits consume remaining pierce. Despawn is cancelled while remaining is greater than 0.
    /// </summary>
    public sealed class Pierce : IModifier
    {
        public Pierce(int count)
        {
            Count = count;
        }

        public int Count { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasHit || context.Hit.Kind != HitKind.Target)
                return;
            if (context.RemainingPierce <= 0)
                return;

            context.RemainingPierce--;
            context.CancelDespawn = true;
        }
    }
}
