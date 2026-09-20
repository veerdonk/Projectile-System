using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
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

        public string Id
        {
            get => id;
            set => id = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        public float LifetimeSeconds
        {
            get => lifetimeSeconds;
            set => lifetimeSeconds = value;
        }

        public float Gravity
        {
            get => gravity;
            set => gravity = value;
        }

        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        public float CriticalChance
        {
            get => criticalChance;
            set => criticalChance = value;
        }

        public float ManaCost
        {
            get => manaCost;
            set => manaCost = value;
        }

        public float EffectRadius
        {
            get => effectRadius;
            set => effectRadius = value;
        }

        public string PresentationId
        {
            get => presentationId;
            set => presentationId = value;
        }

        public int MulticastGenerations
        {
            get => multicastGenerations;
            set => multicastGenerations = value;
        }

        public int MultiplyGenerations
        {
            get => multiplyGenerations;
            set => multiplyGenerations = value;
        }

        public IModifier[] OnSpawn
        {
            get => onSpawn ?? Array.Empty<IModifier>();
            set => onSpawn = value ?? Array.Empty<IModifier>();
        }

        public IModifier[] OnTravel
        {
            get => onTravel ?? Array.Empty<IModifier>();
            set => onTravel = value ?? Array.Empty<IModifier>();
        }

        public IModifier[] OnTick
        {
            get => onTick ?? Array.Empty<IModifier>();
            set => onTick = value ?? Array.Empty<IModifier>();
        }

        public IModifier[] OnInterval
        {
            get => onInterval ?? Array.Empty<IModifier>();
            set => onInterval = value ?? Array.Empty<IModifier>();
        }

        public IModifier[] OnHit
        {
            get => onHit ?? Array.Empty<IModifier>();
            set => onHit = value ?? Array.Empty<IModifier>();
        }

        public IModifier[] OnExpire
        {
            get => onExpire ?? Array.Empty<IModifier>();
            set => onExpire = value ?? Array.Empty<IModifier>();
        }

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
                    return new ApplyStatus(applyStatus.Effect ?? new DummyStatusEffect());
                case AreaEffect area:
                    return new AreaEffect(
                        area.Damage,
                        area.Status,
                        area.IncludeDirectHit,
                        area.IncludeDirectStatus);
                case GroundField field:
                    return new GroundField(
                        field.Radius,
                        field.LifetimeSeconds,
                        field.Damage,
                        field.Status,
                        field.IntervalSeconds,
                        field.PresentationId);
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
            if (pattern is CirclePattern)
                return new CirclePattern();
            if (pattern is RandomCirclePattern)
                return new RandomCirclePattern();
            return new SquarePattern();
        }
    }
}
