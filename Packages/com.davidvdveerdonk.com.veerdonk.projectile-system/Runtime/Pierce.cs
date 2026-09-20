using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.Target"/> hits and <see cref="ProtectionLevel.Basic"/> hits consume remaining pierce.
    /// Basic still consumes when <see cref="HitInfo.Kind"/> is World. Despawn is cancelled while remaining is greater than 0.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnHit)]
    public sealed class Pierce : IModifier
    {
        [SerializeField] int count;

        public Pierce()
        {
        }

        public Pierce(int count)
        {
            this.count = count;
        }

        public int Count => count;

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasHit)
                return;
            if (context.Hit.Kind != HitKind.Target && context.Hit.Protection != ProtectionLevel.Basic)
                return;
            if (context.RemainingPierce <= 0)
                return;

            context.RemainingPierce--;
            context.CancelDespawn = true;
        }
    }
}
