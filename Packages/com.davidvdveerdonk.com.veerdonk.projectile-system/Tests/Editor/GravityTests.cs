using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class GravityTests
    {
        [Test]
        public void Gravity0_StaysLevelOverSeveralTicks()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("gravity-zero", speed: 10f, lifetime: 10f, gravity: 0f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 5; i++)
                {
                    world.Tick(0.2f);
                    Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                    Assert.AreEqual(0f, position.y, 0.0001f);
                    Assert.AreEqual(0f, position.x, 0.0001f);
                    Assert.AreEqual(10f * 0.2f * (i + 1), position.z, 0.0001f);
                    Assert.AreEqual(10f, velocity.z, 0.0001f);
                    Assert.AreEqual(0f, velocity.y, 0.0001f);
                }
            }
        }

        [Test]
        public void GravityPositive_DecreasesY_WhileXZFollowLaunch()
        {
            const float gravity = 10f;
            const float speed = 10f;
            const float dt = 0.2f;
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "gravity-drop",
                speed: speed,
                lifetime: 10f,
                gravity: gravity);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                Vector3 direction = new Vector3(3f, 0f, 4f).normalized;
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = direction
                });

                float expectedY = 0f;
                float expectedVy = 0f;
                for (int i = 0; i < 4; i++)
                {
                    world.Tick(dt);
                    expectedVy -= gravity * dt;
                    expectedY += expectedVy * dt;

                    Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                    Assert.AreEqual(expectedY, position.y, 0.0001f);
                    Assert.Less(position.y, 0f);
                    Assert.AreEqual(direction.x * speed * dt * (i + 1), position.x, 0.0001f);
                    Assert.AreEqual(direction.z * speed * dt * (i + 1), position.z, 0.0001f);
                    Assert.AreEqual(expectedVy, velocity.y, 0.0001f);
                    Assert.AreEqual(direction.x * speed, velocity.x, 0.0001f);
                    Assert.AreEqual(direction.z * speed, velocity.z, 0.0001f);
                }
            }
        }

        [Test]
        public void GravityNegative_KeepsStraightIntegrate()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("gravity-negative", speed: 10f, lifetime: 10f);
            recipe.Gravity = -20f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.25f);
                world.Tick(0.25f);

                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                Assert.AreEqual(0f, position.y, 0.0001f);
                Assert.AreEqual(5f, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(10f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Gravity_IsCopiedOntoTheInstance_AndLeavesTheRecipeUnchanged()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("gravity-copy", speed: 10f, lifetime: 10f, gravity: 12f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetGravity(id, out float gravity));
                Assert.AreEqual(12f, gravity, 0.0001f);

                world.Tick(0.1f);
                world.Tick(0.1f);
                Assert.AreEqual(12f, recipe.Gravity, 0.0001f);
                Assert.IsTrue(world.TryGetGravity(id, out gravity));
                Assert.AreEqual(12f, gravity, 0.0001f);
            }
        }

        [Test]
        public void ModifierContext_ExposesInstanceGravity()
        {
            var probe = new GravityProbe();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "gravity-context",
                speed: 10f,
                lifetime: 10f,
                gravity: 7f,
                onTravel: new IModifier[] { probe });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(7f, probe.Gravity, 0.0001f);
            }
        }

        [Test]
        public void Bounce_ReflectsGravityUpdatedVelocity()
        {
            const float gravity = 10f;
            const float speed = 20f;
            const float dt = 0.1f;
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "gravity-bounce",
                speed: speed,
                lifetime: 5f,
                gravity: gravity,
                onHit: new IModifier[] { new Bounce(1) });
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
                world.Tick(dt);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out int bounce));
                Assert.AreEqual(0, bounce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Vector3 incoming = new Vector3(0f, -gravity * dt, speed);
                Vector3 expected = Vector3.Reflect(incoming, normal.normalized);
                Assert.AreEqual(expected.x, velocity.x, 0.0001f);
                Assert.AreEqual(expected.y, velocity.y, 0.0001f);
                Assert.AreEqual(expected.z, velocity.z, 0.0001f);
            }
        }

        sealed class GravityProbe : IModifier
        {
            public float Gravity { get; private set; }

            public void Apply(ModifierContext context)
            {
                Gravity = context.Gravity;
            }
        }
    }
}
