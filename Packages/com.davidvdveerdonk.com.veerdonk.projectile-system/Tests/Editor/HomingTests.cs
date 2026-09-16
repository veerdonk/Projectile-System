using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class HomingTests
    {
        static readonly Vector3 SpawnPosition = new Vector3(0f, 1f, 0f);
        static readonly Vector3 Dummy1Position = new Vector3(0f, 1f, 8f);
        static readonly Vector3 Dummy2Position = new Vector3(0f, 1f, 14f);
        static readonly Vector3 DummyBouncePosition = new Vector3(4f, 1f, 12f);
        static readonly Vector3 HomingDirection = Quaternion.AngleAxis(25f, Vector3.up) * Vector3.forward;
        const float DummyHalfExtent = 1f;
        const float ProjectileRadius = 0.25f;
        const float HitDistance = DummyHalfExtent + ProjectileRadius;

        [Test]
        public void Homing_TurnsTowardAssignedTargetAtDegreesPerSecond()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "home-turn",
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(90f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var targetId = new TargetId(1);
                world.SetHomingCandidate(targetId, new Vector3(10f, 0f, 10f));
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(targetId)
                });

                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(9f, Vector3.Angle(Vector3.forward, velocity), 0.05f);
                Assert.Less(Vector3.Angle(velocity, new Vector3(1f, 0f, 1f)), 45f);
            }
        }

        [Test]
        public void Homing100_TurnsHarderThanHoming10_InTheSameDt()
        {
            Vector3 strongVelocity = TickOnce(100f);
            Vector3 weakVelocity = TickOnce(10f);

            Assert.Greater(Vector3.Angle(Vector3.forward, strongVelocity), Vector3.Angle(Vector3.forward, weakVelocity));
            Assert.AreEqual(10f, Vector3.Angle(Vector3.forward, strongVelocity), 0.05f);
            Assert.AreEqual(1f, Vector3.Angle(Vector3.forward, weakVelocity), 0.05f);
        }

        [Test]
        public void Homing_AcquiresClosestCandidateInTravelDirection()
        {
            Recipe recipe = HomingRecipe("home-acquire", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(new TargetId(1), new Vector3(0f, 0f, 10f));
                world.SetHomingCandidate(new TargetId(2), new Vector3(0f, 0f, 5f));
                world.SetHomingCandidate(new TargetId(3), new Vector3(0f, 0f, -5f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget target));
                Assert.IsTrue(target.IsAssigned);
                Assert.AreEqual(new TargetId(2), target.Id);
            }
        }

        [Test]
        public void Homing_AssignedSpawnTargetSkipsAcquisition()
        {
            Recipe recipe = HomingRecipe("home-assigned", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(new TargetId(1), new Vector3(0f, 0f, 10f));
                world.SetHomingCandidate(new TargetId(2), new Vector3(0f, 0f, 5f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(new TargetId(1))
                });

                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget target));
                Assert.AreEqual(new TargetId(1), target.Id);
            }
        }

        [Test]
        public void Homing_DoesNotRetargetAfterAcquire()
        {
            Recipe recipe = HomingRecipe("home-lock", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(new TargetId(1), new Vector3(0f, 0f, 8f));
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.01f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget first));
                Assert.AreEqual(new TargetId(1), first.Id);

                world.SetHomingCandidate(new TargetId(2), new Vector3(0f, 0f, 1f));
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(new TargetId(1), locked.Id);
            }
        }

        [Test]
        public void Homing100_HitsSandboxDummy_Homing10_Misses()
        {
            float strongClosest = ClosestApproachToDummy1(100f);
            float weakClosest = ClosestApproachToDummy1(10f);

            Assert.Less(strongClosest, HitDistance);
            Assert.Greater(weakClosest, HitDistance);
        }

        static Vector3 TickOnce(float turnRate)
        {
            Recipe recipe = HomingRecipe("home-rate", turnRate);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var targetId = new TargetId(1);
                world.SetHomingCandidate(targetId, new Vector3(10f, 0f, 10f));
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(targetId)
                });

                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                return velocity;
            }
        }

        static float ClosestApproachToDummy1(float turnRate)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "home-sandbox",
                speed: 20f,
                lifetime: 5f,
                radius: ProjectileRadius,
                onTick: new IModifier[] { new Homing(turnRate) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(new TargetId(1), Dummy1Position);
                world.SetHomingCandidate(new TargetId(2), Dummy2Position);
                world.SetHomingCandidate(new TargetId(4), DummyBouncePosition);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = SpawnPosition,
                    Direction = HomingDirection
                });

                float closest = float.PositiveInfinity;
                const float dt = 0.02f;
                for (int i = 0; i < 260; i++)
                {
                    if (!world.TryGetState(id, out Vector3 position, out _, out _))
                        break;

                    float distance = Vector3.Distance(position, Dummy1Position);
                    if (distance < closest)
                        closest = distance;

                    world.Tick(dt);
                }

                return closest;
            }
        }

        static Recipe HomingRecipe(string id, float turnRate)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(turnRate) });
        }
    }
}
