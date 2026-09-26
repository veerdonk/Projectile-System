using System;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class ModifierKitTests
    {
        RecipeAsset straight;
        ModifierKit pierceKit;
        ModifierKit statusKit;
        ModifierKit combination;

        [TearDown]
        public void TearDown()
        {
            if (straight != null)
                UnityEngine.Object.DestroyImmediate(straight);
            if (pierceKit != null)
                UnityEngine.Object.DestroyImmediate(pierceKit);
            if (statusKit != null)
                UnityEngine.Object.DestroyImmediate(statusKit);
            if (combination != null)
                UnityEngine.Object.DestroyImmediate(combination);
        }

        [Test]
        public void CreateMenu_IsProjectileModifierKit()
        {
            var menu = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(
                typeof(ModifierKit),
                typeof(CreateAssetMenuAttribute));
            Assert.AreEqual("Projectile/Modifier Kit", menu.menuName);
        }

        [Test]
        public void StraightPlusPierceKit_PiercesTwoThenDiesOnTheThird()
        {
            straight = ScriptableObject.CreateInstance<RecipeAsset>();
            straight.Id = "straight";
            straight.Speed = 20f;
            straight.LifetimeSeconds = 5f;
            straight.OnHit = Array.Empty<IModifier>();
            pierceKit = Kit(RecipeStage.OnHit, new Pierce(2));

            Recipe recipe = Recipe.Compose(straight.ToRecipe(), "straight+pierce", pierceKit);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                var seen = new System.Collections.Generic.List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);
                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(2));
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 6f), Vector3.back, new TargetId(3));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(0, straight.OnHit.Length);
                Assert.AreEqual("straight", straight.ToRecipe().Id);
            }
        }

        [Test]
        public void TwoKits_AppendInOrder_SwapReversesOrder()
        {
            Recipe basis = Basis();
            pierceKit = Kit(RecipeStage.OnHit, new Pierce(2));
            statusKit = Kit(RecipeStage.OnHit, new ApplyStatus(new DummyStatusEffect()));

            Recipe pierceFirst = Recipe.Compose(basis, "pierce-then-status", pierceKit, statusKit);
            Assert.IsInstanceOf<Pierce>(pierceFirst.OnHit[0]);
            Assert.IsInstanceOf<ApplyStatus>(pierceFirst.OnHit[1]);

            Recipe statusFirst = Recipe.Compose(basis, "status-then-pierce", statusKit, pierceKit);
            Assert.IsInstanceOf<ApplyStatus>(statusFirst.OnHit[0]);
            Assert.IsInstanceOf<Pierce>(statusFirst.OnHit[1]);
            Assert.AreEqual(0, basis.OnHit.Length);
        }

        [Test]
        public void CombinationKit_AppendsPierceOnHit_AndHomingOnTick()
        {
            combination = ScriptableObject.CreateInstance<ModifierKit>();
            combination.Layers = new[]
            {
                new ModifierKitLayer(RecipeStage.OnHit, new Pierce(1)),
                new ModifierKitLayer(RecipeStage.OnTick, new Homing(100f))
            };

            Recipe composed = Recipe.Compose(Basis(), "pierce-homing", combination);
            Assert.IsInstanceOf<Pierce>(composed.OnHit[0]);
            Assert.AreEqual(1, ((Pierce)composed.OnHit[0]).Count);
            Assert.IsInstanceOf<Homing>(composed.OnTick[0]);
            Assert.AreEqual(100f, ((Homing)composed.OnTick[0]).TurnRateDegreesPerSecond, 1e-4f);
        }

        [Test]
        public void AppendPierceOnSpawn_Throws_AndLeavesListsUnchanged()
        {
            Recipe basis = Basis();
            Assert.Throws<ArgumentException>(() => basis.Append(RecipeStage.OnSpawn, new Pierce(1)));
            Assert.AreEqual(0, basis.OnSpawn.Length);
            Assert.AreEqual(0, basis.OnHit.Length);
        }

        [Test]
        public void CombinedStageFlags_Throw_AndLeaveBasisUnchanged()
        {
            Recipe basis = Basis();
            combination = ScriptableObject.CreateInstance<ModifierKit>();
            combination.Layers = new[]
            {
                new ModifierKitLayer(RecipeStage.OnSpawn | RecipeStage.OnHit, new Pierce(1))
            };

            Assert.Throws<ArgumentException>(() => Recipe.Compose(basis, "illegal", combination));
            Assert.AreEqual("base", basis.Id);
            Assert.AreEqual(0, basis.OnHit.Length);
            Assert.AreEqual(0, basis.OnSpawn.Length);
        }

        [Test]
        public void Compose_NewId_LeavesBasisListsUnchanged()
        {
            Recipe basis = Basis();
            basis.Speed = 20f;
            pierceKit = Kit(RecipeStage.OnHit, new Pierce(2));
            Recipe composed = Recipe.Compose(basis, "straight+pierce", pierceKit);

            Assert.AreEqual("straight+pierce", composed.Id);
            Assert.AreEqual("base", basis.Id);
            Assert.AreEqual(0, basis.OnHit.Length);
            Assert.AreEqual(1, composed.OnHit.Length);
            Assert.AreEqual(20f, composed.Speed, 1e-4f);
            ((Pierce)composed.OnHit[0]).Count = 9;
            Assert.AreEqual(0, basis.OnHit.Length);
        }

        [Test]
        public void ApplyTo_DoesNotAliasKitModifier()
        {
            var authored = new Pierce(2);
            pierceKit = Kit(RecipeStage.OnHit, authored);
            Recipe basis = Basis();
            pierceKit.ApplyTo(basis);

            authored.Count = 7;
            Assert.AreEqual(2, ((Pierce)basis.OnHit[0]).Count);
            Assert.AreEqual("base", basis.Id);
        }

        [Test]
        public void EmptyKit_IsIdentityExceptTheNewId()
        {
            Recipe basis = Basis();
            basis.Speed = 12f;
            pierceKit = ScriptableObject.CreateInstance<ModifierKit>();
            Recipe composed = Recipe.Compose(basis, "empty", pierceKit, null);

            Assert.AreEqual("empty", composed.Id);
            Assert.AreEqual("base", basis.Id);
            Assert.AreEqual(12f, composed.Speed, 1e-4f);
            Assert.AreEqual(0, composed.OnHit.Length);
            Assert.AreEqual(0, composed.OnTick.Length);
        }

        [Test]
        public void SameComposeId_ReplacesTheStoreSlot()
        {
            Recipe basis = Basis();
            basis.Speed = 20f;
            basis.LifetimeSeconds = 5f;
            pierceKit = Kit(RecipeStage.OnHit, new Pierce(2));
            Recipe piercing = Recipe.Compose(basis, "shared", pierceKit);
            Recipe plain = Recipe.Compose(basis, "shared");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(piercing);
            using (world)
            {
                ProjectileId first = world.Spawn(new SpawnRequest
                {
                    Recipe = piercing,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Assert.IsTrue(world.TryGetState(first, out _, out _, out Recipe stored));
                Assert.IsInstanceOf<Pierce>(stored.OnHit[0]);

                world.Spawn(new SpawnRequest
                {
                    Recipe = plain,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Assert.IsTrue(world.TryGetState(first, out _, out _, out stored));
                Assert.AreEqual(0, stored.OnHit.Length);
            }
        }

        static Recipe Basis()
        {
            return new Recipe
            {
                Id = "base",
                OnSpawn = Array.Empty<IModifier>(),
                OnHit = Array.Empty<IModifier>(),
                OnTick = Array.Empty<IModifier>()
            };
        }

        static ModifierKit Kit(RecipeStage stage, IModifier modifier)
        {
            ModifierKit kit = ScriptableObject.CreateInstance<ModifierKit>();
            kit.Layers = new[] { new ModifierKitLayer(stage, modifier) };
            return kit;
        }
    }
}
