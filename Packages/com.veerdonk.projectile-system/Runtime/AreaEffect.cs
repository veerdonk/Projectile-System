using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Overlaps Targets in instance <see cref="ModifierContext.EffectRadius"/> and raises Area
    /// <see cref="IProjectileWorld.Hit"/> / <see cref="IProjectileWorld.StatusApplied"/>.
    /// Area damage and optional status live on this modifier, not on <see cref="Recipe.Damage"/>.
    /// OnHit uses the contact point; OnTravel / OnInterval / OnTick use the instance position.
    /// OnTick overlaps every Tick. <see cref="Occupancy"/> replaces those pulses with enter/exit.
    /// <see cref="IncludeDirectHit"/> / <see cref="IncludeDirectStatus"/> apply only on OnHit.
    /// EffectRadius 0 is a no-op. The modifier does not call Physics.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnTick | RecipeStage.OnHit)]
    public sealed class AreaEffect : IModifier
    {
        [SerializeField] float damage;
        [SerializeReference] IStatusEffect status;
        [SerializeField] bool includeDirectHit = true;
        [SerializeField] bool includeDirectStatus = true;
        [SerializeField] bool occupancy;

        public AreaEffect()
        {
        }

        public AreaEffect(
            float damage,
            IStatusEffect status = null,
            bool includeDirectHit = true,
            bool includeDirectStatus = true,
            bool occupancy = false)
        {
            this.damage = damage;
            this.status = status;
            this.includeDirectHit = includeDirectHit;
            this.includeDirectStatus = includeDirectStatus;
            this.occupancy = occupancy;
        }

        /// <summary>Area Hit damage. Distinct from <see cref="Recipe.Damage"/>.</summary>
        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        /// <summary>Optional status raised per Target in the sphere.</summary>
        public IStatusEffect Status => status;

        /// <summary>When false on OnHit, the Direct contact Hit is not raised. Ignored on OnTravel / OnInterval.</summary>
        public bool IncludeDirectHit
        {
            get => includeDirectHit;
            set => includeDirectHit = value;
        }

        /// <summary>When false on OnHit, Direct ApplyStatus is not flushed. Ignored on OnTravel / OnInterval / OnTick.</summary>
        public bool IncludeDirectStatus
        {
            get => includeDirectStatus;
            set => includeDirectStatus = value;
        }

        /// <summary>
        /// When true, a Target raises <see cref="HitSource.OccupancyEnter"/> once on enter (and status, if set)
        /// and <see cref="HitSource.OccupancyExit"/> with damage 0 on leave. No pulse Hits while they stay inside.
        /// </summary>
        public bool Occupancy
        {
            get => occupancy;
            set => occupancy = value;
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.EffectRadius <= 0f)
                return;

            Vector3 center = context.HasHit ? context.Hit.Point : context.Position;
            context.RequestAreaOverlap(center, Damage, Status, Occupancy);
            if (!context.HasHit)
                return;

            if (!IncludeDirectHit)
                context.SuppressDirectHit();
            if (!IncludeDirectStatus)
                context.SuppressDirectStatus();
        }
    }
}
