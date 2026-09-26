using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// ScriptableObject authoring for a <see cref="Recipe"/>. Call <see cref="ToRecipe"/> before
    /// <see cref="IProjectileWorld.Spawn"/>. Child RecipeAsset references share one Recipe when a cycle is present.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe", menuName = "Projectile/Recipe")]
    public sealed class RecipeAsset : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] float speed;
        [SerializeField] float radius = 0.25f;
        [SerializeField] float lifetimeSeconds;
        [SerializeField] float gravity;
        [SerializeField] float damage = 1f;
        [SerializeField] float criticalChance;
        [SerializeField] float manaCost;
        [SerializeField] float effectRadius;
        [SerializeField] string presentationId;
        [SerializeField] int multicastGenerations;
        [SerializeField] int multiplyGenerations;

        [SerializeReference] IModifier[] onSpawn = Array.Empty<IModifier>();
        [SerializeReference] IModifier[] onTravel = Array.Empty<IModifier>();
        [SerializeReference] IModifier[] onTick = Array.Empty<IModifier>();
        [SerializeReference] IModifier[] onInterval = Array.Empty<IModifier>();
        [SerializeReference] IModifier[] onHit = Array.Empty<IModifier>();
        [SerializeReference] IModifier[] onExpire = Array.Empty<IModifier>();

        /// <inheritdoc cref="Recipe.Id"/>
        public string Id
        {
            get => id;
            set => id = value;
        }

        /// <inheritdoc cref="Recipe.Speed"/>
        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        /// <inheritdoc cref="Recipe.Radius"/>
        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        /// <inheritdoc cref="Recipe.LifetimeSeconds"/>
        public float LifetimeSeconds
        {
            get => lifetimeSeconds;
            set => lifetimeSeconds = value;
        }

        /// <inheritdoc cref="Recipe.Gravity"/>
        public float Gravity
        {
            get => gravity;
            set => gravity = value;
        }

        /// <inheritdoc cref="Recipe.Damage"/>
        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        /// <inheritdoc cref="Recipe.CriticalChance"/>
        public float CriticalChance
        {
            get => criticalChance;
            set => criticalChance = value;
        }

        /// <inheritdoc cref="Recipe.ManaCost"/>
        public float ManaCost
        {
            get => manaCost;
            set => manaCost = value;
        }

        /// <inheritdoc cref="Recipe.EffectRadius"/>
        public float EffectRadius
        {
            get => effectRadius;
            set => effectRadius = value;
        }

        /// <inheritdoc cref="Recipe.PresentationId"/>
        public string PresentationId
        {
            get => presentationId;
            set => presentationId = value;
        }

        /// <inheritdoc cref="Recipe.MulticastGenerations"/>
        public int MulticastGenerations
        {
            get => multicastGenerations;
            set => multicastGenerations = value;
        }

        /// <inheritdoc cref="Recipe.MultiplyGenerations"/>
        public int MultiplyGenerations
        {
            get => multiplyGenerations;
            set => multiplyGenerations = value;
        }

        /// <inheritdoc cref="Recipe.OnSpawn"/>
        public IModifier[] OnSpawn
        {
            get => onSpawn ?? Array.Empty<IModifier>();
            set => onSpawn = value ?? Array.Empty<IModifier>();
        }

        /// <inheritdoc cref="Recipe.OnTravel"/>
        public IModifier[] OnTravel
        {
            get => onTravel ?? Array.Empty<IModifier>();
            set => onTravel = value ?? Array.Empty<IModifier>();
        }

        /// <inheritdoc cref="Recipe.OnTick"/>
        public IModifier[] OnTick
        {
            get => onTick ?? Array.Empty<IModifier>();
            set => onTick = value ?? Array.Empty<IModifier>();
        }

        /// <inheritdoc cref="Recipe.OnInterval"/>
        public IModifier[] OnInterval
        {
            get => onInterval ?? Array.Empty<IModifier>();
            set => onInterval = value ?? Array.Empty<IModifier>();
        }

        /// <inheritdoc cref="Recipe.OnHit"/>
        public IModifier[] OnHit
        {
            get => onHit ?? Array.Empty<IModifier>();
            set => onHit = value ?? Array.Empty<IModifier>();
        }

        /// <inheritdoc cref="Recipe.OnExpire"/>
        public IModifier[] OnExpire
        {
            get => onExpire ?? Array.Empty<IModifier>();
            set => onExpire = value ?? Array.Empty<IModifier>();
        }

        /// <summary>
        /// Builds a runtime <see cref="Recipe"/>. Cycles through child assets share one instance
        /// so self-multicast cannot stack-overflow in the builder.
        /// </summary>
        public Recipe ToRecipe()
        {
            return ToRecipe(new Dictionary<RecipeAsset, Recipe>());
        }

        Recipe ToRecipe(Dictionary<RecipeAsset, Recipe> visited)
        {
            if (visited.TryGetValue(this, out Recipe existing))
                return existing;

            Recipe recipe = new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = radius,
                LifetimeSeconds = lifetimeSeconds,
                Gravity = gravity,
                Damage = damage,
                CriticalChance = criticalChance,
                ManaCost = manaCost,
                EffectRadius = effectRadius,
                PresentationId = presentationId,
                MulticastGenerations = multicastGenerations,
                MultiplyGenerations = multiplyGenerations
            };
            visited.Add(this, recipe);
            recipe.OnSpawn = CloneList(onSpawn, visited);
            recipe.OnTravel = CloneList(onTravel, visited);
            recipe.OnTick = CloneList(onTick, visited);
            recipe.OnInterval = CloneList(onInterval, visited);
            recipe.OnHit = CloneList(onHit, visited);
            recipe.OnExpire = CloneList(onExpire, visited);
            return recipe;
        }

        internal static IModifier[] CloneModifierList(IModifier[] source)
        {
            return CloneList(source, new Dictionary<RecipeAsset, Recipe>());
        }

        static IModifier[] CloneList(IModifier[] source, Dictionary<RecipeAsset, Recipe> visited)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<IModifier>();

            var clone = new IModifier[source.Length];
            for (int i = 0; i < source.Length; i++)
                clone[i] = CloneModifier(source[i], visited);
            return clone;
        }

        static IModifier CloneModifier(IModifier modifier, Dictionary<RecipeAsset, Recipe> visited)
        {
            switch (modifier)
            {
                case Pierce pierce:
                    return new Pierce(pierce.Count);
                case Bounce bounce:
                    return new Bounce(bounce.Count);
                case Interval interval:
                    return new Interval(interval.Seconds);
                case Speed speed:
                    return new Speed(speed.Scale);
                case Damage damage:
                    return new Damage(damage.Scale);
                case Critical critical:
                    return new Critical(critical.Scale);
                case Mana mana:
                    return new Mana(mana.Scale);
                case Spread spread:
                    return new Spread(spread.Scale);
                case EffectRadius effectRadius:
                    return new EffectRadius(effectRadius.Scale);
                case Size size:
                    return new Size(size.Scale);
                case Gravity gravity:
                    return new Gravity(gravity.Scale);
                case Homing homing:
                    return new Homing(homing.TurnRateDegreesPerSecond);
                case ApplyStatus applyStatus:
                    return new ApplyStatus(CloneStatus(applyStatus.Effect) ?? new DummyStatusEffect());
                case AreaEffect area:
                    return new AreaEffect(
                        area.Damage,
                        CloneStatus(area.Status),
                        area.IncludeDirectHit,
                        area.IncludeDirectStatus,
                        area.Occupancy);
                case GroundField field:
                    return new GroundField(
                        field.Radius,
                        field.LifetimeSeconds,
                        field.Damage,
                        CloneStatus(field.Status),
                        field.IntervalSeconds,
                        field.PresentationId,
                        field.Occupancy);
                case Multiply multiply:
                    return new Multiply(
                        multiply.Count,
                        ResolveChild(multiply.ChildAsset, multiply.ChildRecipe, visited),
                        multiply.Spread,
                        ClonePattern(multiply.Pattern));
                case Multicast multicast:
                    return new Multicast(
                        multicast.Count,
                        ResolveChild(multicast.ExtraAsset, multicast.ExtraRecipe, visited));
                default:
                    return modifier;
            }
        }

        static Recipe ResolveChild(RecipeAsset asset, Recipe runtime, Dictionary<RecipeAsset, Recipe> visited)
        {
            if (asset != null)
                return asset.ToRecipe(visited);
            return runtime;
        }

        static IMultiplyPattern ClonePattern(IMultiplyPattern pattern)
        {
            if (pattern == null)
                return new SquarePattern();
            return Create<IMultiplyPattern>(pattern.GetType()) ?? new SquarePattern();
        }

        static IStatusEffect CloneStatus(IStatusEffect effect)
        {
            if (effect == null)
                return null;
            return Create<IStatusEffect>(effect.GetType()) ?? effect;
        }

        static T Create<T>(Type type) where T : class
        {
            if (type == null || type.IsAbstract)
                return null;
            try
            {
                return Activator.CreateInstance(type) as T;
            }
            catch (MissingMethodException)
            {
                return null;
            }
        }
    }
}
