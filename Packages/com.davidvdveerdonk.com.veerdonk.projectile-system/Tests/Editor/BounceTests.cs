using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class BounceTests
    {
        [Test]
        public void Bounce1_ReflectsVelocity_ThenTargetHitDespawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "bounce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1) });
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
                Assert.AreEqual(0, pierce);
                Assert.AreEqual(1, bounce);

                Vector3 point = new Vector3(0f, 0f, 2f);
                Vector3 normal = Vector3.back;
                hits.Queue(HitKind.World, point, normal);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out pierce, out bounce));
                Assert.AreEqual(0, pierce);
                Assert.AreEqual(0, bounce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Vector3 expected = Vector3.Reflect(Vector3.forward * recipe.Speed, normal.normalized);
                Assert.AreEqual(expected.x, velocity.x, 0.0001f);
                Assert.AreEqual(expected.y, velocity.y, 0.0001f);
                Assert.AreEqual(expected.z, velocity.z, 0.0001f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.forward, new TargetId(9));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(HitKind.Target, seen[1].Kind);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
            }
        }

        [Test]
        public void Bounce_SecondWorldHitDespawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "bounce-twice",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 0f), Vector3.forward);
                world.Tick(0.1f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Bounce_DoesNotAbsorbTargetHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "bounce-target",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
            }
        }
    }
}
