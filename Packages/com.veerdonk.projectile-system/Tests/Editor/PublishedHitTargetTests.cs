using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PublishedHitTargetTests
    {
        [Test]
        public void StraightShot_PublishedSphere_RaisesTargetHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("published-straight", speed: 10f, lifetime: 5f, radius: 0.25f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.4f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(ProtectionLevel.None, seen[0].Protection);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.Less(seen[0].Normal.z, -0.9f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void UnmarkedWall_CloserThanPublishedVolume_IsWorld()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("published-wall", speed: 10f, lifetime: 5f, radius: 0.25f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(7), new Vector3(0f, 0f, 6f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Tick(1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(default(TargetId), seen[0].TargetId);
            }
        }

        [Test]
        public void PublishedVolume_CloserThanWall_IsTarget()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("published-nearer", speed: 10f, lifetime: 5f, radius: 0.25f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(4);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 8f), Vector3.back);
                world.Tick(1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(ProtectionLevel.None, seen[0].Protection);
            }
        }

        [Test]
        public void Pierce2_FirstTwoPublishedVolumesContinue_ThirdDespawns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-pierce",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(2) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                var second = new TargetId(2);
                var third = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(first, new Vector3(0f, 0f, 2f), 0.5f);
                world.SetHitTarget(second, new Vector3(0f, 0f, 5f), 0.5f);
                world.SetHitTarget(third, new Vector3(0f, 0f, 8f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.3f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);

                world.Tick(0.3f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(second, seen[1].TargetId);

                world.Tick(0.3f);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(third, seen[2].TargetId);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitKind.Target, seen[1].Kind);
                Assert.AreEqual(HitKind.Target, seen[2].Kind);
            }
        }

        [Test]
        public void Bounce1_ReflectsOffPublishedVolume_AndRaisesHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-bounce",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(9);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, new Vector3(0f, 0f, 2f), 0.5f);
                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.3f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.IsTrue(world.TryGetState(instance, out _, out Vector3 velocity, out _));
                Assert.AreEqual(0f, velocity.x, 0.05f);
                Assert.AreEqual(0f, velocity.y, 0.05f);
                Assert.Less(velocity.z, -9f);
            }
        }

        [Test]
        public void OnHit_AreaEffect_IncludesPublishedVolume_MissesOutside()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-area",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                damage: 1f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(inside, new Vector3(0f, 0f, 5.4f), 0.6f);
                world.SetHitTarget(outside, new Vector3(0f, 0f, 6.5f), 0.4f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.Target, point, Vector3.back, contact);
                world.Tick(0.1f);

                Assert.IsTrue(HasArea(seen, inside));
                Assert.IsFalse(HasArea(seen, outside));
                Assert.AreEqual(4f, AreaDamage(seen, inside), 1e-4f);
            }
        }

        [Test]
        public void GroundField_Pulse_IncludesPublishedDisk_MissesOutsideXZ()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-ground",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(inside, new Vector3(1.8f, 8f, 2f), 0.5f);
                world.SetHitTarget(outside, new Vector3(3.2f, 0f, 2f), 0.3f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 1f, 2f), Vector3.back, contact);
                world.Tick(0.1f);

                Assert.IsTrue(HasArea(seen, inside));
                Assert.IsFalse(HasArea(seen, outside));
                Assert.AreEqual(3f, AreaDamage(seen, inside), 1e-4f);
            }
        }

        [Test]
        public void RadiusZeroOrNegative_DoesNotHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("published-zero", speed: 10f, lifetime: 5f, radius: 0.25f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int hits = 0;
                world.Hit += _ => hits++;
                var id = new TargetId(7);
                world.SetHitTarget(id, new Vector3(0f, 0f, 2f), 0.5f);
                world.SetHitTarget(id, new Vector3(0f, 0f, 2f), 0f);
                world.SetHitTarget(new TargetId(8), new Vector3(0f, 0f, 2f), -1f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);

                Assert.AreEqual(0, hits);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void RemoveHitTarget_ThenTickDoesNotHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("published-remove", speed: 10f, lifetime: 5f, radius: 0.25f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int hits = 0;
                world.Hit += _ => hits++;
                var id = new TargetId(7);
                world.SetHitTarget(id, new Vector3(0f, 0f, 2f), 0.5f);
                world.RemoveHitTarget(id);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);

                Assert.AreEqual(0, hits);
                Assert.AreEqual(1, world.ActiveCount);
            }
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

        static float AreaDamage(List<HitInfo> seen, TargetId id)
        {
            for (int i = 0; i < seen.Count; i++)
            {
                if (seen[i].Source == HitSource.Area && seen[i].TargetId.Equals(id))
                    return seen[i].Damage;
            }

            return -1f;
        }
    }
}
