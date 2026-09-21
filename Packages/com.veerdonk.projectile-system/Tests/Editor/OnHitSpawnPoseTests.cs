using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class OnHitSpawnPoseTests
    {
        [Test]
        public void MultiplyOnHit_SpawnsChildAtPointAlongNormal_UsingChildRecipe()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(1, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                ProjectileId parentId = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1));

                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out ProjectileId childId, out Vector3 position, out Vector3 velocity, out Recipe recipe));
                Assert.AreNotEqual(parentId, childId);
                Assert.AreEqual(child.Id, recipe.Id);
                Vector3 expected = SimulationTestHelper.OffSurface(point, normal, child.Radius);
                Assert.AreEqual(expected.x, position.x, 0.0001f);
                Assert.AreEqual(expected.y, position.y, 0.0001f);
                Assert.AreEqual(expected.z, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.y, 0.0001f);
                Assert.AreEqual(0f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Multiply0_OnHit_DoesNotSpawnChild()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("child-unused", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "parent-multiply-zero",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(0, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                int availableBefore = world.AvailableCount;

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
            }
        }
    }
}
