using System;
using System.Collections.Generic;
using System.Reflection;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;

namespace Sandbox
{
    public static class SampleModifierCatalog
    {
        const float AreaDamage = 3f;
        const float AreaEffectRadius = 3f;
        const float GroundRadius = 2.25f;
        const float GroundLifetime = 2f;
        const float GroundDamage = 3f;
        const float GroundInterval = 0.5f;

        static Type[] modifierTypes;

        public static Type[] ModifiersFor(RecipeStage stage)
        {
            Type[] all = AllModifierTypes();
            var result = new List<Type>();
            for (int i = 0; i < all.Length; i++)
            {
                Type type = all[i];
                ModifierStageAttribute attribute = type.GetCustomAttribute<ModifierStageAttribute>();
                if (attribute == null)
                    continue;
                if ((attribute.Stages & stage) == 0)
                    continue;
                result.Add(type);
            }

            return result.ToArray();
        }

        public static IModifier Create(Type type, Recipe defaultChild, Recipe working = null)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Recipe child = defaultChild ?? new Recipe { Id = "straight", Speed = 20f, LifetimeSeconds = 5f, Radius = 0.25f };

            if (type == typeof(Pierce))
                return new Pierce(1);
            if (type == typeof(Bounce))
                return new Bounce(1);
            if (type == typeof(Interval))
                return new Interval(0.5f);
            if (type == typeof(Speed))
                return new Speed(1f);
            if (type == typeof(Size))
                return new Size(1f);
            if (type == typeof(Damage))
                return new Damage(1f);
            if (type == typeof(Critical))
                return new Critical(1f);
            if (type == typeof(Mana))
                return new Mana(1f);
            if (type == typeof(Spread))
                return new Spread(1f);
            if (type == typeof(EffectRadius))
                return new EffectRadius(1f);
            if (type == typeof(Gravity))
                return new Gravity(1f);
            if (type == typeof(Homing))
                return new Homing(100f);
            if (type == typeof(ApplyStatus))
                return new ApplyStatus(new DummyStatusEffect());
            if (type == typeof(Multiply))
                return new Multiply(2, child, 2f, new SquarePattern());
            if (type == typeof(Multicast))
                return new Multicast(2, child);
            if (type == typeof(AreaEffect))
            {
                if (working != null && working.EffectRadius <= 0f)
                    working.EffectRadius = AreaEffectRadius;
                return new AreaEffect(AreaDamage);
            }

            if (type == typeof(GroundField))
                return new GroundField(GroundRadius, GroundLifetime, GroundDamage, intervalSeconds: GroundInterval);

            try
            {
                return (IModifier)Activator.CreateInstance(type);
            }
            catch (MissingMethodException)
            {
                throw new InvalidOperationException($"Cannot create modifier '{type.Name}' without a parameterless constructor.");
            }
        }

        public static Recipe DefaultChild(SampleLab.Preset[] presets)
        {
            RecipeAsset asset = FindStraight(presets) ?? FirstAsset(presets);
            if (asset != null)
                return asset.ToRecipe();

            return new Recipe { Id = "straight", Speed = 20f, LifetimeSeconds = 5f, Radius = 0.25f };
        }

        public static IModifier[] GetList(Recipe recipe, RecipeStage stage)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            switch (stage)
            {
                case RecipeStage.OnSpawn: return recipe.OnSpawn ?? Array.Empty<IModifier>();
                case RecipeStage.OnTravel: return recipe.OnTravel ?? Array.Empty<IModifier>();
                case RecipeStage.OnTick: return recipe.OnTick ?? Array.Empty<IModifier>();
                case RecipeStage.OnInterval: return recipe.OnInterval ?? Array.Empty<IModifier>();
                case RecipeStage.OnHit: return recipe.OnHit ?? Array.Empty<IModifier>();
                case RecipeStage.OnExpire: return recipe.OnExpire ?? Array.Empty<IModifier>();
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        public static void SetList(Recipe recipe, RecipeStage stage, IModifier[] list)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            IModifier[] value = list ?? Array.Empty<IModifier>();
            switch (stage)
            {
                case RecipeStage.OnSpawn:
                    recipe.OnSpawn = value;
                    break;
                case RecipeStage.OnTravel:
                    recipe.OnTravel = value;
                    break;
                case RecipeStage.OnTick:
                    recipe.OnTick = value;
                    break;
                case RecipeStage.OnInterval:
                    recipe.OnInterval = value;
                    break;
                case RecipeStage.OnHit:
                    recipe.OnHit = value;
                    break;
                case RecipeStage.OnExpire:
                    recipe.OnExpire = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        public static void Append(Recipe recipe, RecipeStage stage, IModifier modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            IModifier[] list = GetList(recipe, stage);
            var next = new IModifier[list.Length + 1];
            Array.Copy(list, next, list.Length);
            next[list.Length] = modifier;
            SetList(recipe, stage, next);
        }

        public static void RemoveAt(Recipe recipe, RecipeStage stage, int index)
        {
            IModifier[] list = GetList(recipe, stage);
            if (index < 0 || index >= list.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (list.Length == 1)
            {
                SetList(recipe, stage, Array.Empty<IModifier>());
                return;
            }

            var next = new IModifier[list.Length - 1];
            if (index > 0)
                Array.Copy(list, 0, next, 0, index);
            if (index < list.Length - 1)
                Array.Copy(list, index + 1, next, index, list.Length - index - 1);
            SetList(recipe, stage, next);
        }

        public static void ReplaceAt(Recipe recipe, RecipeStage stage, int index, IModifier modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            IModifier[] list = GetList(recipe, stage);
            if (index < 0 || index >= list.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            list[index] = modifier;
        }

        static RecipeAsset FindStraight(SampleLab.Preset[] presets)
        {
            if (presets == null)
                return null;

            for (int i = 0; i < presets.Length; i++)
            {
                SampleLab.Preset preset = presets[i];
                if (preset == null || preset.asset == null)
                    continue;
                if (IsStraight(preset))
                    return preset.asset;
            }

            return null;
        }

        static RecipeAsset FirstAsset(SampleLab.Preset[] presets)
        {
            if (presets == null)
                return null;

            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i]?.asset != null)
                    return presets[i].asset;
            }

            return null;
        }

        static bool IsStraight(SampleLab.Preset preset)
        {
            if (string.Equals(preset.label, "Straight", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(preset.asset.Id, "straight", StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(preset.asset.name, "Straight", StringComparison.OrdinalIgnoreCase);
        }

        static Type[] AllModifierTypes()
        {
            if (modifierTypes != null)
                return modifierTypes;

            var result = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                if (types == null)
                    continue;

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                        continue;
                    if (!typeof(IModifier).IsAssignableFrom(type))
                        continue;
                    if (type.GetCustomAttribute<ModifierStageAttribute>() == null)
                        continue;
                    result.Add(type);
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            modifierTypes = result.ToArray();
            return modifierTypes;
        }
    }
}
