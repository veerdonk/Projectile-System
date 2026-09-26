using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
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
        public void Homing_DoesNotRetargetUntilATargetHit()
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
        public void Homing_OnTargetHit_DropsLock_AndDoesNotReacquireThatTargetWhileItStaysAhead()
        {
            Recipe recipe = HomingPierceRecipe("home-retarget", 90f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                world.SetHomingCandidate(first, new Vector3(0f, 0f, 8f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(first, locked.Id);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, first);
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.IsFalse(locked.IsAssigned);

                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void Homing_WorldHit_DoesNotDropLock()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "home-world",
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(90f) },
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
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
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(targetId, locked.Id);

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 1f), Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(targetId, locked.Id);
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

        [Test]
        public void Homing_FirstLock_IsClosestToAcquirePoint_EvenBehindTravel()
        {
            Recipe recipe = HomingRecipe("home-aim", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out _, out TargetId aimed);
                ProjectileId id = SpawnAimed(world, recipe, aimedAt: AimedPoint, assigned: default);
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget target));
                Assert.AreEqual(aimed, target.Id);
            }
        }

        [Test]
        public void Homing_WithoutAcquirePoint_KeepsTravelDirectionLock()
        {
            Recipe recipe = HomingRecipe("home-aim-off", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out TargetId travel, out _);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = false,
                    HomingAcquirePoint = AimedPoint
                });
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget target));
                Assert.AreEqual(travel, target.Id);
            }
        }

        [Test]
        public void Homing_AssignedTarget_WinsOverAcquirePoint()
        {
            Recipe recipe = HomingRecipe("home-aim-assigned", 100f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out TargetId travel, out _);
                ProjectileId id = SpawnAimed(world, recipe, AimedPoint, HomingTarget.Assigned(travel));
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget target));
                Assert.AreEqual(travel, target.Id);
            }
        }

        [Test]
        public void Homing_AfterAimPointLock_RetargetUsesTravelDirection()
        {
            Recipe recipe = HomingPierceRecipe("home-aim-retarget", 90f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out TargetId travel, out TargetId aimed);
                ProjectileId id = SpawnAimed(world, recipe, AimedPoint, default);
                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(aimed, locked.Id);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, aimed);
                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.IsFalse(locked.IsAssigned);

                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(travel, locked.Id);
            }
        }

        [Test]
        public void Homing_AssignedTarget_ThenHit_DoesNotFallBackToAcquirePoint()
        {
            Recipe recipe = HomingPierceRecipe("home-aim-assigned-hit", 90f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out TargetId travel, out _);
                ProjectileId id = SpawnAimed(world, recipe, AimedPoint, HomingTarget.Assigned(travel));
                world.Tick(0.1f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, travel);
                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void MulticastExtra_InheritsAcquirePoint_AndLocksClosestToIt()
        {
            Recipe extra = HomingRecipe("home-aim-extra", 100f);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "home-aim-multi",
                onSpawn: new IModifier[] { new Multicast(2, extra, new StraightAimPattern()) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishAimPair(world, out _, out TargetId aimed);
                world.Spawn(SpawnAimedRequest(recipe, AimedPoint, default));
                world.Tick(0.01f);

                var active = SimulationTestHelper.CollectActive(world);
                Assert.AreEqual(2, active.Count);
                bool foundExtra = false;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].recipe.Id != extra.Id)
                        continue;

                    Assert.IsTrue(world.TryGetHomingTarget(active[i].id, out HomingTarget target));
                    Assert.AreEqual(aimed, target.Id);
                    foundExtra = true;
                }

                Assert.IsTrue(foundExtra);
            }
        }

        static readonly Vector3 AimedPoint = new Vector3(6f, 0f, -1f);

        static void PublishAimPair(ProjectileWorld world, out TargetId travel, out TargetId aimed)
        {
            travel = new TargetId(1);
            aimed = new TargetId(2);
            world.SetHitTarget(travel, new Vector3(0f, 0f, 30f), 0.5f);
            world.SetHitTarget(aimed, AimedPoint, 0.5f);
        }

        static ProjectileId SpawnAimed(ProjectileWorld world, Recipe recipe, Vector3 aimedAt, HomingTarget assigned)
        {
            return world.Spawn(SpawnAimedRequest(recipe, aimedAt, assigned));
        }

        static SpawnRequest SpawnAimedRequest(Recipe recipe, Vector3 aimedAt, HomingTarget assigned)
        {
            return new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                HomingTarget = assigned,
                HomingAcquirePointSet = true,
                HomingAcquirePoint = aimedAt
            };
        }

        sealed class StraightAimPattern : IMulticastPattern
        {
            public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
            {
                return forward.sqrMagnitude > 1e-8f ? forward : Vector3.forward;
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

        static Recipe HomingPierceRecipe(string id, float turnRate)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(turnRate) },
                onHit: new IModifier[] { new Pierce(1) });
        }
    }
}
