using System;
using System.Collections.Generic;
using Veerdonk.ProjectileSystem;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Samples
{
    static class SampleRecipeSheet
    {
        static readonly Dictionary<string, string> Buffer = new Dictionary<string, string>();
        static Vector2 scroll;
        static RecipeStage? addMenuStage;
        static string childMenuKey;

        public static void ClearBuffers()
        {
            Buffer.Clear();
            addMenuStage = null;
            childMenuKey = null;
        }

        public static void Draw(Recipe recipe, SampleLab.Preset[] presets, MultiplyPatternAsset[] patternAssets = null)
        {
            if (recipe == null)
            {
                GUILayout.Label("No working Recipe.");
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Recipe sheet");
            recipe.Delivery = DeliveryField(recipe.Delivery);
            recipe.BeamPulseSeconds = FloatField("beam", "Beam pulse", recipe.BeamPulseSeconds);
            recipe.Speed = FloatField("speed", "Speed", recipe.Speed);
            recipe.Radius = FloatField("radius", "Radius", recipe.Radius);
            recipe.LifetimeSeconds = FloatField("life", "Lifetime", recipe.LifetimeSeconds);
            recipe.Damage = FloatField("damage", "Damage", recipe.Damage);
            recipe.CriticalChance = FloatField("crit", "Crit", recipe.CriticalChance);
            recipe.ManaCost = FloatField("mana", "Mana", recipe.ManaCost);
            recipe.EffectRadius = FloatField("fxr", "Effect radius", recipe.EffectRadius);
            recipe.Gravity = FloatField("grav", "Gravity", recipe.Gravity);
            recipe.PresentationId = TextField("pres", "Presentation id", recipe.PresentationId ?? "");

            DrawList("OnSpawn", recipe, RecipeStage.OnSpawn, presets, patternAssets);
            DrawList("OnTravel", recipe, RecipeStage.OnTravel, presets, patternAssets);
            DrawList("OnTick", recipe, RecipeStage.OnTick, presets, patternAssets);
            DrawList("OnInterval", recipe, RecipeStage.OnInterval, presets, patternAssets);
            DrawList("OnHit", recipe, RecipeStage.OnHit, presets, patternAssets);
            DrawList("OnExpire", recipe, RecipeStage.OnExpire, presets, patternAssets);
            GUILayout.EndScrollView();
        }

        static void DrawList(
            string title,
            Recipe recipe,
            RecipeStage stage,
            SampleLab.Preset[] presets,
            MultiplyPatternAsset[] patternAssets)
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(title);
            if (GUILayout.Button("Add", GUILayout.Width(48f), GUILayout.Height(22f)))
            {
                addMenuStage = addMenuStage == stage ? null : stage;
                childMenuKey = null;
            }

            GUILayout.EndHorizontal();

            if (addMenuStage == stage)
                DrawAddMenu(recipe, stage, presets);

            IModifier[] list = SampleModifierCatalog.GetList(recipe, stage);
            if (list.Length == 0)
            {
                GUILayout.Label("  —");
                return;
            }

            for (int i = 0; i < list.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                DrawModifier(title + i, list[i], recipe, stage, i, presets, patternAssets);
                GUILayout.EndVertical();
                if (GUILayout.Button("Remove", GUILayout.Width(64f), GUILayout.Height(22f)))
                {
                    SampleModifierCatalog.RemoveAt(recipe, stage, i);
                    GUILayout.EndHorizontal();
                    return;
                }

                GUILayout.EndHorizontal();
            }
        }

        static void DrawAddMenu(Recipe recipe, RecipeStage stage, SampleLab.Preset[] presets)
        {
            Type[] types = SampleModifierCatalog.ModifiersFor(stage);
            if (types.Length == 0)
            {
                GUILayout.Label("  No modifiers for this stage");
                return;
            }

            Recipe defaultChild = SampleModifierCatalog.DefaultChild(presets);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (!GUILayout.Button(type.Name, GUILayout.Height(22f)))
                    continue;

                SampleModifierCatalog.Append(recipe, stage, SampleModifierCatalog.Create(type, defaultChild, recipe));
                addMenuStage = null;
                return;
            }
        }

        static void DrawModifier(
            string key,
            IModifier modifier,
            Recipe recipe,
            RecipeStage stage,
            int index,
            SampleLab.Preset[] presets,
            MultiplyPatternAsset[] patternAssets)
        {
            switch (modifier)
            {
                case Pierce pierce:
                    pierce.Count = IntStepper(key, "Pierce", pierce.Count, 0, 16);
                    break;
                case Bounce bounce:
                    bounce.Count = IntStepper(key, "Bounce", bounce.Count, 0, 16);
                    break;
                case Interval interval:
                    interval.Seconds = FloatField(key, "Interval", interval.Seconds);
                    break;
                case Speed speed:
                    speed.Scale = FloatField(key, "Speed", speed.Scale);
                    break;
                case Size size:
                    size.Scale = FloatField(key, "Size", size.Scale);
                    break;
                case Damage damage:
                    damage.Scale = FloatField(key, "Damage", damage.Scale);
                    break;
                case Critical critical:
                    critical.Scale = FloatField(key, "Critical", critical.Scale);
                    break;
                case Mana mana:
                    mana.Scale = FloatField(key, "Mana", mana.Scale);
                    break;
                case Spread spread:
                    spread.Scale = FloatField(key, "Spread", spread.Scale);
                    break;
                case EffectRadius effectRadius:
                    effectRadius.Scale = FloatField(key, "EffectRadius", effectRadius.Scale);
                    break;
                case Gravity gravity:
                    gravity.Scale = FloatField(key, "Gravity", gravity.Scale);
                    break;
                case Homing homing:
                    homing.TurnRateDegreesPerSecond = FloatField(key, "Homing °/s", homing.TurnRateDegreesPerSecond);
                    break;
                case ApplyStatus apply:
                    GUILayout.Label($"  ApplyStatus {EffectName(apply.Effect)}");
                    break;
                case Multiply multiply:
                    DrawMultiply(key, multiply, recipe, stage, index, presets, patternAssets);
                    break;
                case Multicast multicast:
                    DrawMulticast(key, multicast, recipe, stage, index, presets);
                    break;
                case AreaEffect area:
                    DrawArea(key, area);
                    break;
                case GroundField field:
                    DrawGroundField(key, field);
                    break;
                default:
                    GUILayout.Label(modifier == null ? "  (null)" : $"  {modifier.GetType().Name}");
                    break;
            }
        }

        static void DrawArea(string key, AreaEffect area)
        {
            area.Damage = FloatField(key + "d", "AreaEffect dmg", area.Damage);
            area.IncludeDirectHit = Toggle("  Direct hit", area.IncludeDirectHit);
            area.IncludeDirectStatus = Toggle("  Direct status", area.IncludeDirectStatus);
            GUILayout.Label($"    status {EffectName(area.Status)}");
        }

        static void DrawGroundField(string key, GroundField field)
        {
            field.Radius = FloatField(key + "r", "GroundField r", field.Radius);
            field.LifetimeSeconds = FloatField(key + "l", "  Lifetime", field.LifetimeSeconds);
            field.Damage = FloatField(key + "d", "  Damage", field.Damage);
            field.IntervalSeconds = FloatField(key + "i", "  Interval", field.IntervalSeconds);
            field.PresentationId = TextField(key + "p", "  Presentation id", field.PresentationId ?? "");
            GUILayout.Label($"    status {EffectName(field.Status)}");
        }

        static bool Toggle(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            bool next = GUILayout.Toggle(value, "");
            GUILayout.EndHorizontal();
            return next;
        }

        static void DrawMultiply(
            string key,
            Multiply multiply,
            Recipe recipe,
            RecipeStage stage,
            int index,
            SampleLab.Preset[] presets,
            MultiplyPatternAsset[] patternAssets)
        {
            multiply.Count = IntStepper(key + "n", "Multiply", multiply.Count, 0, 16);
            string spreadLabel = multiply.Pattern is GridMultiplyPattern ? "  Spread °" : "  Spread m";
            multiply.Spread = FloatField(key + "s", spreadLabel, multiply.Spread);
            GUILayout.BeginHorizontal();
            GUILayout.Label("  Pattern", GUILayout.Width(140f));
            if (GUILayout.Button(PatternName(multiply.Pattern), GUILayout.Height(22f)))
                multiply.Pattern = NextPattern(multiply.Pattern, patternAssets);
            GUILayout.EndHorizontal();
            DrawChildPicker(
                key + "c",
                "  Child",
                RecipeName(multiply.ChildRecipe, multiply.ChildAsset),
                presets,
                chosen => SampleModifierCatalog.ReplaceAt(
                    recipe,
                    stage,
                    index,
                    new Multiply(multiply.Count, chosen, multiply.Spread, multiply.Pattern)));
        }

        static void DrawMulticast(
            string key,
            Multicast multicast,
            Recipe recipe,
            RecipeStage stage,
            int index,
            SampleLab.Preset[] presets)
        {
            multicast.Count = IntStepper(key, "Multicast", multicast.Count, 1, 16);
            DrawChildPicker(
                key + "e",
                "  Extra",
                RecipeName(multicast.ExtraRecipe, multicast.ExtraAsset),
                presets,
                chosen => SampleModifierCatalog.ReplaceAt(
                    recipe,
                    stage,
                    index,
                    new Multicast(multicast.Count, chosen)));
        }

        static void DrawChildPicker(
            string key,
            string label,
            string current,
            SampleLab.Preset[] presets,
            System.Action<Recipe> choose)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            if (GUILayout.Button(current, GUILayout.Height(22f)))
            {
                childMenuKey = childMenuKey == key ? null : key;
                addMenuStage = null;
            }

            GUILayout.EndHorizontal();
            if (childMenuKey != key || presets == null)
                return;

            for (int i = 0; i < presets.Length; i++)
            {
                SampleLab.Preset preset = presets[i];
                if (preset == null || preset.asset == null)
                    continue;
                string name = string.IsNullOrEmpty(preset.label) ? preset.asset.name : preset.label;
                if (!GUILayout.Button(name, GUILayout.Height(22f)))
                    continue;

                choose(preset.asset.ToRecipe());
                childMenuKey = null;
                return;
            }
        }

        static int IntStepper(string key, string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            if (GUILayout.Button("-", GUILayout.Width(24f), GUILayout.Height(22f)))
                value--;
            GUILayout.Label(value.ToString(), GUILayout.Width(36f));
            if (GUILayout.Button("+", GUILayout.Width(24f), GUILayout.Height(22f)))
                value++;
            GUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, max);
        }

        static readonly string[] DeliveryLabels = { "Straight", "Arc", "Hitscan", "Beam" };

        static Delivery DeliveryField(Delivery value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Delivery", GUILayout.Width(140f));
            int next = GUILayout.Toolbar((int)value, DeliveryLabels);
            GUILayout.EndHorizontal();
            return (Delivery)next;
        }

        static float FloatField(string key, string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            string control = "f_" + key;
            GUI.SetNextControlName(control);
            bool focused = GUI.GetNameOfFocusedControl() == control;
            if (!Buffer.TryGetValue(key, out string text) || !focused)
                text = value.ToString("0.###");
            string next = GUILayout.TextField(text);
            Buffer[key] = next;
            GUILayout.EndHorizontal();
            if (!focused && float.TryParse(next, out float parsed))
                return parsed;
            if (focused && float.TryParse(next, out parsed))
                return parsed;
            return value;
        }

        static string TextField(string key, string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140f));
            string control = "t_" + key;
            GUI.SetNextControlName(control);
            bool focused = GUI.GetNameOfFocusedControl() == control;
            if (!Buffer.TryGetValue(key, out string text) || !focused)
                text = value ?? "";
            string next = GUILayout.TextField(text);
            Buffer[key] = next;
            GUILayout.EndHorizontal();
            return next ?? "";
        }

        static string PatternName(IMultiplyPattern pattern)
        {
            if (pattern is GridMultiplyPattern grid)
                return string.IsNullOrEmpty(grid.SourceName) ? "Grid" : grid.SourceName;
            if (pattern is CirclePattern)
                return "Circle";
            if (pattern is RandomCirclePattern)
                return "RandomCircle";
            return "Square";
        }

        static IMultiplyPattern NextPattern(IMultiplyPattern pattern, MultiplyPatternAsset[] patternAssets)
        {
            IMultiplyPattern[] catalog = PatternCatalog(patternAssets);
            int index = 0;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (!SamePattern(catalog[i], pattern))
                    continue;
                index = i;
                break;
            }

            return catalog[(index + 1) % catalog.Length];
        }

        static IMultiplyPattern[] PatternCatalog(MultiplyPatternAsset[] patternAssets)
        {
            var list = new List<IMultiplyPattern>
            {
                new SquarePattern(),
                new CirclePattern(),
                new RandomCirclePattern()
            };
            if (patternAssets == null)
                return list.ToArray();

            var assets = new List<MultiplyPatternAsset>();
            for (int i = 0; i < patternAssets.Length; i++)
            {
                if (patternAssets[i] != null)
                    assets.Add(patternAssets[i]);
            }

            assets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < assets.Count; i++)
                list.Add(assets[i].ToPattern());
            return list.ToArray();
        }

        static bool SamePattern(IMultiplyPattern catalogEntry, IMultiplyPattern current)
        {
            if (catalogEntry == null || current == null)
                return false;
            if (catalogEntry is GridMultiplyPattern grid && current is GridMultiplyPattern other)
                return grid.HasSameLayout(other);
            return catalogEntry.GetType() == current.GetType();
        }

        static string RecipeName(Recipe recipe, RecipeAsset asset)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.Id))
                return recipe.Id;
            if (asset != null)
                return string.IsNullOrEmpty(asset.Id) ? asset.name : asset.Id;
            return "-";
        }

        static string EffectName(IStatusEffect effect)
        {
            return effect == null || string.IsNullOrEmpty(effect.Id) ? "-" : effect.Id;
        }
    }
}
