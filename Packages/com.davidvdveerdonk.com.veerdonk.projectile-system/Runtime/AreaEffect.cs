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
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class AreaEffect : IModifier
    {
        [SerializeField] float damage;
        [SerializeReference] IStatusEffect status;
        [SerializeField] bool includeDirectHit = true;
        [SerializeField] bool includeDirectStatus = true;

        public AreaEffect()
        {
        }

        public AreaEffect(
            float damage,
            IStatusEffect status = null,
            bool includeDirectHit = true,
            bool includeDirectStatus = true)
        {
            this.damage = damage;
            this.status = status;
            this.includeDirectHit = includeDirectHit;
            this.includeDirectStatus = includeDirectStatus;
        }

        public float Damage => damage;

        public IStatusEffect Status => status;

        public bool IncludeDirectHit => includeDirectHit;

        public bool IncludeDirectStatus => includeDirectStatus;

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
