using System;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class RecipeAssetTests
    {
        RecipeAsset parent;
        RecipeAsset child;

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
                UnityEngine.Object.DestroyImmediate(parent);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child);
        }

        [Test]
        public void Pierce_IsInOnHitCatalog_NotOnSpawn()
        {
            Assert.IsTrue(Contains(RecipeAssetEditor.ModifiersFor(RecipeStage.OnHit), typeof(Pierce)));
            Assert.IsFalse(Contains(RecipeAssetEditor.ModifiersFor(RecipeStage.OnSpawn), typeof(Pierce)));
        }

        [Test]
        public void PatternCatalog_IncludesShippedMultiplyPatterns()
        {
            Type[] patterns = RecipeAssetEditor.Patterns();
            Assert.IsTrue(Contains(patterns, typeof(SquarePattern)));
            Assert.IsTrue(Contains(patterns, typeof(CirclePattern)));
            Assert.IsTrue(Contains(patterns, typeof(RandomCirclePattern)));
        }

        [Test]
        public void StatusCatalog_IncludesDummyStatusEffect()
        {
            Assert.IsTrue(Contains(RecipeAssetEditor.StatusEffects(), typeof(DummyStatusEffect)));
        }

        [Test]
        public void ToRecipe_CopiesStatsAndPierceOnHit()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "pierce-asset";
            parent.Speed = 20f;
            parent.LifetimeSeconds = 3f;
            parent.OnHit = new IModifier[] { new Pierce(2) };

            Recipe recipe = parent.ToRecipe();

            Assert.AreEqual("pierce-asset", recipe.Id);
            Assert.AreEqual(20f, recipe.Speed);
            Assert.AreEqual(3f, recipe.LifetimeSeconds);
            Assert.AreEqual(1, recipe.OnHit.Length);
            var pierce = recipe.OnHit[0] as Pierce;
            Assert.IsNotNull(pierce);
            Assert.AreEqual(2, pierce.Count);
            Assert.AreNotSame(parent.OnHit[0], recipe.OnHit[0]);
        }

        [Test]
        public void ToRecipe_ChangingPierceCountOnCopy_LeavesAssetUnchanged()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.OnHit = new IModifier[] { new Pierce(2) };

            Recipe copy = parent.ToRecipe();
            var copyPierce = copy.OnHit[0] as Pierce;
            Assert.IsNotNull(copyPierce);
            copyPierce.Count = 1;

            Assert.AreEqual(1, copyPierce.Count);
            Assert.AreEqual(2, ((Pierce)parent.OnHit[0]).Count);
        }

        [Test]
        public void ToRecipe_ResolvesMultiplyChildAsset()
        {
            child = ScriptableObject.CreateInstance<RecipeAsset>();
            child.Id = "child";
            child.Speed = 8f;
            child.LifetimeSeconds = 2f;

            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            parent.OnHit = new IModifier[] { new Multiply(2, child) };

            Recipe recipe = parent.ToRecipe();

            Assert.AreEqual(1, recipe.OnHit.Length);
            var multiply = recipe.OnHit[0] as Multiply;
            Assert.IsNotNull(multiply);
            Assert.AreEqual(2, multiply.Count);
            Assert.IsNotNull(multiply.ChildRecipe);
            Assert.AreEqual("child", multiply.ChildRecipe.Id);
            Assert.AreEqual(8f, multiply.ChildRecipe.Speed);
            Assert.AreEqual(2f, multiply.ChildRecipe.LifetimeSeconds);
        }

        [Test]
        public void ToRecipe_KeepsAuthoredMultiplyPatternType()
        {
            child = ScriptableObject.CreateInstance<RecipeAsset>();
            child.Id = "child";
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "parent";
            parent.OnHit = new IModifier[] { new Multiply(2, child, 1f, new CirclePattern()) };

            var multiply = parent.ToRecipe().OnHit[0] as Multiply;
            Assert.IsNotNull(multiply);
            Assert.IsInstanceOf<CirclePattern>(multiply.Pattern);
        }

        [Test]
        public void ToRecipe_KeepsAuthoredStatusEffectType()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "status";
            parent.OnHit = new IModifier[] { new ApplyStatus(new DummyStatusEffect()) };

            var apply = parent.ToRecipe().OnHit[0] as ApplyStatus;
            Assert.IsNotNull(apply);
            Assert.IsInstanceOf<DummyStatusEffect>(apply.Effect);
            Assert.AreNotSame(parent.OnHit[0], apply);
        }

        [Test]
        public void ToRecipe_SelfChild_DoesNotThrow_AndSharesRecipe()
        {
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "loop";
            parent.OnHit = new IModifier[] { new Multiply(1, parent) };

            Recipe recipe = parent.ToRecipe();

            var multiply = recipe.OnHit[0] as Multiply;
            Assert.IsNotNull(multiply);
            Assert.AreSame(recipe, multiply.ChildRecipe);
        }

        [Test]
        public void ToRecipe_TwoAssetCycle_SharesRecipes()
        {
            child = ScriptableObject.CreateInstance<RecipeAsset>();
            child.Id = "b";
            parent = ScriptableObject.CreateInstance<RecipeAsset>();
            parent.Id = "a";
            parent.OnHit = new IModifier[] { new Multiply(1, child) };
            child.OnHit = new IModifier[] { new Multiply(1, parent) };

            Recipe a = parent.ToRecipe();
            var multiplyA = a.OnHit[0] as Multiply;
            Assert.IsNotNull(multiplyA);
            Recipe b = multiplyA.ChildRecipe;
            Assert.IsNotNull(b);
            Assert.AreEqual("b", b.Id);
            var multiplyB = b.OnHit[0] as Multiply;
            Assert.IsNotNull(multiplyB);
            Assert.AreSame(a, multiplyB.ChildRecipe);
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
