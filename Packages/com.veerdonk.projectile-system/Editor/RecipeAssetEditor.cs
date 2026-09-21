using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Inspector for <see cref="RecipeAsset"/>: base stats and six stage-filtered modifier lists.</summary>
    [CustomEditor(typeof(RecipeAsset))]
    public sealed class RecipeAssetEditor : UnityEditor.Editor
    {
        SerializedProperty id;
        SerializedProperty speed;
        SerializedProperty radius;
        SerializedProperty lifetimeSeconds;
        SerializedProperty gravity;
        SerializedProperty damage;
        SerializedProperty criticalChance;
        SerializedProperty manaCost;
        SerializedProperty effectRadius;
        SerializedProperty presentationId;
        SerializedProperty multicastGenerations;
        SerializedProperty multiplyGenerations;

        ReorderableList onSpawn;
        ReorderableList onTravel;
        ReorderableList onTick;
        ReorderableList onInterval;
        ReorderableList onHit;
        ReorderableList onExpire;

        /// <summary><see cref="IModifier"/> types whose <see cref="ModifierStageAttribute"/> includes <paramref name="stage"/>.</summary>
        public static Type[] ModifiersFor(RecipeStage stage)
        {
            var result = new List<Type>();
            foreach (Type type in ConcreteImplementations<IModifier>())
            {
                ModifierStageAttribute attribute = type.GetCustomAttribute<ModifierStageAttribute>();
                if (attribute == null)
                    continue;
                if ((attribute.Stages & stage) == 0)
                    continue;
                result.Add(type);
            }

            return result.ToArray();
        }

        /// <summary>Shipped <see cref="IMultiplyPattern"/> implementations.</summary>
        public static Type[] Patterns()
        {
            return ConcreteImplementations<IMultiplyPattern>();
        }

        /// <summary>Shipped <see cref="IStatusEffect"/> implementations.</summary>
        public static Type[] StatusEffects()
        {
            return ConcreteImplementations<IStatusEffect>();
        }

        static Type[] ConcreteImplementations<T>()
        {
            var result = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;
                result.Add(type);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result.ToArray();
        }

        void OnEnable()
        {
            id = serializedObject.FindProperty("id");
            speed = serializedObject.FindProperty("speed");
            radius = serializedObject.FindProperty("radius");
            lifetimeSeconds = serializedObject.FindProperty("lifetimeSeconds");
            gravity = serializedObject.FindProperty("gravity");
            damage = serializedObject.FindProperty("damage");
            criticalChance = serializedObject.FindProperty("criticalChance");
            manaCost = serializedObject.FindProperty("manaCost");
            effectRadius = serializedObject.FindProperty("effectRadius");
            presentationId = serializedObject.FindProperty("presentationId");
            multicastGenerations = serializedObject.FindProperty("multicastGenerations");
            multiplyGenerations = serializedObject.FindProperty("multiplyGenerations");

            onSpawn = CreateList("onSpawn", "On Spawn", RecipeStage.OnSpawn);
            onTravel = CreateList("onTravel", "On Travel", RecipeStage.OnTravel);
            onTick = CreateList("onTick", "On Tick", RecipeStage.OnTick);
            onInterval = CreateList("onInterval", "On Interval", RecipeStage.OnInterval);
            onHit = CreateList("onHit", "On Hit", RecipeStage.OnHit);
            onExpire = CreateList("onExpire", "On Expire", RecipeStage.OnExpire);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawStats();
            onSpawn.DoLayoutList();
            onTravel.DoLayoutList();
            onTick.DoLayoutList();
            onInterval.DoLayoutList();
            onHit.DoLayoutList();
            onExpire.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawStats()
        {
            EditorGUILayout.PropertyField(id);
            EditorGUILayout.PropertyField(speed);
            EditorGUILayout.PropertyField(radius);
            EditorGUILayout.PropertyField(lifetimeSeconds);
            EditorGUILayout.PropertyField(gravity);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(criticalChance);
            EditorGUILayout.PropertyField(manaCost);
            EditorGUILayout.PropertyField(effectRadius);
            EditorGUILayout.PropertyField(presentationId);
            EditorGUILayout.PropertyField(multicastGenerations);
            EditorGUILayout.PropertyField(multiplyGenerations);
        }

        ReorderableList CreateList(string propertyName, string header, RecipeStage stage)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            var list = new ReorderableList(serializedObject, property, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
                elementHeightCallback = index => ElementHeight(property.GetArrayElementAtIndex(index)),
                drawElementCallback = (rect, index, active, focused) =>
                    DrawElement(rect, property.GetArrayElementAtIndex(index)),
                onAddDropdownCallback = (buttonRect, _) => ShowAddMenu(buttonRect, property, stage)
            };
            return list;
        }

        void ShowAddMenu(Rect buttonRect, SerializedProperty listProperty, RecipeStage stage)
        {
            var menu = new GenericMenu();
            Type[] types = ModifiersFor(stage);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(type.Name), false, () => AddModifier(listProperty, type));
            }

            if (types.Length == 0)
                menu.AddDisabledItem(new GUIContent("No modifiers for this stage"));

            menu.DropDown(buttonRect);
        }

        void AddModifier(SerializedProperty listProperty, Type type)
        {
            serializedObject.Update();
            int index = listProperty.arraySize;
            listProperty.arraySize++;
            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        static float Line => EditorGUIUtility.singleLineHeight + 2f;

        static float ElementHeight(SerializedProperty element)
        {
            return Line * (1 + FieldCount(element.managedReferenceValue));
        }

        static int FieldCount(object value)
        {
            switch (value)
            {
                case Pierce:
                case Bounce:
                case Interval:
                case Speed:
                case Damage:
                case Critical:
                case Mana:
                case Spread:
                case EffectRadius:
                case Size:
                case Gravity:
                case Homing:
                case ApplyStatus:
                    return 1;
                case Multicast:
                    return 2;
                case Multiply:
                    return 4;
                case AreaEffect:
                    return 4;
                case GroundField:
                    return 6;
                default:
                    return 0;
            }
        }

        static void DrawElement(Rect rect, SerializedProperty element)
        {
            object value = element.managedReferenceValue;
            if (value == null)
            {
                EditorGUI.LabelField(rect, "(missing modifier)");
                return;
            }

            Rect line = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(line, value.GetType().Name, EditorStyles.boldLabel);
            line.y += Line;

            switch (value)
            {
                case Pierce:
                case Bounce:
                case Multicast:
                case Multiply:
                    DrawProperty(ref line, element, "count");
                    break;
                case Interval:
                    DrawProperty(ref line, element, "seconds");
                    break;
                case Speed:
                case Damage:
                case Critical:
                case Mana:
                case Spread:
                case EffectRadius:
                case Size:
                case Gravity:
                    DrawProperty(ref line, element, "scale");
                    break;
                case Homing:
                    DrawProperty(ref line, element, "turnRateDegreesPerSecond", "Turn Rate (°/s)");
                    break;
                case ApplyStatus:
                    DrawStatus(ref line, element.FindPropertyRelative("effect"));
                    break;
            }

            if (value is Multiply)
            {
                DrawProperty(ref line, element, "spread");
                DrawProperty(ref line, element, "childAsset", "Child Recipe");
                DrawPattern(ref line, element.FindPropertyRelative("pattern"));
            }
            else if (value is Multicast)
            {
                DrawProperty(ref line, element, "extraAsset", "Extra Recipe");
            }
            else if (value is AreaEffect)
            {
                DrawProperty(ref line, element, "damage");
                DrawStatus(ref line, element.FindPropertyRelative("status"));
                DrawProperty(ref line, element, "includeDirectHit");
                DrawProperty(ref line, element, "includeDirectStatus");
            }
            else if (value is GroundField)
            {
                DrawProperty(ref line, element, "radius");
                DrawProperty(ref line, element, "lifetimeSeconds");
                DrawProperty(ref line, element, "damage");
                DrawStatus(ref line, element.FindPropertyRelative("status"));
                DrawProperty(ref line, element, "intervalSeconds");
                DrawProperty(ref line, element, "presentationId");
            }
        }

        static void DrawProperty(ref Rect line, SerializedProperty element, string field, string label = null)
        {
            SerializedProperty property = element.FindPropertyRelative(field);
            if (property == null)
                return;
            GUIContent content = label == null ? new GUIContent(property.displayName) : new GUIContent(label);
            EditorGUI.PropertyField(line, property, content);
            line.y += Line;
        }

        static void DrawPattern(ref Rect line, SerializedProperty property)
        {
            DrawTypePopup(ref line, property, "Pattern", Patterns(), includeNone: false);
        }

        static void DrawStatus(ref Rect line, SerializedProperty property)
        {
            DrawTypePopup(ref line, property, "Status", StatusEffects(), includeNone: true);
        }

        static void DrawTypePopup(
            ref Rect line,
            SerializedProperty property,
            string label,
            Type[] types,
            bool includeNone)
        {
            if (property == null)
                return;

            int offset = includeNone ? 1 : 0;
            var labels = new string[types.Length + offset];
            if (includeNone)
                labels[0] = "None";
            for (int i = 0; i < types.Length; i++)
                labels[i + offset] = ObjectNames.NicifyVariableName(types[i].Name);

            object currentValue = property.managedReferenceValue;
            int current = includeNone ? 0 : IndexOf(types, typeof(SquarePattern));
            if (current < 0)
                current = 0;
            if (currentValue != null)
            {
                int found = IndexOf(types, currentValue.GetType());
                if (found >= 0)
                    current = found + offset;
            }

            int next = EditorGUI.Popup(line, label, current, labels);
            if (next != current)
            {
                if (includeNone && next == 0)
                    property.managedReferenceValue = null;
                else
                    property.managedReferenceValue = Activator.CreateInstance(types[next - offset]);
            }

            line.y += Line;
        }

        static int IndexOf(Type[] types, Type match)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == match)
                    return i;
            }

            return -1;
        }
    }
}
