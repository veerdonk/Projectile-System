using System;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Inspector for <see cref="ModifierKit"/>: one stage dropdown and a filtered Add menu per layer.
    /// <see cref="IStatusEffect"/> and <see cref="IMultiplyPattern"/> fields use the same shipped-type
    /// dropdowns as <see cref="RecipeAsset"/>. Pattern has no empty choice; a missing pattern shows
    /// <see cref="SquarePattern"/>.
    /// </summary>
    [CustomEditor(typeof(ModifierKit))]
    public sealed class ModifierKitEditor : UnityEditor.Editor
    {
        static readonly RecipeStage[] Stages =
        {
            RecipeStage.OnSpawn,
            RecipeStage.OnTravel,
            RecipeStage.OnTick,
            RecipeStage.OnInterval,
            RecipeStage.OnHit,
            RecipeStage.OnExpire
        };

        static readonly string[] StageLabels =
        {
            "On Spawn",
            "On Travel",
            "On Tick",
            "On Interval",
            "On Hit",
            "On Expire"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (!DrawLayer(layers, i))
                    break;
            }

            if (GUILayout.Button("Add Layer"))
            {
                int index = layers.arraySize;
                layers.arraySize++;
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                layer.FindPropertyRelative("stage").intValue = (int)RecipeStage.OnHit;
                layer.FindPropertyRelative("modifiers").arraySize = 0;
            }

            serializedObject.ApplyModifiedProperties();
        }

        bool DrawLayer(SerializedProperty layers, int index)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Layer " + (index + 1), EditorStyles.boldLabel);
            bool removeLayer = GUILayout.Button("Remove", GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();

            if (removeLayer)
            {
                layers.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndVertical();
                return false;
            }

            SerializedProperty stageProperty = layer.FindPropertyRelative("stage");
            RecipeStage stage = StagePopup((RecipeStage)stageProperty.intValue);
            stageProperty.intValue = (int)stage;

            SerializedProperty modifiers = layer.FindPropertyRelative("modifiers");
            for (int i = 0; i < modifiers.arraySize; i++)
            {
                SerializedProperty element = modifiers.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                object value = element.managedReferenceValue;
                EditorGUILayout.LabelField(value == null ? "(missing modifier)" : value.GetType().Name, EditorStyles.boldLabel);
                bool remove = GUILayout.Button("Remove", GUILayout.Width(70f));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    modifiers.DeleteArrayElementAtIndex(i);
                    break;
                }

                DrawFields(element);
            }

            if (GUILayout.Button("Add Modifier"))
                ShowAddMenu(modifiers, stage);

            EditorGUILayout.EndVertical();
            return true;
        }

        static RecipeStage StagePopup(RecipeStage current)
        {
            int selected = 4;
            for (int i = 0; i < Stages.Length; i++)
            {
                if (Stages[i] == current)
                {
                    selected = i;
                    break;
                }
            }

            int next = EditorGUILayout.Popup("Stage", selected, StageLabels);
            return Stages[next];
        }

        void ShowAddMenu(SerializedProperty listProperty, RecipeStage stage)
        {
            var menu = new GenericMenu();
            Type[] types = RecipeAssetEditor.ModifiersFor(stage);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(type.Name), false, () => AddModifier(listProperty, type));
            }

            if (types.Length == 0)
                menu.AddDisabledItem(new GUIContent("No modifiers for this stage"));

            menu.ShowAsContext();
        }

        void AddModifier(SerializedProperty listProperty, Type type)
        {
            serializedObject.Update();
            int index = listProperty.arraySize;
            listProperty.arraySize++;
            listProperty.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawFields(SerializedProperty element)
        {
            SerializedProperty end = element.GetEndProperty();
            SerializedProperty child = element.Copy();
            if (!child.NextVisible(true))
                return;

            while (!SerializedProperty.EqualContents(child, end))
            {
                if (IsStatusEffectField(child))
                    DrawStatusPopup(child);
                else if (IsMultiplyPatternField(child))
                    DrawPatternPopup(child);
                else
                    EditorGUILayout.PropertyField(child, true);
                if (!child.NextVisible(false))
                    break;
            }
        }

        static bool IsStatusEffectField(SerializedProperty property)
        {
            return IsManagedReference(property, ".IStatusEffect");
        }

        static bool IsMultiplyPatternField(SerializedProperty property)
        {
            return IsManagedReference(property, ".IMultiplyPattern");
        }

        static bool IsManagedReference(SerializedProperty property, string typeSuffix)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return false;

            string typeName = property.managedReferenceFieldTypename;
            return typeName != null && typeName.EndsWith(typeSuffix, StringComparison.Ordinal);
        }

        static void DrawStatusPopup(SerializedProperty property)
        {
            Type[] types = RecipeAssetEditor.StatusEffects();
            var labels = new string[types.Length + 1];
            labels[0] = "None";
            for (int i = 0; i < types.Length; i++)
                labels[i + 1] = ObjectNames.NicifyVariableName(types[i].Name);

            object currentValue = property.managedReferenceValue;
            int current = 0;
            if (currentValue != null)
            {
                int found = IndexOf(types, currentValue.GetType());
                if (found >= 0)
                    current = found + 1;
            }

            int next = EditorGUILayout.Popup("Status", current, labels);
            if (next != current)
                AssignStatusEffect(property, next);
        }

        static void DrawPatternPopup(SerializedProperty property)
        {
            RecipeAssetEditor.DrawPatternPopupLayout(property);
        }

        /// <summary>
        /// Writes a shipped <see cref="IStatusEffect"/> onto a SerializeReference field.
        /// Index 0 clears it. Later indexes follow <see cref="RecipeAssetEditor.StatusEffects"/>.
        /// </summary>
        public static void AssignStatusEffect(SerializedProperty property, int index)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            Type[] types = RecipeAssetEditor.StatusEffects();
            if (index <= 0)
            {
                property.managedReferenceValue = null;
                return;
            }

            int typeIndex = index - 1;
            if (typeIndex >= types.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            property.managedReferenceValue = Activator.CreateInstance(types[typeIndex]);
        }

        /// <summary>
        /// Popup index for a Multiply pattern. Indexes follow <see cref="RecipeAssetEditor.PatternOptions"/>
        /// for a procedural type or a grid that matches a project asset.
        /// A null, unknown, or unmatched value reports <see cref="SquarePattern"/>. The inspector does not
        /// write that stand-in until the popup index changes. Unmatched grids are drawn by
        /// <see cref="RecipeAssetEditor.DrawPatternPopupLayout"/>, which keeps their label.
        /// </summary>
        public static int PatternPopupIndex(object currentValue)
        {
            int index = RecipeAssetEditor.PatternOptionIndex(currentValue);
            if (index >= 0)
                return index;

            Type[] types = RecipeAssetEditor.Patterns();
            int square = IndexOf(types, typeof(SquarePattern));
            return square < 0 ? 0 : square;
        }

        /// <summary>
        /// Writes a shipped <see cref="IMultiplyPattern"/> onto a SerializeReference field.
        /// Indexes follow <see cref="RecipeAssetEditor.Patterns"/>. There is no empty choice.
        /// </summary>
        public static void AssignMultiplyPattern(SerializedProperty property, int index)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            Type[] types = RecipeAssetEditor.Patterns();
            if (index < 0 || index >= types.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            property.managedReferenceValue = Activator.CreateInstance(types[index]);
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
