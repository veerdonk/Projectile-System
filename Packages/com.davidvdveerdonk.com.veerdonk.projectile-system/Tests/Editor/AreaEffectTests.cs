using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class AreaEffectTests
    {
        [Test]
        public void OnHit_Splash_HitsNearbyTarget_AndDirectContact()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-onhit",
                speed: 20f,
                lifetime: 5f,
                damage: 1f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var nearby = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                hits.AddOverlapTarget(nearby, new Vector3(2f, 0f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(contact, seen[0].TargetId);
                Assert.AreEqual(1f, seen[0].Damage, 1e-4f);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(contact, seen[1].TargetId);
                Assert.AreEqual(3f, seen[1].Damage, 1e-4f);
                Assert.AreEqual(HitSource.Area, seen[2].Source);
                Assert.AreEqual(nearby, seen[2].TargetId);
                Assert.AreEqual(3f, seen[2].Damage, 1e-4f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void OnHit_AreaOnly_SuppressesDirectHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-only",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f, includeDirectHit: false) });
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

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(contact, seen[0].TargetId);
                Assert.AreEqual(3f, seen[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void OnTravel_Burst_HitsTargetAtSpawn_BeforeMove()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-travel",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onTravel: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var muzzle = new TargetId(8);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                hits.AddOverlapTarget(muzzle, new Vector3(2f, 0f, 0f));

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
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out _, out _));
                Assert.AreEqual(0f, position.z, 1e-4f);
            }
        }

        [Test]
        public void OnInterval_Aura_HitsBesidePath_ThenMissesAfterLeaving()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-aura",
                speed: 10f,
                lifetime: 5f,
                effectRadius: 4f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var beside = new TargetId(4);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                hits.AddOverlapTarget(beside, new Vector3(2f, 0f, 5f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.25f);
                Assert.AreEqual(0, seen.Count);

                world.Tick(0.25f);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(3f, seen[0].Damage, 1e-4f);

                world.Tick(0.25f);
                Assert.AreEqual(1, seen.Count);

                world.Tick(0.25f);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[1].Source);

                world.Tick(0.25f);
                world.Tick(0.25f);
                Assert.AreEqual(2, seen.Count);
            }
        }

        [Test]
        public void EffectRadius0_HitsNobody()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-zero",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 0f,
                onHit: new IModifier[] { new AreaEffect(3f) });
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

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(1f, seen[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void WorldOverlap_IsIgnored_TargetInSphereIsHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-world-ignore",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f) });
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

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.World, point, Vector3.back);
                hits.AddOverlapWorld(point);
                hits.AddOverlapTarget(contact, new Vector3(1f, 0f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(contact, seen[1].TargetId);
                Assert.AreEqual(3f, seen[1].Damage, 1e-4f);
            }
        }

        [Test]
        public void AreaStatus_PerTarget_SuppressesDirectStatus()
        {
            var areaEffect = new DummyStatusEffect();
            var directEffect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-status",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[]
                {
                    new ApplyStatus(directEffect),
                    new AreaEffect(3f, areaEffect, includeDirectStatus: false)
                });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var nearby = new TargetId(2);
                var hitsSeen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                world.Hit += info => hitsSeen.Add(info);
                world.StatusApplied += info => statuses.Add(info);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                hits.AddOverlapTarget(contact, point);
                hits.AddOverlapTarget(nearby, new Vector3(2f, 0f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(3, hitsSeen.Count);
                Assert.AreEqual(HitSource.Direct, hitsSeen[0].Source);
                Assert.AreEqual(2, statuses.Count);
                Assert.AreEqual(contact, statuses[0].TargetId);
                Assert.AreSame(areaEffect, statuses[0].Effect);
                Assert.AreEqual(nearby, statuses[1].TargetId);
                Assert.AreSame(areaEffect, statuses[1].Effect);
            }
        }
    }
}
