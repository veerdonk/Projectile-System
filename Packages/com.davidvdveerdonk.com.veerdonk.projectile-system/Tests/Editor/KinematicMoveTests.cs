using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class KinematicMoveTests
    {
        [Test]
        public void Tick_MovesAlongDirectionBySpeedTimesDt()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("move", speed: 10f, lifetime: 10f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);

            ProjectileId id = world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });

            world.Tick(0.5f);

            Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
            Assert.AreEqual(5f, position.z, 0.0001f);
            Assert.AreEqual(0f, position.x, 0.0001f);
            Assert.AreEqual(0f, position.y, 0.0001f);
            Assert.AreEqual(10f, velocity.z, 0.0001f);
        }
    }
}
