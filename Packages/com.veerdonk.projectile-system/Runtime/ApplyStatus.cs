using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// <see cref="HitKind.Target"/> hits request <see cref="Effect"/>. World hits do nothing.
    /// The world raises <see cref="IProjectileWorld.StatusApplied"/> for each request after this OnHit list,
    /// unless an <see cref="AreaEffect"/> suppressed the Direct status flush.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnHit)]
    public sealed class ApplyStatus : IModifier
    {
        [SerializeReference] IStatusEffect effect;

        public ApplyStatus()
        {
            effect = new DummyStatusEffect();
        }

        public ApplyStatus(IStatusEffect effect)
        {
            this.effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        /// <summary>Status requested on Target hits. Default constructor uses <see cref="DummyStatusEffect"/>.</summary>
        public IStatusEffect Effect => effect;

        /// <inheritdoc />
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
