using System.Collections.Generic;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    static class SampleRecipeSheet
    {
        static readonly Dictionary<string, string> Buffer = new Dictionary<string, string>();
        static Vector2 scroll;

        public static void ClearBuffers()
        {
            Buffer.Clear();
        }

        public static void Draw(Recipe recipe)
        {
            if (recipe == null)
            {
                GUILayout.Label("No working Recipe.");
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Recipe sheet");
            recipe.Speed = FloatField("speed", "Speed", recipe.Speed);
            recipe.Radius = FloatField("radius", "Radius", recipe.Radius);
            recipe.LifetimeSeconds = FloatField("life", "Lifetime", recipe.LifetimeSeconds);
            recipe.Damage = FloatField("damage", "Damage", recipe.Damage);
            recipe.CriticalChance = FloatField("crit", "Crit", recipe.CriticalChance);
            recipe.ManaCost = FloatField("mana", "Mana", recipe.ManaCost);
            recipe.EffectRadius = FloatField("fxr", "Effect radius", recipe.EffectRadius);
            recipe.Gravity = FloatField("grav", "Gravity", recipe.Gravity);
            recipe.PresentationId = TextField("pres", "Presentation id", recipe.PresentationId ?? "");

            DrawList("OnSpawn", recipe.OnSpawn);
            DrawList("OnTravel", recipe.OnTravel);
            DrawList("OnTick", recipe.OnTick);
            DrawList("OnInterval", recipe.OnInterval);
            DrawList("OnHit", recipe.OnHit);
            DrawList("OnExpire", recipe.OnExpire);
            GUILayout.EndScrollView();
        }

        static void DrawList(string title, IModifier[] list)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title);
            if (list == null || list.Length == 0)
            {
                GUILayout.Label("  —");
                return;
            }

            for (int i = 0; i < list.Length; i++)
                DrawModifier(title + i, list[i]);
        }

        static void DrawModifier(string key, IModifier modifier)
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
                    DrawMultiply(key, multiply);
                    break;
                case Multicast multicast:
                    multicast.Count = IntStepper(key, "Multicast", multicast.Count, 1, 16);
                    GUILayout.Label($"    extra {RecipeName(multicast.ExtraRecipe, multicast.ExtraAsset)}");
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

        static void DrawMultiply(string key, Multiply multiply)
        {
            multiply.Count = IntStepper(key + "n", "Multiply", multiply.Count, 0, 16);
            multiply.Spread = FloatField(key + "s", "  Spread m", multiply.Spread);
            GUILayout.BeginHorizontal();
            GUILayout.Label("  Pattern", GUILayout.Width(140f));
            if (GUILayout.Button(PatternName(multiply.Pattern), GUILayout.Height(22f)))
                multiply.Pattern = NextPattern(multiply.Pattern);
            GUILayout.EndHorizontal();
            GUILayout.Label($"    child {RecipeName(multiply.ChildRecipe, multiply.ChildAsset)}");
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
            if (pattern is CirclePattern)
                return "Circle";
            if (pattern is RandomCirclePattern)
                return "RandomCircle";
            return "Square";
        }

        static IMultiplyPattern NextPattern(IMultiplyPattern pattern)
        {
            if (pattern is SquarePattern)
                return new CirclePattern();
            if (pattern is CirclePattern)
                return new RandomCirclePattern();
            return new SquarePattern();
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
