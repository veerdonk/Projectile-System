using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HitDespawnTests
    {
        [Test]
        public void TargetHit_RaisesHit_Despawns_AndRestoresPool()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("hit", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                int availableBefore = world.AvailableCount;
                var targetId = new TargetId(7);

                HitInfo? seen = null;
                world.Hit += info => seen = info;
                ExpireInfo? expired = null;
                world.Expired += info => expired = info;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                Vector3 normal = Vector3.back;
                hits.Queue(HitKind.Target, point, normal, targetId);

                world.Tick(0.1f);

                Assert.IsTrue(seen.HasValue);
                Assert.AreEqual(id, seen.Value.InstanceId);
                Assert.AreEqual(HitKind.Target, seen.Value.Kind);
                Assert.AreEqual(targetId, seen.Value.TargetId);
                Assert.AreEqual(point, seen.Value.Point);
                Assert.AreEqual(normal.normalized, seen.Value.Normal);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
            }
        }

        [Test]
        public void WorldHit_Despawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("world-hit", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 1f), Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
            }
        }
    }
}
