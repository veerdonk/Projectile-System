using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PoolCountTests
    {
        [Test]
        public void AvailableCount_UnchangedAfterRentAndReturn()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("pool", speed: 10f, lifetime: 1f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                Assert.AreEqual(4, world.AvailableCount);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, world.AvailableCount);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(4, world.AvailableCount);
            }
        }
    }
}
