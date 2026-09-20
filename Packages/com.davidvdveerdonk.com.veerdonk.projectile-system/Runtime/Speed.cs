using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales current speed by <see cref="Scale"/> and keeps travel direction.
    /// Speed(2) doubles, Speed(0.5) halves. Scale &lt;= 0 is a no-op.
    /// OnTravel is the spawn-time start scale; on OnInterval the period comes from <see cref="Interval"/>
    /// on the Recipe's OnSpawn list; OnHit scales after earlier modifiers in that list
    /// (so Bounce then Speed scales the reflected velocity).
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnInterval | RecipeStage.OnHit)]
    public sealed class Speed : IModifier
    {
        [SerializeField] float scale;

        public Speed()
        {
        }

        public Speed(float scale)
        {
            this.scale = scale;
        }

        public float Scale
        {
            get => scale;
            set => scale = value;
        }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Scale <= 0f)
                return;

            Vector3 velocity = context.Velocity;
            float magnitude = velocity.magnitude;
            if (magnitude < 1e-6f)
            {
                float speed = context.Speed;
                if (speed < 1e-6f)
                    return;
                context.Speed = speed * Scale;
                return;
            }

            context.Velocity = velocity * Scale;
            context.Speed = magnitude * Scale;
        }
    }
}
