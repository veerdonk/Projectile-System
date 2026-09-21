using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class SpeedTests
    {
        [Test]
        public void Speed2_OnTravel_StartsAtTwiceRecipeSpeed()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "speed-travel",
                speed: 10f,
                lifetime: 10f,
                onTravel: new IModifier[] { new Speed(2f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                Assert.AreEqual(0f, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(20f, velocity.z, 0.0001f);

                world.Tick(0.5f);

                Assert.IsTrue(world.TryGetState(id, out position, out velocity, out _));
                Assert.AreEqual(10f, position.z, 0.0001f);
                Assert.AreEqual(20f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Speed05_OnInterval_ScalesAgainEachCrossing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "speed-interval",
                speed: 10f,
                lifetime: 10f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Speed(0.5f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(5f, velocity.magnitude, 0.0001f);
                Assert.AreEqual(5f, velocity.z, 0.0001f);

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetState(id, out _, out velocity, out _));
                Assert.AreEqual(2.5f, velocity.magnitude, 0.0001f);
                Assert.AreEqual(2.5f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Bounce1_ThenSpeed2_OnHit_ScalesReflectedVelocity()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "bounce-speed",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1), new Speed(2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                Vector3 normal = Vector3.back;
                hits.Queue(HitKind.World, point, normal);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out int bounce));
                Assert.AreEqual(0, bounce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Vector3 expected = Vector3.Reflect(Vector3.forward * recipe.Speed, normal.normalized) * 2f;
                Assert.AreEqual(expected.x, velocity.x, 0.0001f);
                Assert.AreEqual(expected.y, velocity.y, 0.0001f);
                Assert.AreEqual(expected.z, velocity.z, 0.0001f);
                Assert.AreEqual(40f, velocity.magnitude, 0.0001f);
            }
        }

        [Test]
        public void Pierce1_ThenSpeed05_OnHit_ScalesAndStaysLive()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-speed",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new Speed(0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(10f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Speed_OnInterval_RunsWhenMultiplyGenerationsAreSpent()
        {
            Recipe child = SimulationTestHelper.CreateRecipe(
                "speed-child",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Speed(0.5f) });
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "speed-parent",
                speed: 10f,
                lifetime: 0.5f,
                onExpire: new IModifier[] { new Multiply(1, child) });

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out ProjectileId childId, out _, out Vector3 velocity, out Recipe recipe));
                Assert.AreEqual(child.Id, recipe.Id);
                Assert.AreEqual(10f, velocity.magnitude, 0.0001f);

                world.Tick(0.5f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(childId, out _, out velocity, out _));
                Assert.AreEqual(5f, velocity.magnitude, 0.0001f);
                Assert.AreEqual(5f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Speed_NonPositiveScale_IsNoOp()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "speed-noop",
                speed: 10f,
                lifetime: 10f,
                onTravel: new IModifier[] { new Speed(0f), new Speed(-1f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(10f, velocity.z, 0.0001f);
            }
        }
    }
}
