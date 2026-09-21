using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PierceTests
    {
        [Test]
        public void Pierce2_FirstTwoTargetHitsContinue_ThirdDespawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(2) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                int availableBefore = world.AvailableCount;
                var seen = new System.Collections.Generic.List<HitInfo>();
                world.Hit += info => seen.Add(info);
                ExpireInfo? expired = null;
                world.Expired += info => expired = info;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out int bounce));
                Assert.AreEqual(2, pierce);
                Assert.AreEqual(0, bounce);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out pierce, out bounce));
                Assert.AreEqual(1, pierce);
                Assert.AreEqual(0, bounce);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(2));
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out pierce, out bounce));
                Assert.AreEqual(0, pierce);
                Assert.AreEqual(0, bounce);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 6f), Vector3.back, new TargetId(3));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(new TargetId(1), seen[0].TargetId);
                Assert.AreEqual(new TargetId(2), seen[1].TargetId);
                Assert.AreEqual(new TargetId(3), seen[2].TargetId);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
            }
        }

        [Test]
        public void Pierce0_TargetHitDespawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-zero",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(0) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                int availableBefore = world.AvailableCount;
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
            }
        }

        [Test]
        public void Pierce_DoesNotAbsorbWorldHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-world",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(2) });
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
