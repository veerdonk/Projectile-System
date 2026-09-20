using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class GroundFieldTests
    {
        [Test]
        public void OnHit_Plant_PulsesImmediately_AndAgainAtInterval()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-onhit",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var nearby = new TargetId(2);
                var seen = new List<HitInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.Expired += _ => expired++;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                hits.AddOverlapTarget(nearby, new Vector3(1.5f, 4f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(1, expired);
                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(1f, seen[0].Damage, 1e-4f);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(contact, seen[1].TargetId);
                Assert.AreEqual(3f, seen[1].Damage, 1e-4f);
                Assert.AreEqual(id, seen[1].InstanceId);
                Assert.AreEqual(HitSource.Area, seen[2].Source);
                Assert.AreEqual(nearby, seen[2].TargetId);
                Assert.AreEqual(3f, seen[2].Damage, 1e-4f);

                world.Tick(0.5f);
                Assert.AreEqual(5, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[3].Source);
                Assert.AreEqual(contact, seen[3].TargetId);
                Assert.AreEqual(id, seen[3].InstanceId);
                Assert.AreEqual(nearby, seen[4].TargetId);
                Assert.AreEqual(1, expired);
            }
        }

        [Test]
        public void OutsideXZ_IsMissed_EvenWhenSphereWouldCoverHighTarget()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-disk",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var highInside = new TargetId(2);
                var beside = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = Vector3.zero;
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                hits.AddOverlapTarget(highInside, new Vector3(1.2f, 1.6f, 0f));
                hits.AddOverlapTarget(beside, new Vector3(2.1f, 0.2f, 0f));
                world.Tick(0.1f);

                var area = new List<HitInfo>();
                for (int i = 0; i < seen.Count; i++)
                {
                    if (seen[i].Source == HitSource.Area)
                        area.Add(seen[i]);
                }

                Assert.AreEqual(2, area.Count);
                Assert.AreEqual(contact, area[0].TargetId);
                Assert.AreEqual(highInside, area[1].TargetId);
            }
        }

        [Test]
        public void AfterLifetime_NoFurtherHits_AndNoExpiredForField()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-lifetime",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 1f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var seen = new List<HitInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.Expired += _ => expired++;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.zero, Vector3.back, contact);
                hits.AddOverlapTarget(contact, Vector3.zero);
                world.Tick(0.1f);
                int afterPlant = CountArea(seen);
                Assert.AreEqual(1, afterPlant);
                Assert.AreEqual(1, expired);

                world.Tick(0.5f);
                Assert.AreEqual(2, CountArea(seen));

                world.Tick(0.5f);
                Assert.AreEqual(2, CountArea(seen));
                Assert.AreEqual(1, expired);

                world.Tick(0.5f);
                Assert.AreEqual(2, CountArea(seen));
                Assert.AreEqual(1, expired);
            }
        }

        [Test]
        public void Status_RaisesPerPulse()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-status",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, effect, 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var nearby = new TargetId(2);
                var statuses = new List<StatusInfo>();
                world.StatusApplied += info => statuses.Add(info);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.zero, Vector3.back, contact);
                hits.AddOverlapTarget(contact, Vector3.zero);
                hits.AddOverlapTarget(nearby, new Vector3(1f, 0f, 0f));
                world.Tick(0.1f);

                Assert.AreEqual(2, statuses.Count);
                Assert.AreEqual(contact, statuses[0].TargetId);
                Assert.AreSame(effect, statuses[0].Effect);
                Assert.AreEqual(id, statuses[0].InstanceId);
                Assert.AreEqual(nearby, statuses[1].TargetId);

                world.Tick(0.5f);
                Assert.AreEqual(4, statuses.Count);
                Assert.AreSame(effect, statuses[2].Effect);
                Assert.AreEqual(id, statuses[2].InstanceId);
            }
        }

        [Test]
        public void OnExpire_PlantsAtExpirePosition()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-expire",
                speed: 10f,
                lifetime: 1f,
                onExpire: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var atExpire = new TargetId(4);
                var dummy = new TargetId(5);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                hits.AddOverlapTarget(atExpire, new Vector3(0f, 0f, 5f));
                hits.AddOverlapTarget(dummy, new Vector3(0f, 0f, 8f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.AreEqual(0, seen.Count);

                world.Tick(0.5f);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(atExpire, seen[0].TargetId);
                Assert.AreEqual(3f, seen[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void OnTravel_PlantsAtSpawn()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-travel",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var muzzle = new TargetId(8);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                hits.AddOverlapTarget(muzzle, new Vector3(1f, 3f, 0f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(muzzle, seen[0].TargetId);
                Assert.AreEqual(3f, seen[0].Damage, 1e-4f);
                Assert.AreEqual(id, seen[0].InstanceId);
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out _, out _));
                Assert.AreEqual(0f, position.z, 1e-4f);
            }
        }

        [Test]
        public void Strong_IsSkipped_BasicCarriesProtection()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-protection",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(3f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var basic = new TargetId(2);
                var strong = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.zero, Vector3.back, contact);
                hits.AddOverlapTarget(contact, Vector3.zero);
                hits.AddOverlapTarget(basic, new Vector3(1f, 0f, 0f), ProtectionLevel.Basic);
                hits.AddOverlapTarget(strong, new Vector3(-1f, 0f, 0f), ProtectionLevel.Strong);
                world.Tick(0.1f);

                var area = new List<HitInfo>();
                for (int i = 0; i < seen.Count; i++)
                {
                    if (seen[i].Source == HitSource.Area)
                        area.Add(seen[i]);
                }

                Assert.AreEqual(2, area.Count);
                Assert.AreEqual(contact, area[0].TargetId);
                Assert.AreEqual(ProtectionLevel.None, area[0].Protection);
                Assert.AreEqual(basic, area[1].TargetId);
                Assert.AreEqual(ProtectionLevel.Basic, area[1].Protection);
            }
        }

        [Test]
        public void RadiusZero_OrLifetimeZero_PlantsNothing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-noop",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[]
                {
                    new GroundField(0f, 2f, 3f, intervalSeconds: 0.5f),
                    new GroundField(2f, 0f, 3f, intervalSeconds: 0.5f)
                });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.zero, Vector3.back, contact);
                hits.AddOverlapTarget(contact, Vector3.zero);
                world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);

                world.Tick(0.5f);
                Assert.AreEqual(1, seen.Count);
            }
        }

        [Test]
        public void OnHitThenOnExpire_PlantTwoDisks()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-two",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) },
                onExpire: new IModifier[] { new GroundField(2f, 2f, 4f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Vector3.zero, Vector3.back, contact);
                hits.AddOverlapTarget(contact, Vector3.zero);
                world.Tick(0.1f);

                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(3f, seen[1].Damage, 1e-4f);
                Assert.AreEqual(HitSource.Area, seen[2].Source);
                Assert.AreEqual(4f, seen[2].Damage, 1e-4f);
            }
        }

        [Test]
        public void CreateViewsFalse_DoesNotCallGroundFieldFactory()
        {
            var factory = new CountingGroundFieldViewFactory();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-noview",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false, factory);
            using (world)
            {
                world.Prewarm(recipe, 1);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Assert.AreEqual(0, factory.CreateCount);
            }
        }

        [Test]
        public void NullPrefab_FallsBackToPrimitiveDisk()
        {
            var factory = new PrefabGroundFieldViewFactory(null);
            ProjectileView view = factory.Create(2f, null);
            try
            {
                Assert.IsTrue(view.ScaleToRadius);
                Assert.AreEqual(4f, view.transform.localScale.x, 1e-4f);
                Assert.AreEqual(4f, view.transform.localScale.z, 1e-4f);
            }
            finally
            {
                if (view != null)
                    Object.DestroyImmediate(view.gameObject);
            }
        }

        static int CountArea(List<HitInfo> seen)
        {
            int count = 0;
            for (int i = 0; i < seen.Count; i++)
            {
                if (seen[i].Source == HitSource.Area)
                    count++;
            }

            return count;
        }

        sealed class CountingGroundFieldViewFactory : IGroundFieldViewFactory
        {
            public int CreateCount;

            public ProjectileView Create(float radius, Transform poolParent)
            {
                CreateCount++;
                return null;
            }
        }
    }
}
