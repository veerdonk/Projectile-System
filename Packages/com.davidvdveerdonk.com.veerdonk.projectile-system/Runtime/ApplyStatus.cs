using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.Target"/> hits request <see cref="Effect"/>. World hits do nothing.
    /// The world raises <see cref="IProjectileWorld.StatusApplied"/> for each request after this OnHit list.
    /// </summary>
    public sealed class ApplyStatus : IModifier
    {
        public ApplyStatus(IStatusEffect effect)
        {
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public IStatusEffect Effect { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasHit || context.Hit.Kind != HitKind.Target)
                return;

            context.RequestStatus(Effect);
        }
    }
}
