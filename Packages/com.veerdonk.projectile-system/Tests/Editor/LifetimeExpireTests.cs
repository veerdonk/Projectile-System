using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class LifetimeExpireTests
    {
        [Test]
        public void Tick_ExpiresAtLifetimeAndReturnsToPool_WithoutChildren()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("lifetime", speed: 4f, lifetime: 3f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                int availableBefore = world.AvailableCount;

                ExpireInfo? expired = null;
                world.Expired += info => expired = info;
                int hitCount = 0;
                world.Hit += _ => hitCount++;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(3f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
                Assert.AreEqual(0, hitCount);
                Assert.IsFalse(world.TryGetState(id, out _, out _, out _));
            }
        }
    }
}
