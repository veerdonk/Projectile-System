using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class LifecycleEventTests
    {
        sealed class KeepAlive : IModifier
        {
            public void Apply(ModifierContext context)
            {
                context.CancelDespawn = true;
            }
        }

        sealed class Recorder
        {
            public readonly List<SpawnInfo> Spawns = new List<SpawnInfo>();
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public readonly List<ExpireInfo> Expires = new List<ExpireInfo>();
            public readonly List<HomingLockInfo> Locks = new List<HomingLockInfo>();
            public readonly List<string> Order = new List<string>();

            public Recorder(ProjectileWorld world)
            {
                world.Spawned += info => { Spawns.Add(info); Order.Add("spawn " + info.InstanceId); };
                world.Hit += info => { Hits.Add(info); Order.Add("hit " + info.InstanceId); };
                world.Expired += info => { Expires.Add(info); Order.Add("expire " + info.InstanceId); };
                world.HomingLocked += info => { Locks.Add(info); Order.Add("lock " + info.InstanceId); };
            }
        }

        static ProjectileId Fire(ProjectileWorld world, Recipe recipe, HomingTarget homing = default)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                HomingTarget = homing
            });
        }

        static Recipe HitscanRecipe(string id, IModifier[] onHit = null)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(id, speed: 10f, lifetime: 5f, onHit: onHit);
            recipe.Delivery = Delivery.Hitscan;
            return recipe;
        }

        [Test]
        public void Spawned_GameSpawn_HasNoParentAndGameCause()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-game");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);

                Assert.AreEqual(1, rec.Spawns.Count);
                Assert.AreEqual(id, rec.Spawns[0].InstanceId);
                Assert.AreEqual(default(ProjectileId), rec.Spawns[0].ParentId);
                Assert.AreEqual(SpawnCause.Game, rec.Spawns[0].Cause);
                Assert.AreEqual(Vector3.forward, rec.Spawns[0].Direction);
                Assert.AreSame(recipe, rec.Spawns[0].Recipe);
            }
        }

        [Test]
        public void Spawned_MultiplyChild_CarriesParentAndCause_AfterParentHit()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("life-mchild", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "life-mparent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(2, child) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 8);
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, parent);
                hits.QueueFor(id, HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(3, rec.Spawns.Count);
                for (int i = 1; i < 3; i++)
                {
                    Assert.AreEqual(id, rec.Spawns[i].ParentId);
                    Assert.AreEqual(SpawnCause.Multiply, rec.Spawns[i].Cause);
                }

                Assert.Less(rec.Order.IndexOf("spawn " + id), rec.Order.IndexOf("hit " + id));
                Assert.Less(rec.Order.IndexOf("hit " + id), rec.Order.IndexOf("spawn " + rec.Spawns[1].InstanceId));
                Assert.Less(rec.Order.IndexOf("hit " + id), rec.Order.IndexOf("expire " + id));
            }
        }

        [Test]
        public void Spawned_MulticastExtra_CarriesParentAndCause()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("life-cextra");
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "life-cparent",
                onSpawn: new IModifier[] { new Multicast(2, extra) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(extra, 4);
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, parent);

                Assert.AreEqual(2, rec.Spawns.Count);
                Assert.AreEqual(id, rec.Spawns[0].InstanceId);
                Assert.AreEqual(SpawnCause.Multicast, rec.Spawns[1].Cause);
                Assert.AreEqual(id, rec.Spawns[1].ParentId);
            }
        }

        [Test]
        public void Spawned_UnreachableArc_RaisesNothing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-arc", speed: 1f, gravity: 9.81f);
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = new Vector3(0f, 0f, 500f)
                });

                Assert.AreEqual(default(ProjectileId), id);
                Assert.AreEqual(0, rec.Order.Count);
            }
        }

        [Test]
        public void Spawned_Hitscan_ComesBeforeHitAndExpired()
        {
            Recipe recipe = HitscanRecipe("life-hitscan");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                world.SetHitTarget(new TargetId(7), new Vector3(0f, 0f, 3f), 0.5f);
                Fire(world, recipe);

                Assert.AreEqual(3, rec.Order.Count);
                StringAssert.StartsWith("spawn", rec.Order[0]);
                StringAssert.StartsWith("hit", rec.Order[1]);
                StringAssert.StartsWith("expire", rec.Order[2]);
                Assert.AreEqual(ExpireReason.Hit, rec.Expires[0].Reason);
            }
        }

        [Test]
        public void ExpireReason_Lifetime_HasLastPose()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-expire", speed: 10f, lifetime: 0.25f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                Fire(world, recipe);
                for (int i = 0; i < 6 && rec.Expires.Count == 0; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, rec.Expires.Count);
                Assert.AreEqual(ExpireReason.Lifetime, rec.Expires[0].Reason);
                Assert.Greater(rec.Expires[0].Position.z, 1f);
                Assert.AreEqual(Vector3.forward, rec.Expires[0].Direction);
            }
        }

        [Test]
        public void ExpireReason_HitDespawn_IsHitAndOutcomeEnded()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-hit", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(HitOutcome.Ended, rec.Hits[0].Outcome);
                Assert.AreEqual(ExpireReason.Hit, rec.Expires[0].Reason);
                Assert.Less(rec.Order.IndexOf("hit " + id), rec.Order.IndexOf("expire " + id));
            }
        }

        [Test]
        public void ExpireReason_StrongProtection_IsAbsorbedWithNoHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-strong", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1), ProtectionLevel.Strong);
                world.Tick(0.1f);

                Assert.AreEqual(0, rec.Hits.Count);
                Assert.AreEqual(ExpireReason.Absorbed, rec.Expires[0].Reason);
            }
        }

        [Test]
        public void ExpireReason_GameDespawn_IsDespawned()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("life-despawn");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                world.Despawn(id);

                Assert.AreEqual(ExpireReason.Despawned, rec.Expires[0].Reason);
            }
        }

        [Test]
        public void ExpireReason_HitscanMiss_IsHitscanEnd()
        {
            Recipe recipe = HitscanRecipe("life-hitscan-miss");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                Fire(world, recipe);

                Assert.AreEqual(0, rec.Hits.Count);
                Assert.AreEqual(ExpireReason.HitscanEnd, rec.Expires[0].Reason);
            }
        }

        [Test]
        public void HitOutcome_Pierce_IsPiercedThenEnded()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "life-pierce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);
                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(2));
                world.Tick(0.1f);

                Assert.AreEqual(HitOutcome.Pierced, rec.Hits[0].Outcome);
                Assert.AreEqual(HitOutcome.Ended, rec.Hits[1].Outcome);
            }
        }

        [Test]
        public void HitOutcome_Bounce_IsBouncedThenEnded()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "life-bounce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                hits.QueueFor(id, HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Tick(0.1f);
                hits.QueueFor(id, HitKind.World, new Vector3(0f, 0f, -2f), Vector3.forward);
                world.Tick(0.1f);

                Assert.AreEqual(HitOutcome.Bounced, rec.Hits[0].Outcome);
                Assert.AreEqual(HitOutcome.Ended, rec.Hits[1].Outcome);
            }
        }

        [Test]
        public void HitOutcome_CancelDespawn_IsContinued()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "life-continue",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new KeepAlive() });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                ProjectileId id = Fire(world, recipe);
                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(HitOutcome.Continued, rec.Hits[0].Outcome);
                Assert.AreEqual(0, rec.Expires.Count);
            }
        }

        [Test]
        public void HomingLocked_AssignedAtSpawn_RaisedOnceAfterSpawned()
        {
            Recipe recipe = HomingRecipe("life-home-assigned");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                var target = new TargetId(1);
                world.SetHomingCandidate(target, new Vector3(0f, 0f, 8f));
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(target));
                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(1, rec.Locks.Count);
                Assert.AreEqual(HomingLockKind.Assigned, rec.Locks[0].Kind);
                Assert.AreEqual(target, rec.Locks[0].TargetId);
                Assert.AreEqual(id, rec.Locks[0].InstanceId);
                Assert.AreEqual("spawn " + id, rec.Order[0]);
                Assert.AreEqual("lock " + id, rec.Order[1]);
            }
        }

        [Test]
        public void HomingLocked_Acquired_RaisedOnce_NotWhileKept()
        {
            Recipe recipe = HomingRecipe("life-home-acquire");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                world.SetHomingCandidate(new TargetId(1), new Vector3(0f, 0f, 8f));
                Fire(world, recipe);
                Assert.AreEqual(0, rec.Locks.Count);

                world.Tick(0.01f);
                world.Tick(0.01f);
                world.Tick(0.01f);

                Assert.AreEqual(1, rec.Locks.Count);
                Assert.AreEqual(HomingLockKind.Acquired, rec.Locks[0].Kind);
                Assert.AreEqual(new TargetId(1), rec.Locks[0].TargetId);
            }
        }

        [Test]
        public void HomingLocked_ReacquireAfterTargetHit_RaisesAgain()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "life-home-reacquire",
                speed: 20f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(90f) },
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var rec = new Recorder(world);
                world.SetHomingCandidate(new TargetId(1), new Vector3(0f, 0f, 8f));
                ProjectileId id = Fire(world, recipe);
                world.Tick(0.1f);
                Assert.AreEqual(1, rec.Locks.Count);

                hits.QueueFor(id, HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);
                world.SetHomingCandidate(new TargetId(2), new Vector3(0f, 0f, 20f));
                world.Tick(0.1f);

                Assert.AreEqual(2, rec.Locks.Count);
                Assert.AreEqual(HomingLockKind.Acquired, rec.Locks[1].Kind);
                Assert.AreEqual(new TargetId(2), rec.Locks[1].TargetId);
            }
        }

        static Recipe HomingRecipe(string id)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(100f) });
        }
    }
}
