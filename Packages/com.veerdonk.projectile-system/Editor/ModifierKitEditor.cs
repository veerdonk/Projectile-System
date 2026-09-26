using System;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Inspector for <see cref="ModifierKit"/>: one stage dropdown and a filtered Add menu per layer.</summary>
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
                EditorGUILayout.PropertyField(child, true);
                if (!child.NextVisible(false))
                    break;
            }
        }
    }
}
