using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// OnSpawn: how many seconds pass between OnInterval runs for this instance. Every modifier on the
    /// OnInterval list fires on those crossings, in list order. Without an Interval the period is 0 and
    /// OnInterval never runs. This is not a despawn clock; <see cref="Recipe.LifetimeSeconds"/> still is.
    /// </summary>
    public sealed class Interval : IModifier
    {
        public Interval(float seconds)
        {
            Seconds = seconds;
        }

        public float Seconds { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.IntervalSeconds = Seconds > 0f ? Seconds : 0f;
        }
    }
}
