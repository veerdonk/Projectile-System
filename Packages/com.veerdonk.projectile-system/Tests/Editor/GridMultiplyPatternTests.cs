using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class GridMultiplyPatternTests
    {
        const string CatalogFolder = "Assets/GridPatternCatalogTest";
        const string CatalogAsset = CatalogFolder + "/Phase33GridFan.asset";

        RecipeAsset parent;
        RecipeAsset child;
        ModifierKit kit;
        MultiplyPatternAsset patternAsset;

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
                UnityEngine.Object.DestroyImmediate(parent);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child);
            if (kit != null)
                UnityEngine.Object.DestroyImmediate(kit);
            if (patternAsset != null)
                UnityEngine.Object.DestroyImmediate(patternAsset);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CatalogAsset) != null)
                AssetDatabase.DeleteAsset(CatalogAsset);
            if (AssetDatabase.IsValidFolder(CatalogFolder))
                AssetDatabase.DeleteAsset(CatalogFolder);
        }

        [Test]
        public void DefaultGrid_Is3x3_Spacing2_AndSpawnsNothing()
        {
            var grid = new GridMultiplyPattern();
            Assert.AreEqual(3, grid.Size);
            Assert.AreEqual(2f, grid.Spacing);
            Assert.AreEqual(0, grid.NumberedCount);
            Assert.AreEqual(0, grid.ResolveCount(4));
            Assert.AreEqual(0, EmptyMultiplyChildCount());
        }

        [Test]
        public void Multiply2_UsesCells1And2_Multiply5_UsesNumberedCount()
        {
            GridMultiplyPattern grid = Example();
            Assert.AreEqual(2, grid.ResolveCount(2));
            Assert.AreEqual(3, grid.ResolveCount(5));
            AssertLocal(grid, 0, 2, Vector2.zero);
            AssertLocal(grid, 1, 2, new Vector2(2f, 0f));
            AssertLocal(grid, 0, 3, Vector2.zero);
            AssertLocal(grid, 1, 3, new Vector2(2f, 0f));
            AssertLocal(grid, 2, 3, new Vector2(0f, 2f));

            List<(Vector3 position, Vector3 velocity)> two = FireInterval(2, 20f, grid);
            Assert.AreEqual(2, two.Count);
            Assert.AreEqual(2f, Vector3.Distance(two[0].position, two[1].position), 1e-3f);

            List<(Vector3 position, Vector3 velocity)> five = FireInterval(5, 20f, Example());
            Assert.AreEqual(3, five.Count);
        }

        [Test]
        public void Spacing2_PlacesAdjacentPips_Spread20_AimsFarthest_Spread0_IsParallel_Spacing0_Stacks()
        {
            GridMultiplyPattern grid = Example();
            ModifierContext context = Context();
            Vector3 center = grid.GetDirection(context, Vector3.forward, 20f, 0, 3);
            Vector3 right = grid.GetDirection(context, Vector3.forward, 20f, 1, 3);
            Vector3 above = grid.GetDirection(context, Vector3.forward, 20f, 2, 3);
            Vector3 planeRight = MultiplyPatternPlane.FromLocal(Vector3.forward, Vector2.right).normalized;
            Vector3 planeUp = MultiplyPatternPlane.FromLocal(Vector3.forward, Vector2.up).normalized;
            Assert.AreEqual(Vector3.forward, center);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, right), 0.05f);
            Assert.Greater(Vector3.Dot(right.normalized, planeRight), 0.3f);
            Assert.AreEqual(0f, Vector3.Dot(right.normalized, planeUp), 1e-3f);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, above), 0.05f);
            Assert.Greater(Vector3.Dot(above.normalized, planeUp), 0.3f);
            Assert.AreEqual(0f, Vector3.Dot(above.normalized, planeRight), 1e-3f);
            Assert.AreEqual(Vector3.forward, grid.GetDirection(context, Vector3.forward, 0f, 1, 3));

            var stacked = new GridMultiplyPattern();
            stacked.AssignLayout(3, 0f, new[] { 0, 3, 0, 0, 1, 2, 0, 0, 0 });
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Vector3.zero, stacked.GetOffset(context, Vector3.forward, 20f, i, 3));
                Assert.AreEqual(Vector3.forward, stacked.GetDirection(context, Vector3.forward, 20f, i, 3));
            }

            List<(Vector3 position, Vector3 velocity)> aimed = FireInterval(3, 20f, Example());
            (Vector3 position, Vector3 velocity) rightChild = Find(aimed, item => item.position.x < -1f);
            (Vector3 position, Vector3 velocity) aboveChild = Find(aimed, item => item.position.y > 1f);
            (Vector3 position, Vector3 velocity) centerChild = Find(
                aimed,
                item => Mathf.Abs(item.position.x) < 1f && Mathf.Abs(item.position.y) < 1f);
            Assert.AreEqual(2f, Vector3.Distance(centerChild.position, rightChild.position), 1e-3f);
            Assert.AreEqual(2f, Vector3.Distance(centerChild.position, aboveChild.position), 1e-3f);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, rightChild.velocity), 0.05f);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, aboveChild.velocity), 0.05f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, centerChild.velocity), 0.05f);

            List<(Vector3 position, Vector3 velocity)> parallel = FireInterval(3, 0f, Example());
            for (int i = 0; i < parallel.Count; i++)
            {
                Assert.AreEqual(0f, parallel[i].velocity.x, 1e-3f);
                Assert.AreEqual(0f, parallel[i].velocity.y, 1e-3f);
                Assert.Greater(parallel[i].velocity.z, 0f);
            }

            List<(Vector3 position, Vector3 velocity)> stack = FireInterval(3, 20f, stacked);
            Assert.AreEqual(3, stack.Count);
            Assert.AreEqual(0f, Vector3.Distance(stack[0].position, stack[1].position), 1e-3f);
            Assert.AreEqual(0f, Vector3.Distance(stack[0].position, stack[2].position), 1e-3f);
        }

        [Test]
        public void RightClick_Compacts_GrowKeepsNumbers_ShrinkDropsOffGrid()
        {
            var grid = new GridMultiplyPattern();
            grid.LeftClick(1, 1);
            grid.LeftClick(1, 2);
            grid.LeftClick(0, 1);
            Assert.AreEqual(1, grid.Cell(1, 1));
            Assert.AreEqual(2, grid.Cell(1, 2));
            Assert.AreEqual(3, grid.Cell(0, 1));

            grid.LeftClick(1, 1);
            Assert.AreEqual(1, grid.Cell(1, 1));
            Assert.AreEqual(3, grid.NumberedCount);

            grid.RightClick(1, 2);
            Assert.AreEqual(0, grid.Cell(1, 2));
            Assert.AreEqual(1, grid.Cell(1, 1));
            Assert.AreEqual(2, grid.Cell(0, 1));
            Assert.AreEqual(2, grid.NumberedCount);

            grid.LeftClick(1, 2);
            Assert.AreEqual(3, grid.Cell(1, 2));
            grid.Grow();
            Assert.AreEqual(4, grid.Size);
            Assert.AreEqual(1, grid.Cell(1, 1));
            Assert.AreEqual(2, grid.Cell(0, 1));
            Assert.AreEqual(3, grid.Cell(1, 2));
            Assert.AreEqual(0, grid.Cell(0, 3));
            Assert.AreEqual(0, grid.Cell(3, 0));
            Assert.AreEqual(0, grid.Cell(3, 3));

            var shrink = new GridMultiplyPattern();
            shrink.AssignLayout(3, 2f, new[] { 0, 3, 0, 0, 1, 2, 0, 0, 0 });
            shrink.Shrink();
            Assert.AreEqual(2, shrink.Size);
            Assert.AreEqual(1, shrink.Cell(1, 1));
            Assert.AreEqual(2, shrink.Cell(0, 1));
            Assert.AreEqual(0, shrink.Cell(0, 0));
            Assert.AreEqual(0, shrink.Cell(1, 0));
            shrink.Shrink();
            Assert.AreEqual(2, shrink.Size);
        }

        [Test]
        public void EvenGrid_HasNoCenterCell()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(2, 2f, new[] { 1, 0, 0, 0 });
            AssertLocal(grid, 0, 1, new Vector2(-1f, 1f));
        }

        [Test]
        public void ProceduralPatterns_KeepForward_AndRequestedCount()
        {
            IMultiplyPattern[] patterns =
            {
                new SquarePattern(),
                new CirclePattern(),
                new RandomCirclePattern()
            };
            for (int i = 0; i < patterns.Length; i++)
            {
                Assert.AreEqual(Vector3.forward, patterns[i].GetDirection(Context(), Vector3.forward, 20f, 1, 4));
                Assert.AreEqual(4, patterns[i].ResolveCount(4));
            }

            Assert.AreNotEqual(Vector3.zero, new SquarePattern().GetOffset(Context(), Vector3.forward, 2f, 0, 2));
            Type[] catalog = RecipeAssetEditor.Patterns();
            Assert.IsFalse(Contains(catalog, typeof(GridMultiplyPattern)));
        }

        [Test]
        public void ToRecipe_AndKitClone_CopyGridFields_NotAnEmptyActivator()
        {
            child = ScriptableObject.CreateInstance<RecipeAsset>();
            child.Id = "child";
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            GridMultiplyPattern grid = Example();
            parent.OnHit = new IModifier[] { new Multiply(5, child, 20f, grid) };

            var multiply = parent.ToRecipe().OnHit[0] as Multiply;
            Assert.IsNotNull(multiply);
            var copied = multiply.Pattern as GridMultiplyPattern;
            Assert.IsNotNull(copied);
            Assert.AreNotSame(grid, copied);
            Assert.IsTrue(grid.HasSameLayout(copied));
            copied.AssignLayout(2, 0f, new[] { 1, 0, 0, 0 });
            Assert.AreEqual(2f, grid.Spacing);
            Assert.AreEqual(3, grid.NumberedCount);

            kit = ScriptableObject.CreateInstance<ModifierKit>();
            kit.Layers = new[]
            {
                new ModifierKitLayer(RecipeStage.OnHit, new Multiply(5, new Recipe { Id = "kit-child" }, 20f, Example()))
            };
            var recipe = new Recipe
            {
                Id = "base",
                OnHit = Array.Empty<IModifier>()
            };
            kit.ApplyTo(recipe);
            var fromKit = recipe.OnHit[0] as Multiply;
            Assert.IsNotNull(fromKit);
            Assert.IsTrue(Example().HasSameLayout(fromKit.Pattern as GridMultiplyPattern));
        }

        [Test]
        public void PatternDropdown_CopiesAssetFields_AndDoesNotAliasTheAsset()
        {
            if (AssetDatabase.IsValidFolder(CatalogFolder))
                AssetDatabase.DeleteAsset(CatalogFolder);
            AssetDatabase.CreateFolder("Assets", "GridPatternCatalogTest");

            var created = ScriptableObject.CreateInstance<MultiplyPatternAsset>();
            created.AssignLayout(3, 4f, new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0 });
            AssetDatabase.CreateAsset(created, CatalogAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MultiplyPatternAsset saved = AssetDatabase.LoadAssetAtPath<MultiplyPatternAsset>(CatalogAsset);

            try
            {
                RecipeAssetEditor.PatternOption[] options = RecipeAssetEditor.PatternOptions();
                int assetIndex = -1;
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i].Asset == saved)
                        assetIndex = i;
                }

                Assert.GreaterOrEqual(assetIndex, RecipeAssetEditor.Patterns().Length);

                kit = ScriptableObject.CreateInstance<ModifierKit>();
                kit.Layers = new[] { new ModifierKitLayer(RecipeStage.OnHit, new Multiply(3, new Recipe { Id = "c" })) };
                var serialized = new SerializedObject(kit);
                SerializedProperty pattern = serialized.FindProperty("layers").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("modifiers").GetArrayElementAtIndex(0).FindPropertyRelative("pattern");
                Assert.AreEqual(assetIndex, RecipeAssetEditor.PatternOptionIndex(saved.ToPattern()));
                RecipeAssetEditor.AssignPatternOption(pattern, assetIndex);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var copied = ((Multiply)kit.Layers[0].Modifiers[0]).Pattern as GridMultiplyPattern;
                Assert.IsNotNull(copied);
                Assert.AreEqual(4f, copied.Spacing);
                Assert.AreEqual(1, copied.Cell(1, 1));
                Assert.AreEqual("Phase33GridFan", copied.SourceName);
                saved.Spacing = 9f;
                Assert.AreEqual(4f, copied.Spacing);
                Assert.IsFalse(ReferenceEquals(saved, copied));

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    RecipeAssetEditor.AssignPatternOption(pattern, options.Length));
                Assert.Throws<ArgumentNullException>(() => RecipeAssetEditor.AssignPatternOption(null, assetIndex));
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CatalogAsset) != null)
                    AssetDatabase.DeleteAsset(CatalogAsset);
                if (AssetDatabase.IsValidFolder(CatalogFolder))
                    AssetDatabase.DeleteAsset(CatalogFolder);
            }
        }

        static GridMultiplyPattern Example()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 3, 0, 0, 1, 2, 0, 0, 0 });
            return grid;
        }

        static int EmptyMultiplyChildCount()
        {
            return FireInterval(4, 20f, new GridMultiplyPattern()).Count;
        }

        static List<(Vector3 position, Vector3 velocity)> FireInterval(int count, float spread, GridMultiplyPattern grid)
        {
            Recipe child = SimulationTestHelper.CreateRecipe("grid-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("grid-parent", speed: 10f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(count, child, spread, grid) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.25f);
                world.Tick(0.25f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                var children = new List<(Vector3 position, Vector3 velocity)>();
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].recipe.Id != child.Id)
                        continue;
                    children.Add((active[i].position, active[i].velocity));
                }

                return children;
            }
        }

        static (Vector3 position, Vector3 velocity) Find(
            List<(Vector3 position, Vector3 velocity)> children,
            System.Predicate<(Vector3 position, Vector3 velocity)> match)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (match(children[i]))
                    return children[i];
            }

            Assert.Fail("Missing child");
            return default;
        }

        static void AssertLocal(GridMultiplyPattern grid, int index, int count, Vector2 local)
        {
            Vector3 offset = grid.GetOffset(Context(), Vector3.forward, 99f, index, count);
            Vector3 expected = Expected(local);
            Assert.AreEqual(expected.x, offset.x, 1e-4f);
            Assert.AreEqual(expected.y, offset.y, 1e-4f);
            Assert.AreEqual(expected.z, offset.z, 1e-4f);
        }

        static Vector3 Expected(Vector2 local)
        {
            return MultiplyPatternPlane.FromLocal(Vector3.forward, local);
        }

        static ModifierContext Context()
        {
            return new ModifierContext(new List<SpawnRequest>(), new HomingCatalog(), 1u);
        }

        static bool Contains(Type[] types, Type match)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == match)
                    return true;
            }

            return false;
        }
    }
}
