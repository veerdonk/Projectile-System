using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.Editor
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Sandbox.unity</c> as the sample (turret, yard, presets, ballistic Cannonball).
    /// </summary>
    public static class SampleSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Projectile Lab/Rebuild")]
        public static void Rebuild()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Sample", "Exit Play Mode before rebuilding the sample scene.", "OK");
                return;
            }

            Build();
            Debug.Log($"Sample scene written to {ScenePath}");
        }

        public static void RebuildFromCommandLine()
        {
            Build();
            Debug.Log($"Sample scene written to {ScenePath}");
        }

        static void Build()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath))
                throw new System.InvalidOperationException($"Missing {SampleScenePath}");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);

            if (!AssetDatabase.CopyAsset(SampleScenePath, ScenePath))
                throw new System.InvalidOperationException($"Could not copy {SampleScenePath} to {ScenePath}");

            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SampleLab.Preset[] presets = SamplePresetAssets.CreateOrUpdate();

            GameObject existingLab = GameObject.Find("SampleLab");
            if (existingLab != null)
                Object.DestroyImmediate(existingLab);
            GameObject legacyLab = GameObject.Find("SandboxLab");
            if (legacyLab != null)
                Object.DestroyImmediate(legacyLab);

            GameObject labGo = new GameObject("SampleLab");
            SampleLab lab = labGo.AddComponent<SampleLab>();
            SerializedObject so = new SerializedObject(lab);
            SerializedProperty presetProp = so.FindProperty("presets");
            presetProp.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                SerializedProperty element = presetProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = presets[i].label;
                element.FindPropertyRelative("asset").objectReferenceValue = presets[i].asset;
                element.FindPropertyRelative("useBallisticAim").boolValue = presets[i].useBallisticAim;
            }

            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Sandbox/Prefabs/ProjectileCube.prefab");
            if (cubePrefab != null)
                so.FindProperty("cubeViewPrefab").objectReferenceValue = cubePrefab;

            so.ApplyModifiedPropertiesWithoutUndo();

            SampleYard.BuildIfNeeded(labGo.transform);
            lab.BindChildren();
            SampleYard.AimCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException($"Could not save {ScenePath}");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
        }
    }
}
