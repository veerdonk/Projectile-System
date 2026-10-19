using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PathPredictionTests
    {
        const float Step = 1f / 60f;
        const float CapsuleRadius = 1.06f;
        const float CapsuleHeight = 3.15f;
        const float CapsuleHoming = 1.6f;
        static readonly TargetId Shooter = new TargetId(1);
        static readonly TargetId Enemy = new TargetId(2);
        static readonly Vector3 Muzzle = new Vector3(0f, CapsuleHoming, 0f);
        static readonly Vector3 Lob = new Vector3(0f, 0.2f, 1f);
        static readonly PathPrediction Settings = new PathPrediction { StepSeconds = Step };

        [Test]
        public void ArcIntoCapsule_ReturnsTarget_AtTheTwinsHit()
        {
            Recipe recipe = ArcShot("predict-capsule");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 12f));
                var request = new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob };
                var points = new List<Vector3>();

                Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit hit));
                Assert.AreEqual(HitKind.Target, hit.Kind);
                Assert.AreEqual(Enemy, hit.TargetId);
                Assert.AreEqual(hit.Point, points[points.Count - 1]);
                Assert.AreEqual(Muzzle, points[0]);

                HitInfo twin = TwinHit(world, request, out float twinTime);
                Assert.AreEqual(Enemy, twin.TargetId);
                Assert.Less(Vector3.Distance(twin.Point, hit.Point), 0.05f);
                Assert.AreEqual(twinTime, hit.Time, Step * 0.5f);
            }
        }

        [Test]
        public void SpeedOnTravel_MatchesTheTwin()
        {
            Recipe recipe = ArcShot("predict-speed");
            recipe.Speed = 7.5f;
            recipe.OnTravel = new IModifier[] { new Speed(2f) };
            Recipe plain = ArcShot("predict-speed-plain");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 12f));
                var request = new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob };
                var points = new List<Vector3>();
                var plainPoints = new List<Vector3>();

                Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit hit));
                Assert.IsTrue(world.PredictPath(
                    new SpawnRequest { Recipe = plain, Position = Muzzle, Direction = Lob },
                    Settings,
                    plainPoints,
                    out PathHit plainHit));

                HitInfo twin = TwinHit(world, request, out _);
                Assert.AreEqual(Enemy, hit.TargetId);
                Assert.Less(Vector3.Distance(twin.Point, hit.Point), 0.05f);
                Assert.AreEqual(plainPoints.Count, points.Count);
                Assert.Less(Vector3.Distance(plainHit.Point, hit.Point), 1e-4f);
            }
        }

        [Test]
        public void LifetimeEnd_ReturnsFalse_WithTheTwinsExpirePositionLast()
        {
            Recipe recipe = ArcShot("predict-lifetime");
            recipe.LifetimeSeconds = 1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var request = new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob };
                var points = new List<Vector3>();

                Assert.IsFalse(world.PredictPath(request, Settings, points, out PathHit hit));
                Assert.AreEqual(default(PathHit), hit);

                var expired = new List<ExpireInfo>();
                world.Expired += info => expired.Add(info);
                world.Spawn(request);
                for (int i = 0; i < 120 && expired.Count == 0; i++)
                    world.Tick(Step);

                Assert.AreEqual(1, expired.Count);
                Assert.AreEqual(ExpireReason.Lifetime, expired[0].Reason);
                Assert.Less(Vector3.Distance(expired[0].Position, points[points.Count - 1]), 1e-4f);
            }
        }

        [Test]
        public void StepCap_EndsThePath()
        {
            Recipe recipe = ArcShot("predict-cap");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var points = new List<Vector3>();
                var settings = new PathPrediction { StepSeconds = Step, MaxSteps = 10 };

                Assert.IsFalse(world.PredictPath(
                    new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob },
                    settings,
                    points,
                    out _));
                Assert.AreEqual(11, points.Count);
            }
        }

        [Test]
        public void Prediction_RaisesNoEvents_AndLeavesThePoolAsItWas()
        {
            Recipe recipe = ArcShot("predict-quiet");
            recipe.OnSpawn = new IModifier[] { new Multicast(3, ArcShot("predict-quiet-extra")) };
            recipe.OnHit = new IModifier[] { new AreaEffect(), new Multiply(2, ArcShot("predict-quiet-child")) };
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int events = 0;
                world.Spawned += _ => events++;
                world.Hit += _ => events++;
                world.Expired += _ => events++;
                world.HomingLocked += _ => events++;
                world.StatusApplied += _ => events++;
                world.AreaBurst += _ => events++;
                world.GroundFieldPlanted += _ => events++;
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 12f));
                int active = world.ActiveCount;
                int available = world.AvailableCount;
                var points = new List<Vector3>();

                Assert.IsTrue(world.PredictPath(
                    new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Muzzle,
                        Direction = Lob,
                        HomingTarget = HomingTarget.Assigned(Enemy)
                    },
                    Settings,
                    points,
                    out _));
                world.Tick(Step);

                Assert.AreEqual(0, events);
                Assert.AreEqual(active, world.ActiveCount);
                Assert.AreEqual(available, world.AvailableCount);
            }
        }

        [Test]
        public void StartInsideTheIgnoreTarget_DoesNotHitIt()
        {
            Recipe recipe = Arrow("predict-ignore-target");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Shooter, Vector3.zero);
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 15f));
                var points = new List<Vector3>();

                Assert.IsTrue(world.PredictPath(Shot(recipe, skip: true), Settings, points, out PathHit skipped));
                Assert.AreEqual(Enemy, skipped.TargetId);
                Assert.AreEqual(15f - CapsuleRadius, skipped.Point.z, 1e-3f);

                Assert.IsTrue(world.PredictPath(Shot(recipe, skip: false), Settings, points, out PathHit control));
                Assert.AreEqual(Shooter, control.TargetId);
                Assert.AreEqual(2, points.Count);
            }
        }

        [Test]
        public void HitscanAndBeam_ReturnFalse_WithAnEmptyList()
        {
            Recipe hitscan = Arrow("predict-hitscan");
            hitscan.Delivery = Delivery.Hitscan;
            Recipe beam = Arrow("predict-beam");
            beam.Delivery = Delivery.Beam;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(hitscan);
            using (world)
            {
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 5f));
                var points = new List<Vector3> { Vector3.one };

                Assert.IsFalse(world.PredictPath(Shot(hitscan, skip: false), Settings, points, out _));
                Assert.AreEqual(0, points.Count);
                Assert.IsFalse(world.PredictPath(Shot(beam, skip: false), Settings, points, out _));
                Assert.AreEqual(0, points.Count);
            }
        }

        [Test]
        public void UnreachableArcAimPoint_ReturnsFalse_WithAnEmptyList()
        {
            Recipe recipe = ArcShot("predict-unreachable");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var points = new List<Vector3>();
                var request = new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Muzzle,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = new Vector3(0f, 0f, 500f)
                };

                Assert.IsFalse(world.PredictPath(request, Settings, points, out _));
                Assert.AreEqual(0, points.Count);
            }
        }

        [Test]
        public void ArcAimPoint_UsesTheBallisticSolve()
        {
            Recipe recipe = ArcShot("predict-solve");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                Vector3 bottom = new Vector3(0f, 0f, 14f);
                PublishCapsule(world, Enemy, bottom);
                var points = new List<Vector3>();
                var request = new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Muzzle,
                    Direction = Vector3.back,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = bottom + Vector3.up * CapsuleHoming
                };

                Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit hit));
                Assert.AreEqual(Enemy, hit.TargetId);
            }
        }

        [Test]
        public void InsideAHitHandler_PredictionLeavesLiveSkipsAsTheyWere()
        {
            Recipe recipe = Arrow("predict-in-handler");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Shooter, Vector3.zero);
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 15f));
                var points = new List<Vector3>();
                var seen = new List<HitInfo>();
                PathHit predicted = default;
                bool predictedHit = false;
                bool skipActiveAfterPrediction = false;
                ProjectileId inside = default;
                world.Hit += info =>
                {
                    seen.Add(info);
                    if (seen.Count != 1)
                        return;
                    predictedHit = world.PredictPath(Shot(recipe, skip: false), Settings, points, out predicted);
                    skipActiveAfterPrediction = ShooterSkipActive(world, inside);
                };

                inside = world.Spawn(Shot(recipe, skip: true));
                world.Tick(0.05f);
                Assert.IsTrue(ShooterSkipActive(world, inside));
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, CapsuleHoming, 13.5f),
                    Direction = Vector3.forward
                });
                world.Tick(0.05f);

                Assert.AreEqual(1, seen.Count);
                Assert.IsTrue(predictedHit);
                Assert.AreEqual(Shooter, predicted.TargetId);
                Assert.IsTrue(skipActiveAfterPrediction);
                Assert.IsTrue(ShooterSkipActive(world, inside));
                Assert.IsFalse(Catalog(world).TryGetShooter(new ProjectileId(int.MinValue), out _));

                for (int i = 0; i < 20; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(Enemy, seen[1].TargetId);
            }
        }

        [Test]
        public void GravityOnSpawn_MatchesTheTwin()
        {
            Recipe recipe = ArcShot("predict-gravity");
            recipe.OnSpawn = new IModifier[] { new Gravity(2f) };
            AssertMatchesTwin(recipe, new Vector3(0f, 0f, 7f));
        }

        [Test]
        public void SizeOnTravel_MatchesTheTwin()
        {
            Recipe recipe = ArcShot("predict-size");
            recipe.OnTravel = new IModifier[] { new Size(3f) };
            AssertMatchesTwin(recipe, new Vector3(0f, 0f, 12f));
        }

        [Test]
        public void GravityOnStraight_DoesNotFall()
        {
            Recipe recipe = Arrow("predict-straight-gravity");
            recipe.Gravity = 9.81f;
            recipe.OnSpawn = new IModifier[] { new Gravity(2f) };
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var points = new List<Vector3>();
                Assert.IsFalse(world.PredictPath(Shot(recipe, skip: false), Settings, points, out _));
                for (int i = 0; i < points.Count; i++)
                    Assert.AreEqual(CapsuleHoming, points[i].y, 1e-5f);
            }
        }

        [Test]
        public void DefaultSettings_UseOneSixtiethAnd1024Steps()
        {
            Recipe recipe = Arrow("predict-defaults");
            recipe.LifetimeSeconds = 100f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var points = new List<Vector3>();
                Assert.IsFalse(world.PredictPath(Shot(recipe, skip: false), default, points, out _));
                Assert.AreEqual(1025, points.Count);
                Assert.AreEqual(20f / 60f, points[1].z - points[0].z, 1e-4f);
            }
        }

        [Test]
        public void RepeatCall_DoesNotAllocate()
        {
            Recipe recipe = ArcShot("predict-alloc");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Shooter, Vector3.zero);
                PublishCapsule(world, Enemy, new Vector3(0f, 0f, 12f));
                SpawnRequest request = Shot(recipe, skip: true);
                request.Direction = Lob;
                request.IgnoreColliders = new Collider[0];
                var points = new List<Vector3>();
                world.PredictPath(request, Settings, points, out _);

                Assert.That(() => { world.PredictPath(request, Settings, points, out _); }, Is.Not.AllocatingGCMemory());
            }
        }

        [Test]
        public void RepeatCall_ReusesTheList()
        {
            Recipe recipe = ArcShot("predict-reuse");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var request = new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob };
                var points = new List<Vector3>();
                world.PredictPath(request, Settings, points, out _);
                int count = points.Count;
                int capacity = points.Capacity;

                world.PredictPath(request, Settings, points, out _);

                Assert.AreEqual(count, points.Count);
                Assert.AreEqual(capacity, points.Capacity);
            }
        }

        static HitInfo TwinHit(ProjectileWorld world, in SpawnRequest request, out float time)
        {
            var seen = new List<HitInfo>();
            System.Action<HitInfo> record = info => seen.Add(info);
            world.Hit += record;
            world.Spawn(request);
            int ticks = 0;
            while (seen.Count == 0 && ticks < 600)
            {
                world.Tick(Step);
                ticks++;
            }

            world.Hit -= record;
            Assert.AreEqual(1, seen.Count);
            time = ticks * Step;
            return seen[0];
        }

        static void AssertMatchesTwin(Recipe recipe, Vector3 enemyBottom)
        {
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishCapsule(world, Enemy, enemyBottom);
                var request = new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Lob };
                var points = new List<Vector3>();

                Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit hit));
                HitInfo twin = TwinHit(world, request, out float twinTime);
                Assert.AreEqual(Enemy, hit.TargetId);
                Assert.Less(Vector3.Distance(twin.Point, hit.Point), 1e-3f);
                Assert.AreEqual(twinTime, hit.Time, Step * 0.5f);
            }
        }

        static bool ShooterSkipActive(ProjectileWorld world, ProjectileId id)
        {
            var shooters = (System.Collections.IDictionary)typeof(HitTargetCatalog)
                .GetField("shooters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(Catalog(world));
            object skip = shooters[id];
            return skip != null && (bool)skip.GetType().GetProperty("Active").GetValue(skip);
        }

        static HitTargetCatalog Catalog(ProjectileWorld world)
        {
            return (HitTargetCatalog)typeof(ProjectileWorld)
                .GetField("hitTargets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(world);
        }

        static Recipe ArcShot(string id)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(id, speed: 15f, lifetime: 5f, radius: 0.2f, gravity: 9.81f);
            recipe.Delivery = Delivery.Arc;
            return recipe;
        }

        static Recipe Arrow(string id)
        {
            return SimulationTestHelper.CreateRecipe(id, speed: 20f, lifetime: 5f, radius: 0.1f);
        }

        static SpawnRequest Shot(Recipe recipe, bool skip)
        {
            return new SpawnRequest
            {
                Recipe = recipe,
                Position = Muzzle,
                Direction = Vector3.forward,
                IgnoreTargetSet = skip,
                IgnoreTarget = Shooter
            };
        }

        static void PublishCapsule(ProjectileWorld world, TargetId id, Vector3 bottom)
        {
            world.SetHitTarget(id, bottom, CapsuleRadius, CapsuleHeight, CapsuleHoming);
        }
    }
}
