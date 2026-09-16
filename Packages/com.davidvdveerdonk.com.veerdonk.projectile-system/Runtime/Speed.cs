using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Scales current speed by <see cref="Scale"/> and keeps travel direction.
    /// Speed(2) doubles, Speed(0.5) halves. Scale &lt;= 0 is a no-op.
    /// OnTravel is the spawn-time start scale; OnInterval uses <see cref="IntervalSeconds"/>
    /// the same way Multiply does; OnHit scales after earlier modifiers in that list
    /// (so Bounce then Speed scales the reflected velocity).
    /// </summary>
    public sealed class Speed : IModifier
    {
        public Speed(float scale, float intervalSeconds = 0f)
        {
            Scale = scale;
            IntervalSeconds = intervalSeconds;
        }

        public float Scale { get; }

        public float IntervalSeconds { get; }

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
