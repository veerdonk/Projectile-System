using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class MulticastPatternTests
    {
        const string CatalogFolder = "Assets/MulticastPatternCatalogTest";
        const string CatalogAsset = CatalogFolder + "/Phase45Wave.asset";
        const string OtherCatalogAsset = CatalogFolder + "/Phase45Line.asset";

        RecipeAsset parent;
        RecipeAsset copy;
        ModifierKit kit;

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
                UnityEngine.Object.DestroyImmediate(parent);
            if (copy != null)
                UnityEngine.Object.DestroyImmediate(copy);
            if (kit != null)
                UnityEngine.Object.DestroyImmediate(kit);
            DeleteCatalog();
        }

        [Test]
        public void Multicast3_ThreeCellGrid_AimsEachShotAtItsCell_TheOriginalIncluded()
        {
            var pattern = new GridMulticastPattern(ThreeCells(), 20f);
            List<(Vector3 velocity, bool original)> shots = Fire(3, pattern);

            Assert.AreEqual(3, shots.Count);
            Vector3 left = Expected(new Vector2(-2f, 0f));
            Vector3 up = Expected(new Vector2(0f, 2f));
            Vector3 right = Expected(new Vector2(2f, 0f));
            Vector3 original = Single(shots, true);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, original), 0.05f);
            Assert.Greater(Vector3.Dot(original.normalized, left), 0.3f);

            int upCount = 0;
            int rightCount = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, shots[i].velocity), 0.05f);
                if (shots[i].original)
                    continue;
                if (Vector3.Dot(shots[i].velocity.normalized, up) > 0.3f)
                    upCount++;
                if (Vector3.Dot(shots[i].velocity.normalized, right) > 0.3f)
                    rightCount++;
            }

            Assert.AreEqual(1, upCount);
            Assert.AreEqual(1, rightCount);
        }

        [Test]
        public void Multicast5_ThreeCellGrid_ShotsPastTheCellsFlyStraightAhead()
        {
            var pattern = new GridMulticastPattern(ThreeCells(), 20f);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, i, 5)), 0.05f);
            Assert.AreEqual(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, 3, 5));
            Assert.AreEqual(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, 4, 5));

            List<(Vector3 velocity, bool original)> shots = Fire(5, pattern);
            Assert.AreEqual(5, shots.Count);
            int straight = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                if (Vector3.Angle(Vector3.forward, shots[i].velocity) < 0.05f)
                    straight++;
            }

            Assert.AreEqual(2, straight);
        }

        [Test]
        public void GridSpread_AimsTheFarthestCellInUse_AndZeroKeepsEveryShotParallel()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 3, 0, 0, 1, 2, 0, 0, 0 });
            var pattern = new GridMulticastPattern(grid, 20f);

            Assert.AreEqual(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, 0, 3));
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, 1, 3)), 0.05f);

            pattern.Spread = 0f;
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Vector3.forward, pattern.GetDirection(Context(), Vector3.forward, i, 3));
            List<(Vector3 velocity, bool original)> shots = Fire(3, pattern);
            for (int i = 0; i < shots.Count; i++)
                Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, shots[i].velocity), 0.05f);
        }

        [Test]
        public void GridMulticastPattern_CopiesTheGrid_AndComparesLayoutOnly()
        {
            GridMultiplyPattern grid = ThreeCells();
            var pattern = new GridMulticastPattern(grid, 20f);
            grid.AssignLayout(2, 0f, new[] { 1, 0, 0, 0 });
            Assert.IsTrue(ThreeCells().HasSameLayout(pattern.Grid));

            var sameCellsOtherSpread = new GridMulticastPattern(ThreeCells(), 45f);
            Assert.IsTrue(pattern.HasSameLayout(sameCellsOtherSpread));
            Assert.IsFalse(pattern.HasSameLayout(new GridMulticastPattern(OneCell(), 20f)));
            Assert.IsFalse(pattern.HasSameLayout(null));

            GridMulticastPattern clone = pattern.Clone();
            Assert.AreNotSame(pattern.Grid, clone.Grid);
            Assert.AreEqual(20f, clone.Spread);
            Assert.IsTrue(pattern.HasSameLayout(clone));
            Assert.Throws<ArgumentNullException>(() => new GridMulticastPattern(null));
        }

        [Test]
        public void NoPattern_KeepsTheRandom15DegreeCone()
        {
            AssertDefaultCone(new Multicast().Pattern);
            AssertDefaultCone(new Multicast(3, new Recipe { Id = "e" }).Pattern);
            var multicast = new Multicast(3, new Recipe { Id = "e" }, new GridMulticastPattern(ThreeCells()));
            multicast.Pattern = null;
            AssertDefaultCone(multicast.Pattern);

            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            parent.OnSpawn = new IModifier[] { new Multicast(3, new Recipe { Id = "e" }, new RandomConePattern(40f)) };
            var serialized = new SerializedObject(parent);
            serialized.FindProperty("onSpawn").GetArrayElementAtIndex(0).FindPropertyRelative("pattern")
                .managedReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertDefaultCone(((Multicast)parent.OnSpawn[0]).Pattern);
            AssertDefaultCone(((Multicast)parent.ToRecipe().OnSpawn[0]).Pattern);
        }

        [Test]
        public void AssetSavedWithoutAPatternField_KeepsTheRandom15DegreeCone()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            parent.OnSpawn = new IModifier[] { new Multicast(3, new Recipe { Id = "e" }, new RandomConePattern(40f)) };
            string json = EditorJsonUtility.ToJson(parent);
            string withoutPattern = Regex.Replace(json, ",\\s*\"pattern\"\\s*:\\s*\\{\\s*\"rid\"\\s*:\\s*-?\\d+\\s*\\}", string.Empty);
            Assert.AreNotEqual(json, withoutPattern, json);

            copy = ScriptableObject.CreateInstance<RecipeAsset>();
            EditorJsonUtility.FromJsonOverwrite(withoutPattern, copy);

            var multicast = copy.OnSpawn[0] as Multicast;
            Assert.IsNotNull(multicast);
            Assert.AreEqual(3, multicast.Count);
            AssertDefaultCone(multicast.Pattern);
        }

        [Test]
        public void DeserializedAsset_KeepsAnAuthoredGridAndConeAngle()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            parent.OnSpawn = new IModifier[]
            {
                new Multicast(3, new Recipe { Id = "grid" }, new GridMulticastPattern(ThreeCells(), 25f)),
                new Multicast(4, new Recipe { Id = "cone" }, new RandomConePattern(40f))
            };

            string json = EditorJsonUtility.ToJson(parent);
            copy = ScriptableObject.CreateInstance<RecipeAsset>();
            EditorJsonUtility.FromJsonOverwrite(json, copy);

            var grid = ((Multicast)copy.OnSpawn[0]).Pattern as GridMulticastPattern;
            Assert.IsNotNull(grid);
            Assert.AreEqual(25f, grid.Spread);
            Assert.IsTrue(ThreeCells().HasSameLayout(grid.Grid));
            var cone = ((Multicast)copy.OnSpawn[1]).Pattern as RandomConePattern;
            Assert.IsNotNull(cone);
            Assert.AreEqual(40f, cone.HalfAngleDegrees);
        }

        [Test]
        public void ToRecipe_AndKitClone_CopyTheAuthoredPattern()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            var authoredGrid = new GridMulticastPattern(ThreeCells(), 25f);
            var authoredCone = new RandomConePattern(40f);
            parent.OnSpawn = new IModifier[]
            {
                new Multicast(3, new Recipe { Id = "grid" }, authoredGrid),
                new Multicast(3, new Recipe { Id = "cone" }, authoredCone)
            };

            Recipe recipe = parent.ToRecipe();
            var grid = ((Multicast)recipe.OnSpawn[0]).Pattern as GridMulticastPattern;
            Assert.IsNotNull(grid);
            Assert.AreNotSame(authoredGrid, grid);
            Assert.AreNotSame(authoredGrid.Grid, grid.Grid);
            Assert.AreEqual(25f, grid.Spread);
            Assert.IsTrue(authoredGrid.HasSameLayout(grid));
            var cone = ((Multicast)recipe.OnSpawn[1]).Pattern as RandomConePattern;
            Assert.IsNotNull(cone);
            Assert.AreNotSame(authoredCone, cone);
            Assert.AreEqual(40f, cone.HalfAngleDegrees);

            kit = ScriptableObject.CreateInstance<ModifierKit>();
            kit.Layers = new[]
            {
                new ModifierKitLayer(
                    RecipeStage.OnSpawn,
                    new Multicast(3, new Recipe { Id = "kit-extra" }, new GridMulticastPattern(ThreeCells(), 30f)))
            };
            var target = new Recipe
            {
                Id = "base",
                OnSpawn = Array.Empty<IModifier>()
            };
            kit.ApplyTo(target);
            var fromKit = ((Multicast)target.OnSpawn[0]).Pattern as GridMulticastPattern;
            Assert.IsNotNull(fromKit);
            Assert.AreEqual(30f, fromKit.Spread);
            Assert.IsTrue(new GridMulticastPattern(ThreeCells()).HasSameLayout(fromKit));
        }

        [Test]
        public void AuthoredConeHalfAngle_IsUsed()
        {
            List<(Vector3 velocity, bool original)> wide = Fire(8, new RandomConePattern(40f));
            Assert.AreEqual(8, wide.Count);
            float widest = 0f;
            for (int i = 0; i < wide.Count; i++)
            {
                float angle = Vector3.Angle(Vector3.forward, wide[i].velocity);
                Assert.LessOrEqual(angle, 40.001f);
                widest = Mathf.Max(widest, angle);
            }

            Assert.Greater(widest, RandomConePattern.DefaultHalfAngleDegrees);

            List<(Vector3 velocity, bool original)> narrow = Fire(4, new RandomConePattern(0f));
            for (int i = 0; i < narrow.Count; i++)
                Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, narrow[i].velocity), 0.05f);
        }

        [Test]
        public void PatternDropdown_OffersConeThenDesignerAssets_AndCopiesTheGrid()
        {
            DeleteCatalog();
            AssetDatabase.CreateFolder("Assets", "MulticastPatternCatalogTest");
            var created = ScriptableObject.CreateInstance<MultiplyPatternAsset>();
            created.AssignLayout(3, 2f, new[] { 0, 2, 0, 1, 0, 3, 0, 0, 0 });
            AssetDatabase.CreateAsset(created, CatalogAsset);
            var other = ScriptableObject.CreateInstance<MultiplyPatternAsset>();
            other.AssignLayout(3, 2f, new[] { 0, 0, 0, 1, 0, 0, 0, 0, 0 });
            AssetDatabase.CreateAsset(other, OtherCatalogAsset);
            AssetDatabase.SaveAssets();
            MultiplyPatternAsset saved = AssetDatabase.LoadAssetAtPath<MultiplyPatternAsset>(CatalogAsset);
            MultiplyPatternAsset savedOther = AssetDatabase.LoadAssetAtPath<MultiplyPatternAsset>(OtherCatalogAsset);

            CollectionAssert.Contains(RecipeAssetEditor.MulticastPatterns(), typeof(RandomConePattern));
            CollectionAssert.DoesNotContain(RecipeAssetEditor.MulticastPatterns(), typeof(GridMulticastPattern));

            RecipeAssetEditor.PatternOption[] options = RecipeAssetEditor.MulticastPatternOptions();
            int coneIndex = -1;
            int assetIndex = -1;
            int otherIndex = -1;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Type == typeof(RandomConePattern))
                    coneIndex = i;
                if (options[i].Asset == saved)
                    assetIndex = i;
                if (options[i].Asset == savedOther)
                    otherIndex = i;
            }

            Assert.GreaterOrEqual(coneIndex, 0);
            Assert.GreaterOrEqual(assetIndex, RecipeAssetEditor.MulticastPatterns().Length);
            Assert.GreaterOrEqual(otherIndex, RecipeAssetEditor.MulticastPatterns().Length);
            Assert.AreEqual(coneIndex, RecipeAssetEditor.MulticastPatternOptionIndex(null));
            Assert.AreEqual(coneIndex, RecipeAssetEditor.MulticastPatternOptionIndex(new RandomConePattern(30f)));
            Assert.AreEqual(-1, RecipeAssetEditor.MulticastPatternOptionIndex(new GridMulticastPattern(OneCellCorner())));

            kit = ScriptableObject.CreateInstance<ModifierKit>();
            kit.Layers = new[]
            {
                new ModifierKitLayer(RecipeStage.OnSpawn, new Multicast(3, new Recipe { Id = "e" }))
            };
            var serialized = new SerializedObject(kit);
            SerializedProperty pattern = serialized.FindProperty("layers").GetArrayElementAtIndex(0)
                .FindPropertyRelative("modifiers").GetArrayElementAtIndex(0).FindPropertyRelative("pattern");
            RecipeAssetEditor.AssignMulticastPatternOption(pattern, assetIndex);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var copied = ((Multicast)kit.Layers[0].Modifiers[0]).Pattern as GridMulticastPattern;
            Assert.IsNotNull(copied);
            Assert.AreEqual("Phase45Wave", copied.SourceName);
            Assert.AreEqual(GridMulticastPattern.DefaultSpreadDegrees, copied.Spread);
            Assert.AreEqual(1, copied.Grid.Cell(1, 0));
            Assert.AreEqual(assetIndex, RecipeAssetEditor.MulticastPatternOptionIndex(copied));
            saved.Spacing = 9f;
            Assert.AreEqual(2f, copied.Grid.Spacing);

            serialized.Update();
            pattern.FindPropertyRelative("spread").floatValue = 33f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RecipeAssetEditor.AssignMulticastPatternOption(pattern, otherIndex);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var swapped = ((Multicast)kit.Layers[0].Modifiers[0]).Pattern as GridMulticastPattern;
            Assert.IsNotNull(swapped);
            Assert.AreEqual("Phase45Line", swapped.SourceName);
            Assert.AreEqual(33f, swapped.Spread);

            RecipeAssetEditor.AssignMulticastPatternOption(pattern, coneIndex);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssertDefaultCone(((Multicast)kit.Layers[0].Modifiers[0]).Pattern);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RecipeAssetEditor.AssignMulticastPatternOption(pattern, options.Length));
            Assert.Throws<ArgumentNullException>(() => RecipeAssetEditor.AssignMulticastPatternOption(null, coneIndex));
        }

        static void AssertDefaultCone(IMulticastPattern pattern)
        {
            var cone = pattern as RandomConePattern;
            Assert.IsNotNull(cone);
            Assert.AreEqual(RandomConePattern.DefaultHalfAngleDegrees, cone.HalfAngleDegrees);
        }

        static List<(Vector3 velocity, bool original)> Fire(int count, IMulticastPattern pattern)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("pattern-extra", speed: 12f, lifetime: 2f);
            Recipe shot = SimulationTestHelper.CreateRecipe("pattern-parent", speed: 20f, lifetime: 5f);
            shot.OnSpawn = new IModifier[] { new Multicast(count, extra, pattern) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: 9u);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                var shots = new List<(Vector3 velocity, bool original)>(active.Count);
                for (int i = 0; i < active.Count; i++)
                    shots.Add((active[i].velocity, active[i].recipe.Id == shot.Id));
                return shots;
            }
        }

        static Vector3 Single(List<(Vector3 velocity, bool original)> shots, bool original)
        {
            int found = -1;
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].original != original)
                    continue;
                Assert.AreEqual(-1, found, "More than one match");
                found = i;
            }

            Assert.GreaterOrEqual(found, 0, "Missing shot");
            return shots[found].velocity;
        }

        static GridMultiplyPattern ThreeCells()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 2, 0, 1, 0, 3, 0, 0, 0 });
            return grid;
        }

        static GridMultiplyPattern OneCell()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0 });
            return grid;
        }

        static GridMultiplyPattern OneCellCorner()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(4, 3f, new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            return grid;
        }

        static Vector3 Expected(Vector2 local)
        {
            return MultiplyPatternPlane.FromLocal(Vector3.forward, local).normalized;
        }

        static ModifierContext Context()
        {
            return new ModifierContext(new List<SpawnRequest>(), new HomingCatalog(), 1u);
        }

        static void DeleteCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CatalogAsset) != null)
                AssetDatabase.DeleteAsset(CatalogAsset);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OtherCatalogAsset) != null)
                AssetDatabase.DeleteAsset(OtherCatalogAsset);
            if (AssetDatabase.IsValidFolder(CatalogFolder))
                AssetDatabase.DeleteAsset(CatalogFolder);
        }
    }
}
