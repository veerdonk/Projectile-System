using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.World"/> and <see cref="HitKind.Target"/> hits consume remaining bounce and reflect
    /// velocity around <see cref="HitInfo.Normal"/>. The Hit event still fires, so dummy HP still drops on Target.
    /// Despawn is cancelled while remaining is greater than 0.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnHit)]
    public sealed class Bounce : IModifier
    {
        [SerializeField] int count;

        public Bounce()
        {
        }

        public Bounce(int count)
        {
            this.count = count;
        }

        /// <summary>Hits this instance can still reflect.</summary>
        public int Count
        {
            get => count;
            set => count = value;
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasHit)
                return;
            if (context.RemainingBounce <= 0)
                return;

            context.RemainingBounce--;
            context.Velocity = Vector3.Reflect(context.Velocity, context.Hit.Normal);
            context.CancelDespawn = true;
        }
    }
}
