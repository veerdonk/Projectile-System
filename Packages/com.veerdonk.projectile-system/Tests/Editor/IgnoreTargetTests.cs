using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class IgnoreTargetTests
    {
        const float ArcherRadius = 1.06f;
        const float ArcherHeight = 3.15f;
        const float ArcherHoming = 1.6f;
        static readonly TargetId Shooter = new TargetId(1);
        static readonly TargetId Other = new TargetId(2);
        static readonly Vector3 Muzzle = new Vector3(0f, ArcherHoming, 0f);

        [Test]
        public void WithoutSkip_ArrowFromTheAxis_HitsItsShooter()
        {
            Recipe recipe = Arrow("ignore-target-control");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                world.Spawn(new SpawnRequest { Recipe = recipe, Position = Muzzle, Direction = Vector3.forward });

                world.Tick(0.05f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Shooter, seen[0].TargetId);
            }
        }

        [Test]
        public void WithSkip_ArrowFromTheAxis_SkipsItsShooter_AndHitsAnotherEnemy()
        {
            Recipe recipe = Arrow("ignore-target-other");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                PublishArcher(world, Other, new Vector3(0f, 0f, 15f));
                world.Spawn(Shot(recipe));

                for (int i = 0; i < 20; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Other, seen[0].TargetId);
                Assert.AreEqual(15f - ArcherRadius, seen[0].Point.z, 1e-3f);
            }
        }

        [Test]
        public void AfterTheBodyClears_ALaterContactWithTheShooterIsANormalHit()
        {
            Recipe recipe = Arrow("ignore-target-later");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                world.Spawn(Shot(recipe));

                world.Tick(0.1f);
                world.Tick(0.1f);
                Assert.AreEqual(0, seen.Count);

                PublishArcher(world, Shooter, new Vector3(0f, 0f, 10f));
                for (int i = 0; i < 10; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Shooter, seen[0].TargetId);
            }
        }

        [Test]
        public void MulticastExtras_CopyTheSkip()
        {
            Recipe extra = Arrow("ignore-target-extra");
            Recipe parent = Arrow("ignore-target-parent");
            parent.OnSpawn = new IModifier[] { new Multicast(3, extra) };
            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: 9u);
            using (world)
            {
                world.Prewarm(parent, 4);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                world.Spawn(Shot(parent));
                Assert.AreEqual(3, world.ActiveCount);

                for (int i = 0; i < 10; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(0, seen.Count);
            }
        }

        [Test]
        public void Homing_NeverAcquiresTheShooter_LocksTheOtherEnemy()
        {
            Recipe recipe = Arrow("ignore-target-homing", onTick: new IModifier[] { new Homing(720f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(Shooter, Muzzle + Vector3.forward * 0.5f, ArcherRadius);
                world.SetHitTarget(Other, new Vector3(4f, ArcherHoming, 10f), 0.5f);
                world.Spawn(Shot(recipe));

                for (int i = 0; i < 40; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(Other, locks[0].TargetId);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Other, seen[0].TargetId);
            }
        }

        [Test]
        public void AimPointAcquisition_AtTheShooter_StillLocksTheOtherEnemy()
        {
            Recipe recipe = Arrow("ignore-target-aim", onTick: new IModifier[] { new Homing(720f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                world.SetHitTarget(Other, new Vector3(4f, ArcherHoming, 10f), 0.5f);
                SpawnRequest request = Shot(recipe);
                request.HomingAcquirePointSet = true;
                request.HomingAcquirePoint = Muzzle;
                world.Spawn(request);

                world.Tick(0.05f);

                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(Other, locks[0].TargetId);
            }
        }

        [Test]
        public void Beam_FromInsideItsShooter_SkipsItOnEveryPulse()
        {
            Recipe recipe = Arrow("ignore-target-beam");
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                PublishArcher(world, Other, new Vector3(0f, 0f, 10f));
                world.Spawn(Shot(recipe));
                Assert.AreEqual(1, seen.Count);

                for (int pulse = 2; pulse <= 5; pulse++)
                {
                    world.Tick(0.1f);
                    Assert.AreEqual(pulse, seen.Count);
                }

                for (int i = 0; i < seen.Count; i++)
                    Assert.AreEqual(Other, seen[i].TargetId);
            }
        }

        [Test]
        public void Hitscan_FromInsideItsShooter_HitsTheOtherEnemy()
        {
            Recipe recipe = Arrow("ignore-target-hitscan");
            recipe.Delivery = Delivery.Hitscan;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                PublishArcher(world, Other, new Vector3(0f, 0f, 10f));
                world.Spawn(Shot(recipe));

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Other, seen[0].TargetId);
            }
        }

        [Test]
        public void BounceOffAnotherEnemyInsideTheShooter_KeepsTheShooterSkip()
        {
            Recipe recipe = Arrow("ignore-target-bounce");
            recipe.OnHit = new IModifier[] { new Bounce(1) };
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                PublishArcher(world, Other, new Vector3(0f, 0f, 2f));
                world.Spawn(Shot(recipe));

                for (int i = 0; i < 20; i++)
                    world.Tick(0.02f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(Other, seen[0].TargetId);
            }
        }

        [Test]
        public void MultiplyChildren_SpawnedInsideTheShooter_CopyTheSkip()
        {
            Recipe child = Arrow("ignore-target-multiply-child");
            Recipe parent = SimulationTestHelper.CreateRecipe("ignore-target-multiply", speed: 1f, lifetime: 0.5f, radius: 0.1f);
            parent.OnSpawn = new IModifier[] { new Interval(0.1f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child) };
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 8);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                PublishArcher(world, Shooter, Vector3.zero);
                world.Spawn(Shot(parent));

                world.Tick(0.12f);
                Assert.Greater(world.ActiveCount, 1);
                for (int i = 0; i < 4; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(0, seen.Count);
            }
        }

        [Test]
        public void CustomAcquisition_ThatPicksTheShooter_DoesNotLock()
        {
            Recipe recipe = Arrow("ignore-target-custom", onTick: new IModifier[] { new Homing(720f, new PickShooter()) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int locks = 0;
                world.HomingLocked += _ => locks++;
                PublishArcher(world, Shooter, Vector3.zero);
                world.Spawn(Shot(recipe));

                for (int i = 0; i < 5; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(0, locks);
            }
        }

        [Test]
        public void ShooterNotYetPublished_KeepsTheSkipUntilItIsPublishedAndCleared()
        {
            Recipe recipe = Arrow("ignore-target-late-publish");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.Spawn(Shot(recipe));
                world.Tick(0.01f);
                PublishArcher(world, Shooter, Vector3.zero);

                for (int i = 0; i < 10; i++)
                    world.Tick(0.02f);

                Assert.AreEqual(0, seen.Count);
            }
        }

        [Test]
        public void RetiredShot_ReleasesTheShooterRecord()
        {
            Recipe recipe = Arrow("ignore-target-release");
            recipe.LifetimeSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                PublishArcher(world, Shooter, Vector3.zero);
                ProjectileId id = world.Spawn(Shot(recipe));
                HitTargetCatalog catalog = Catalog(world);
                Assert.IsTrue(catalog.TryGetShooter(id, out _));

                world.Tick(0.2f);
                world.Tick(0.01f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.IsFalse(catalog.TryGetShooter(id, out _));
            }
        }

        static HitTargetCatalog Catalog(ProjectileWorld world)
        {
            return (HitTargetCatalog)typeof(ProjectileWorld)
                .GetField("hitTargets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(world);
        }

        sealed class PickShooter : IHomingAcquisition
        {
            public bool TryAcquire(ModifierContext context, out HomingTarget target)
            {
                target = HomingTarget.Assigned(Shooter);
                return true;
            }
        }

        static Recipe Arrow(string id, IModifier[] onTick = null)
        {
            return SimulationTestHelper.CreateRecipe(id, speed: 20f, lifetime: 5f, radius: 0.1f, onTick: onTick);
        }

        static SpawnRequest Shot(Recipe recipe)
        {
            return new SpawnRequest
            {
                Recipe = recipe,
                Position = Muzzle,
                Direction = Vector3.forward,
                IgnoreTargetSet = true,
                IgnoreTarget = Shooter
            };
        }

        static void PublishArcher(ProjectileWorld world, TargetId id, Vector3 bottom)
        {
            world.SetHitTarget(id, bottom, ArcherRadius, ArcherHeight, ArcherHoming);
        }
    }
}
