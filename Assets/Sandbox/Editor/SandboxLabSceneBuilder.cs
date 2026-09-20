using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.Editor
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Sandbox.unity</c> from the URP sample lighting plus the phase 0 lane.
    /// </summary>
    public static class SandboxLabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Projectile Lab/Rebuild Sandbox Scene")]
        public static void RebuildSandboxScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Sandbox", "Exit Play Mode before rebuilding the sandbox scene.", "OK");
                return;
            }

            Build();
            Debug.Log($"Sandbox scene written to {ScenePath}");
        }

        public static void RebuildFromCommandLine()
        {
            Build();
            Debug.Log($"Sandbox scene written to {ScenePath}");
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

            GameObject existing = GameObject.Find("SandboxLab");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject labGo = new GameObject("SandboxLab");
            SandboxLab lab = labGo.AddComponent<SandboxLab>();
            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Sandbox/Prefabs/ProjectileCube.prefab");
            if (cubePrefab != null)
            {
                SerializedObject so = new SerializedObject(lab);
                so.FindProperty("cubeViewPrefab").objectReferenceValue = cubePrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            SandboxLab.BuildLaneIfNeeded(labGo.transform);
            lab.BindChildren();
            SandboxLab.AimCameraAtLane();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException($"Could not save {ScenePath}");

            var buildScenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
        }
    }
}
