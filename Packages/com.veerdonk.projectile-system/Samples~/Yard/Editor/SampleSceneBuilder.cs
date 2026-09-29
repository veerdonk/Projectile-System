using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Veerdonk.ProjectileSystem.Samples.Editor
{
    /// <summary>
    /// Builds <c>Yard.unity</c> under the Package Manager import folder and copies that scene back to <c>Samples~/Yard</c>.
    /// </summary>
    public static class SampleSceneBuilder
    {
        public const string PackageName = "com.veerdonk.projectile-system";
        public const string DisplayName = "Projectile Yard";

        public static string PackageVersion => ReadPackageVersion();

        public static string ImportRoot => ResolveImportRoot();

        public static string ScenePath => $"{ImportRoot}/Scenes/Yard.unity";

        public static string RecipesFolder => $"{ImportRoot}/Recipes";

        public static string CubePrefabPath => $"{ImportRoot}/Prefabs/ProjectileCube.prefab";

        [MenuItem("Projectile Lab/Rebuild")]
        public static void Rebuild()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Projectile Yard",
                    "Exit Play Mode before rebuilding the sample scene.",
                    "OK");
                return;
            }

            Build();
            Debug.Log($"Projectile Yard scene written to {ScenePath}");
        }

        public static void RebuildFromCommandLine()
        {
            Build();
            Debug.Log($"Projectile Yard scene written to {ScenePath}");
        }

        static void Build()
        {
            EnsureAssetFolder($"{ImportRoot}/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save {ScenePath}");

            AssetDatabase.Refresh();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SampleLab.Preset[] presets = SamplePresetAssets.CreateOrUpdate();

            GameObject existingLab = GameObject.Find("SampleLab");
            if (existingLab != null)
                UnityEngine.Object.DestroyImmediate(existingLab);
            GameObject legacyLab = GameObject.Find("SandboxLab");
            if (legacyLab != null)
                UnityEngine.Object.DestroyImmediate(legacyLab);

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

            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            if (cubePrefab != null)
                so.FindProperty("cubeViewPrefab").objectReferenceValue = cubePrefab;

            SerializedProperty grids = so.FindProperty("multiplyPatterns");
            if (SamplePresetAssets.GridPattern != null && grids != null)
            {
                grids.arraySize = 1;
                grids.GetArrayElementAtIndex(0).objectReferenceValue = SamplePresetAssets.GridPattern;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            SampleYard.BuildIfNeeded(labGo.transform);
            lab.BindChildren();
            SampleYard.AimCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save {ScenePath}");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            CopySceneToPackageSource();
        }

        public static void EnsureAssetFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
            if (!string.IsNullOrEmpty(parent) && parent != "Assets" && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        static string ResolveImportRoot()
        {
            foreach (UnityEditor.PackageManager.UI.Sample sample in
                     UnityEditor.PackageManager.UI.Sample.FindByPackage(PackageName, PackageVersion))
            {
                if (sample.displayName != DisplayName || string.IsNullOrEmpty(sample.importPath))
                    continue;
                return ToAssetPath(sample.importPath);
            }

            return $"Assets/Samples/{PackageName}/{PackageVersion}/{DisplayName}";
        }

        static string ToAssetPath(string path)
        {
            path = path.Replace('\\', '/');
            const string marker = "Assets/";
            int index = path.IndexOf(marker, StringComparison.Ordinal);
            return index > 0 ? path.Substring(index) : path;
        }

        static void CopySceneToPackageSource()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string importScene = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            string destDir = Path.Combine(projectRoot, "Packages", PackageName, "Samples~", "Yard", "Scenes");
            Directory.CreateDirectory(destDir);
            File.Copy(importScene, Path.Combine(destDir, "Yard.unity"), true);
            string meta = importScene + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, Path.Combine(destDir, "Yard.unity.meta"), true);
        }

        static string ReadPackageVersion()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string jsonPath = Path.Combine(projectRoot, "Packages", PackageName, "package.json");
            string json = File.ReadAllText(jsonPath);
            const string key = "\"version\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return "0.1.0";

            int open = json.IndexOf('"', keyIndex + key.Length);
            int close = json.IndexOf('"', open + 1);
            if (open < 0 || close < 0)
                return "0.1.0";

            return json.Substring(open + 1, close - open - 1);
        }
    }
}
