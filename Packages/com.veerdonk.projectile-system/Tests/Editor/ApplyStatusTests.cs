using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class ApplyStatusTests
    {
        [Test]
        public void TargetHit_RaisesStatusApplied_WithDummyEffect_ThenDespawns()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "apply-status",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new ApplyStatus(effect) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                int availableBefore = world.AvailableCount;
                var targetId = new TargetId(7);
                var order = new List<string>();
                HitInfo? hit = null;
                StatusInfo? status = null;
                ExpireInfo? expired = null;
                world.Hit += info =>
                {
                    hit = info;
                    order.Add("hit");
                };
                world.StatusApplied += info =>
                {
                    status = info;
                    order.Add("status");
                };
                world.Expired += info =>
                {
                    expired = info;
                    order.Add("expired");
                };

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, targetId);
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.IsTrue(status.HasValue);
                Assert.AreEqual(id, status.Value.InstanceId);
                Assert.AreEqual(targetId, status.Value.TargetId);
                Assert.AreSame(effect, status.Value.Effect);
                Assert.AreEqual(DummyStatusEffect.EffectId, status.Value.Effect.Id);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(id, expired.Value.InstanceId);
                CollectionAssert.AreEqual(new[] { "hit", "status", "expired" }, order);
            }
        }

        [Test]
        public void TwoApplyStatus_OnTargetHit_RaisesStatusAppliedInListOrder()
        {
            var first = new DummyStatusEffect();
            var second = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "apply-status-two",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new ApplyStatus(first), new ApplyStatus(second) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                var targetId = new TargetId(7);
                var seen = new List<StatusInfo>();
                var order = new List<string>();
                world.Hit += _ => order.Add("hit");
                world.StatusApplied += info =>
                {
                    seen.Add(info);
                    order.Add("status");
                };
                world.Expired += _ => order.Add("expired");

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, targetId);
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(id, seen[0].InstanceId);
                Assert.AreEqual(targetId, seen[0].TargetId);
                Assert.AreSame(first, seen[0].Effect);
                Assert.AreEqual(id, seen[1].InstanceId);
                Assert.AreEqual(targetId, seen[1].TargetId);
                Assert.AreSame(second, seen[1].Effect);
                CollectionAssert.AreEqual(new[] { "hit", "status", "status", "expired" }, order);
            }
        }

        [Test]
        public void WorldHit_DoesNotRaiseStatusApplied()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "apply-status-world",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int statusCount = 0;
                world.StatusApplied += _ => statusCount++;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.World, new Vector3(0f, 0f, 1f), Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(0, statusCount);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Pierce1_ApplyStatus_RaisesOncePerTargetHit_IncludingKillingHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-status",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                var seen = new List<StatusInfo>();
                world.StatusApplied += info => seen.Add(info);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                var first = new TargetId(1);
                var second = new TargetId(2);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, first);
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(id, seen[0].InstanceId);
                Assert.AreEqual(first, seen[0].TargetId);
                Assert.AreEqual(DummyStatusEffect.EffectId, seen[0].Effect.Id);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, second);
                world.Tick(0.1f);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(id, seen[1].InstanceId);
                Assert.AreEqual(second, seen[1].TargetId);
                Assert.AreEqual(DummyStatusEffect.EffectId, seen[1].Effect.Id);
            }
        }

        [Test]
        public void Pierce1_TwoApplyStatus_RaisesEachEffectPerTargetHit()
        {
            var first = new DummyStatusEffect();
            var second = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-status-two",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new ApplyStatus(first), new ApplyStatus(second) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe, prewarm: 4);
            using (world)
            {
                var seen = new List<StatusInfo>();
                world.StatusApplied += info => seen.Add(info);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                var dummy1 = new TargetId(1);
                var dummy2 = new TargetId(2);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, dummy1);
                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(2, seen.Count);
                Assert.AreSame(first, seen[0].Effect);
                Assert.AreEqual(dummy1, seen[0].TargetId);
                Assert.AreSame(second, seen[1].Effect);
                Assert.AreEqual(dummy1, seen[1].TargetId);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, dummy2);
                world.Tick(0.1f);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(4, seen.Count);
                Assert.AreSame(first, seen[2].Effect);
                Assert.AreEqual(dummy2, seen[2].TargetId);
                Assert.AreSame(second, seen[3].Effect);
                Assert.AreEqual(dummy2, seen[3].TargetId);
                Assert.AreEqual(id, seen[3].InstanceId);
            }
        }
    }
}
