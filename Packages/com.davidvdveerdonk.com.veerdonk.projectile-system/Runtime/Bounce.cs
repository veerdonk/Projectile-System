using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.World"/> hits consume remaining bounce and reflect velocity around <see cref="HitInfo.Normal"/>.
    /// Despawn is cancelled while remaining is greater than 0.
    /// </summary>
    public sealed class Bounce : IModifier
    {
        public Bounce(int count)
        {
            Count = count;
        }

        public int Count { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasHit || context.Hit.Kind != HitKind.World)
                return;
            if (context.RemainingBounce <= 0)
                return;

            context.RemainingBounce--;
            context.Velocity = Vector3.Reflect(context.Velocity, context.Hit.Normal);
            context.CancelDespawn = true;
        }
    }
}
