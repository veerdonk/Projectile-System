using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class CapsuleHitTargetTests
    {
        const float Radius = 0.6f;
        const float Height = 3.2f;
        const float HeadHeight = 2.6f;
        static readonly Vector3 Feet = new Vector3(0f, 0f, 4f);

        [Test]
        public void Straight_HeadHeight_HitsCapsule_AtTheContactPoint()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("capsule-head", speed: 10f, lifetime: 5f, radius: 0.1f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, Feet, Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });

                world.Tick(0.4f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(0f, seen[0].Point.x, 1e-3f);
                Assert.AreEqual(HeadHeight, seen[0].Point.y, 1e-3f);
                Assert.AreEqual(Feet.z - Radius, seen[0].Point.z, 1e-3f);
                Assert.Less(seen[0].Normal.z, -0.99f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Straight_HeadHeight_MissesTheOldSphere()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("capsule-head-sphere", speed: 10f, lifetime: 1f, radius: 0.1f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int hits = 0;
                world.Hit += _ => hits++;
                world.SetHitTarget(new TargetId(7), Feet + Vector3.up * 1.5f, Radius);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 8; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, hits);
            }
        }

        [Test]
        public void Straight_BesideCapsule_Misses_JustInsideHits()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("capsule-beside", speed: 10f, lifetime: 1f, radius: 0.1f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var beside = new TargetId(1);
                var inside = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(beside, Feet + Vector3.right * 0.75f, Radius, Height, 1.5f);
                world.SetHitTarget(inside, new Vector3(5f, 0f, 4f) + Vector3.right * 0.65f, Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1.6f, 0f),
                    Direction = Vector3.forward
                });
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(5f, 1.6f, 0f),
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 8; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(inside, seen[0].TargetId);
            }
        }

        [Test]
        public void Hitscan_HeadHeight_HitsCapsule()
        {
            Recipe recipe = DeliveryRecipe("capsule-hitscan", Delivery.Hitscan);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, Feet, Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(HeadHeight, seen[0].Point.y, 1e-3f);
            }
        }

        [Test]
        public void Beam_HeadHeight_PulseHitsCapsule()
        {
            Recipe recipe = DeliveryRecipe("capsule-beam", Delivery.Beam);
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, Feet, Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_BesideCapsule_Misses()
        {
            Recipe recipe = DeliveryRecipe("capsule-beam-miss", Delivery.Beam);
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int hits = 0;
                world.Hit += _ => hits++;
                world.SetHitTarget(new TargetId(7), Feet + Vector3.right * 0.9f, Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });
                world.Tick(0.25f);

                Assert.AreEqual(0, hits);
            }
        }

        [Test]
        public void Pierce_ThroughTwoCapsules_HitsBothInOrder()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "capsule-pierce",
                speed: 10f,
                lifetime: 5f,
                radius: 0.1f,
                onHit: new IModifier[] { new Pierce(2) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                var second = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(first, new Vector3(0f, 0f, 2f), Radius, Height, 1.5f);
                world.SetHitTarget(second, new Vector3(0f, 0f, 5f), Radius, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, HeadHeight, 0f),
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 8; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);
                Assert.AreEqual(second, seen[1].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void AreaEffect_ReachesTheUpperBody_SphereAndBesideDoNot()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "capsule-area",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                effectRadius: 1f,
                onHit: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var capsule = new TargetId(2);
                var sphere = new TargetId(3);
                var beside = new TargetId(4);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(capsule, new Vector3(0f, 0f, 3f), 0.4f, Height, 1.5f);
                world.SetHitTarget(sphere, new Vector3(0f, 1.5f, 3f), 0.4f);
                world.SetHitTarget(beside, new Vector3(1.5f, 0f, 3f), 0.4f, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 3f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(HasArea(seen, capsule));
                Assert.IsFalse(HasArea(seen, sphere));
                Assert.IsFalse(HasArea(seen, beside));
            }
        }

        [Test]
        public void GroundField_UsesTheAxisDistance()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "capsule-ground",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(inside, new Vector3(2.3f, 0f, 2f), 0.4f, Height, 1.5f);
                world.SetHitTarget(outside, new Vector3(2.5f, 0f, 2f), 0.4f, Height, 1.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(HasArea(seen, inside));
                Assert.IsFalse(HasArea(seen, outside));
            }
        }

        [Test]
        public void Homing_SteersTowardTheHomingPoint_NotTheCentre()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "capsule-home",
                speed: 10f,
                lifetime: 5f,
                radius: 0.1f,
                onTick: new IModifier[] { new Homing(90f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(4);
                world.SetHitTarget(id, new Vector3(0f, 0f, 10f), Radius, Height, 0.4f);
                ProjectileId shot = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, Height * 0.5f, 0f),
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetHomingTarget(shot, out HomingTarget locked));
                Assert.AreEqual(id, locked.Id);
                Assert.IsTrue(world.TryGetState(shot, out _, out Vector3 velocity, out _));
                Assert.Less(velocity.y, -0.5f);
                Assert.AreEqual(0f, velocity.x, 1e-3f);
            }
        }

        [Test]
        public void Homing_AcquiresByTheHomingPoint()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "capsule-acquire",
                speed: 10f,
                lifetime: 5f,
                radius: 0.1f,
                onTick: new IModifier[] { new Homing(90f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var low = new TargetId(1);
                var high = new TargetId(2);
                world.SetHitTarget(low, new Vector3(-3f, 0f, 8f), Radius, Height, 0.5f);
                world.SetHitTarget(high, new Vector3(3f, 0f, 8f), Radius, Height, 3f);
                ProjectileId shot = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 3f, 0f),
                    Direction = Vector3.forward
                });

                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(shot, out HomingTarget locked));
                Assert.AreEqual(high, locked.Id);
            }
        }

        [Test]
        public void ShortCapsule_IsASphereOnTheBottom_HomingPointClamped()
        {
            var catalog = new HitTargetCatalog();
            var id = new TargetId(1);
            catalog.SetCapsule(id, new Vector3(0f, 0f, 4f), 0.5f, 0.2f, 5f);

            Assert.IsTrue(catalog.TryGetPosition(id, out Vector3 aim));
            Assert.AreEqual(new Vector3(0f, 1f, 4f), aim);

            Assert.IsTrue(catalog.TrySweep(default, new Vector3(0f, 0.5f, 0f), Vector3.forward, 10f, 0.1f, out HitInfo hit, out float travel, out _));
            Assert.AreEqual(3.4f, travel, 1e-3f);
            Assert.AreEqual(3.5f, hit.Point.z, 1e-3f);

            Assert.IsFalse(catalog.TrySweep(default, new Vector3(0f, 1.2f, 0f), Vector3.forward, 10f, 0.1f, out _, out _, out _));
        }

        [Test]
        public void Sweep_DownTheAxis_HitsTheTopCap()
        {
            var catalog = new HitTargetCatalog();
            catalog.SetCapsule(new TargetId(1), Vector3.zero, 0.5f, 3f, 1.5f);

            Assert.IsTrue(catalog.TrySweep(default, new Vector3(0f, 10f, 0f), Vector3.down, 20f, 0.1f, out HitInfo hit, out float travel, out _));

            Assert.AreEqual(6.9f, travel, 1e-3f);
            Assert.AreEqual(3f, hit.Point.y, 1e-3f);
            Assert.Greater(hit.Normal.y, 0.99f);
        }

        [Test]
        public void Sweep_StartingInside_HitsAtZeroTravel()
        {
            var catalog = new HitTargetCatalog();
            catalog.SetCapsule(new TargetId(1), Vector3.zero, 0.5f, 3f, 1.5f);

            Assert.IsTrue(catalog.TrySweep(default, new Vector3(0.3f, 2.4f, 0f), Vector3.forward, 5f, 0.1f, out HitInfo hit, out float travel, out _));

            Assert.AreEqual(0f, travel);
            Assert.AreEqual(2.4f, hit.Point.y, 1e-3f);
            Assert.AreEqual(0.5f, hit.Point.x, 1e-3f);
        }

        [Test]
        public void AppendSphere_ContactIsOnTheCapsuleFacingTheCentre()
        {
            var catalog = new HitTargetCatalog();
            var id = new TargetId(1);
            catalog.SetCapsule(id, Vector3.zero, 0.5f, 3f, 1.5f);
            var results = new List<HitInfo>();

            catalog.AppendSphere(default, new Vector3(0f, 1.5f, 1f), 0.6f, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(id, results[0].TargetId);
            Assert.AreEqual(0.5f, results[0].Point.z, 1e-3f);
            Assert.AreEqual(1.5f, results[0].Point.y, 1e-3f);

            results.Clear();
            catalog.AppendSphere(default, new Vector3(0f, 1.5f, 1.2f), 0.6f, results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void AppendDisk_IgnoresHeight_UsesTheAxis()
        {
            var catalog = new HitTargetCatalog();
            catalog.SetCapsule(new TargetId(1), new Vector3(1.8f, 8f, 0f), 0.5f, 3f, 1.5f);
            var results = new List<HitInfo>();

            catalog.AppendDisk(default, Vector3.zero, 1.4f, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1.3f, results[0].Point.x, 1e-3f);
            Assert.AreEqual(8.5f, results[0].Point.y, 1e-3f);
        }

        [Test]
        public void ReplacingASphereWithACapsule_KeepsOneEntry_RadiusZeroRemoves()
        {
            var catalog = new HitTargetCatalog();
            var id = new TargetId(1);
            catalog.Set(id, new Vector3(0f, 1.5f, 0f), 0.6f);
            catalog.SetCapsule(id, Vector3.zero, 0.6f, Height, 2f);

            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryGetPosition(id, out Vector3 aim));
            Assert.AreEqual(new Vector3(0f, 2f, 0f), aim);

            catalog.SetCapsule(id, Vector3.zero, 0f, Height, 2f);
            Assert.AreEqual(0, catalog.Count);
            Assert.IsFalse(catalog.Contains(id));
        }

        static Recipe DeliveryRecipe(string id, Delivery delivery)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(id, speed: 20f, lifetime: 5f, radius: 0.1f);
            recipe.Delivery = delivery;
            return recipe;
        }

        static bool HasArea(List<HitInfo> seen, TargetId id)
        {
            for (int i = 0; i < seen.Count; i++)
            {
                if (seen[i].Source == HitSource.Area && seen[i].TargetId.Equals(id))
                    return true;
            }

            return false;
        }
    }
}
