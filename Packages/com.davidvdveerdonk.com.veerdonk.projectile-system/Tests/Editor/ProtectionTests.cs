using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class ProtectionTests
    {
        [Test]
        public void Basic_Bounce1_Reflects_RaisesProtectedHit_NoStatusApplied()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "basic-bounce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1), new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                int statusCount = 0;
                world.Hit += info => seen.Add(info);
                world.StatusApplied += _ => statusCount++;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                Vector3 normal = Vector3.back;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1), ProtectionLevel.Basic);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(ProtectionLevel.Basic, seen[0].Protection);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(0, statusCount);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out int bounce));
                Assert.AreEqual(0, bounce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Vector3 expected = Vector3.Reflect(Vector3.forward * recipe.Speed, normal.normalized);
                Assert.AreEqual(expected.x, velocity.x, 0.0001f);
                Assert.AreEqual(expected.y, velocity.y, 0.0001f);
                Assert.AreEqual(expected.z, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Basic_Pierce1_Continues_ThenUnprotectedTargetIsUnchanged()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "basic-pierce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                world.Hit += info => seen.Add(info);
                world.StatusApplied += info => statuses.Add(info);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1), ProtectionLevel.Basic);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(ProtectionLevel.Basic, seen[0].Protection);
                Assert.AreEqual(0, statuses.Count);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(2));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(ProtectionLevel.None, seen[1].Protection);
                Assert.AreEqual(HitKind.Target, seen[1].Kind);
                Assert.AreEqual(new TargetId(2), seen[1].TargetId);
                Assert.AreEqual(1, statuses.Count);
                Assert.AreEqual(new TargetId(2), statuses[0].TargetId);
            }
        }

        [Test]
        public void Basic_Pierce1_ConsumesOnWorldHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "basic-pierce-world",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back, default, ProtectionLevel.Basic);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(ProtectionLevel.Basic, seen[0].Protection);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);
            }
        }

        [Test]
        public void Strong_EatsBouncingShot_NoHit_NoOnHitOrOnExpireChildren()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("protection-child", speed: 8f, lifetime: 2f);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "strong-eat",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[]
                {
                    new Bounce(1),
                    new AreaEffect(3f),
                    new Multiply(2, child)
                },
                onExpire: new IModifier[] { new Multiply(1, child) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 8);
            using (world)
            {
                world.Prewarm(child, 4);
                int availableBefore = world.AvailableCount;
                var seen = new List<HitInfo>();
                int statusCount = 0;
                ExpireInfo? expired = null;
                world.Hit += info => seen.Add(info);
                world.StatusApplied += _ => statusCount++;
                world.Expired += info => expired = info;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.World, point, Vector3.back, default, ProtectionLevel.Strong);
                hits.AddOverlapTarget(new TargetId(9), new Vector3(1f, 0f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(0, statusCount);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
            }
        }

        [Test]
        public void UnprotectedTarget_StillRaisesHitAndStatus()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "unprotected",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                HitInfo? hit = null;
                StatusInfo? status = null;
                world.Hit += info => hit = info;
                world.StatusApplied += info => status = info;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(3));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(ProtectionLevel.None, hit.Value.Protection);
                Assert.AreEqual(HitKind.Target, hit.Value.Kind);
                Assert.IsTrue(status.HasValue);
                Assert.AreEqual(new TargetId(3), status.Value.TargetId);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void AreaOverlap_SkipsStrong_IncludesBasicWithProtection()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-protection",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var contact = new TargetId(1);
                var basic = new TargetId(2);
                var strong = new TargetId(3);
                var open = new TargetId(4);
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
                hits.AddOverlapTarget(basic, new Vector3(1f, 0f, 2f), ProtectionLevel.Basic);
                hits.AddOverlapTarget(strong, new Vector3(-1f, 0f, 2f), ProtectionLevel.Strong);
                hits.AddOverlapTarget(open, new Vector3(0f, 1f, 2f));
                world.Tick(0.1f);

                Assert.AreEqual(4, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(ProtectionLevel.None, seen[0].Protection);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(contact, seen[1].TargetId);
                Assert.AreEqual(ProtectionLevel.None, seen[1].Protection);
                Assert.AreEqual(HitSource.Area, seen[2].Source);
                Assert.AreEqual(basic, seen[2].TargetId);
                Assert.AreEqual(ProtectionLevel.Basic, seen[2].Protection);
                Assert.AreEqual(HitSource.Area, seen[3].Source);
                Assert.AreEqual(open, seen[3].TargetId);
                Assert.AreEqual(ProtectionLevel.None, seen[3].Protection);
            }
        }
    }
}
