using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// OnSpawn: total instances is <see cref="Count"/> — the original plus <see cref="Count"/> - 1 extras of an explicit
    /// sibling Recipe. <see cref="Pattern"/> aims all of them, the original included. Extras never multicast again.
    /// </summary>
    public sealed class Multicast : IModifier
    {
        public Multicast(int count, Recipe extraRecipe, IMulticastPattern pattern = null)
        {
            Count = count;
            ExtraRecipe = extraRecipe ?? throw new ArgumentNullException(nameof(extraRecipe));
            Pattern = pattern ?? new RandomConePattern();
        }

        public int Count { get; }

        public Recipe ExtraRecipe { get; }

        public IMulticastPattern Pattern { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int count = Count;
            if (count <= 1)
                return;

            Vector3 forward = context.Velocity;
            float speed = forward.magnitude;
            if (speed > 1e-6f)
                forward /= speed;
            else
            {
                forward = Vector3.forward;
                speed = context.Speed;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Aim(Pattern.GetDirection(context, forward, i, count), forward);
                if (i == 0)
                    context.Velocity = direction * speed;
                else
                    context.RequestSpawn(ExtraRecipe, context.Position, direction, suppressMulticast: true);
            }
        }

        static Vector3 Aim(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 1e-12f ? direction.normalized : fallback;
        }
    }
}
