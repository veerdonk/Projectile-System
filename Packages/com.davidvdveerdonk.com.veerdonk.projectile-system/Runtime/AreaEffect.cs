using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Overlaps Targets in instance <see cref="ModifierContext.EffectRadius"/> and raises Area
    /// <see cref="IProjectileWorld.Hit"/> / <see cref="IProjectileWorld.StatusApplied"/>.
    /// Area damage and optional status live on this modifier, not on <see cref="Recipe.Damage"/>.
    /// OnHit uses the contact point; OnTravel / OnInterval use the instance position.
    /// <see cref="IncludeDirectHit"/> / <see cref="IncludeDirectStatus"/> apply only on OnHit.
    /// EffectRadius 0 is a no-op. The modifier does not call Physics.
    /// </summary>
    public sealed class AreaEffect : IModifier
    {
        public AreaEffect(
            float damage,
            IStatusEffect status = null,
            bool includeDirectHit = true,
            bool includeDirectStatus = true)
        {
            Damage = damage;
            Status = status;
            IncludeDirectHit = includeDirectHit;
            IncludeDirectStatus = includeDirectStatus;
        }

        public float Damage { get; }

        public IStatusEffect Status { get; }

        public bool IncludeDirectHit { get; }

        public bool IncludeDirectStatus { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.EffectRadius <= 0f)
                return;

            Vector3 center = context.HasHit ? context.Hit.Point : context.Position;
            context.RequestAreaOverlap(center, Damage, Status);
            if (!context.HasHit)
                return;

            if (!IncludeDirectHit)
                context.SuppressDirectHit();
            if (!IncludeDirectStatus)
                context.SuppressDirectStatus();
        }
    }
}
