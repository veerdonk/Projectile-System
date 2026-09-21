using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class LiveSnapshotTests
    {
        [Test]
        public void SecondInstanceStillMoves_WhenFirstDespawnsOnHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("snap", speed: 10f, lifetime: 10f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                ProjectileId first = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                ProjectileId second = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.right,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.forward, Vector3.back, new TargetId(1));
                world.Tick(0.5f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out ProjectileId remaining, out Vector3 position, out _, out _));
                Assert.AreEqual(5f, position.z, 0.0001f);
                Assert.IsTrue(remaining == first || remaining == second);
                if (remaining == first)
                {
                    Assert.AreEqual(0f, position.x, 0.0001f);
                    Assert.IsFalse(world.TryGetState(second, out _, out _, out _));
                }
                else
                {
                    Assert.AreEqual(1f, position.x, 0.0001f);
                    Assert.IsFalse(world.TryGetState(first, out _, out _, out _));
                }
            }
        }
    }
}
