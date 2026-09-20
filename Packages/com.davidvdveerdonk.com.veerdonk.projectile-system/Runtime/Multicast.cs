using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// OnSpawn: total instances is <see cref="Count"/> — the original plus <see cref="Count"/> - 1 extras of an explicit
    /// sibling Recipe. <see cref="Pattern"/> aims all of them, the original included. Extras never multicast again.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnSpawn)]
    public sealed class Multicast : IModifier
    {
        [SerializeField] int count;
        [SerializeField] RecipeAsset extraAsset;

        Recipe extraRecipe;
        IMulticastPattern pattern;

        public Multicast()
        {
            pattern = new RandomConePattern();
        }

        public Multicast(int count, Recipe extraRecipe, IMulticastPattern pattern = null)
        {
            this.count = count;
            this.extraRecipe = extraRecipe ?? throw new ArgumentNullException(nameof(extraRecipe));
            this.pattern = pattern ?? new RandomConePattern();
        }

        public Multicast(int count, RecipeAsset extraAsset, IMulticastPattern pattern = null)
            : this(count, new Recipe(), pattern)
        {
            this.extraAsset = extraAsset ?? throw new ArgumentNullException(nameof(extraAsset));
            extraRecipe = null;
        }

        public int Count
        {
            get => count;
            set => count = value;
        }

        public Recipe ExtraRecipe => extraRecipe;

        public RecipeAsset ExtraAsset => extraAsset;

        public IMulticastPattern Pattern => pattern;

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int total = Count;
            if (total <= 1)
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

            for (int i = 0; i < total; i++)
            {
                Vector3 direction = Aim(Pattern.GetDirection(context, forward, i, total), forward);
                if (i == 0)
                    context.Velocity = direction * speed;
                else
                    context.RequestMulticastExtra(ExtraRecipe, context.Position, direction);
            }
        }

        static Vector3 Aim(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 1e-12f ? direction.normalized : fallback;
        }
    }
}
