using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HotPathWorldTests
    {
        sealed class RequestChildren : IModifier
        {
            readonly Recipe child;
            readonly int count;

            public RequestChildren(Recipe child, int count)
            {
                this.child = child;
                this.count = count;
            }

            public void Apply(ModifierContext context)
            {
                for (int i = 0; i < count; i++)
                    context.RequestSpawn(child, context.Position, Vector3.forward);
            }
        }

        struct Body
        {
            public TargetId Id;
            public Vector3 Position;
            public ProtectionLevel Protection;
        }

        [Test]
        public void AreaBurst_SeveralHundredDuplicateOverlaps_OneHitEachInFirstSeenOrder()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "hot-area-burst",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onTravel: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                List<Body> bodies = RandomBodies(new System.Random(3), 400, 2.5f, withHeight: true);
                AddBodies(hits, bodies);
                var seen = new List<TargetId>();
                world.Hit += info =>
                {
                    Assert.AreEqual(HitSource.Area, info.Source);
                    seen.Add(info.TargetId);
                };

                world.Spawn(new SpawnRequest { Recipe = recipe, Position = Vector3.zero, Direction = Vector3.forward });

                CollectionAssert.AreEqual(FirstSeen(bodies), seen);
            }
        }

        [Test]
        public void GroundFieldPulse_SeveralHundredDuplicateOverlaps_OneHitEachInFirstSeenOrder()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "hot-field-pulse",
                speed: 0f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(3f, 1f, 2f, intervalSeconds: 0.25f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                List<Body> bodies = RandomBodies(new System.Random(4), 400, 2.5f, withHeight: false);
                AddBodies(hits, bodies);
                var seen = new List<TargetId>();
                var pulses = new List<GroundFieldPulseInfo>();
                world.Hit += info =>
                {
                    Assert.AreEqual(HitSource.Area, info.Source);
                    seen.Add(info.TargetId);
                };
                world.GroundFieldPulsed += pulses.Add;

                world.Spawn(new SpawnRequest { Recipe = recipe, Position = Vector3.zero, Direction = Vector3.forward });

                List<TargetId> expected = FirstSeen(bodies);
                Assert.AreEqual(1, pulses.Count);
                Assert.AreEqual(expected.Count, pulses[0].TargetsHit);
                CollectionAssert.AreEqual(expected, seen);

                seen.Clear();
                world.Tick(0.3f);
                Assert.AreEqual(2, pulses.Count);
                Assert.AreEqual(expected.Count, pulses[1].TargetsHit);
                CollectionAssert.AreEqual(expected, seen);
            }
        }

        [Test]
        public void AreaOccupancy_ManyOccupants_EnterAndExitInTheListOrder()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "hot-area-occupancy",
                speed: 0f,
                lifetime: 30f,
                onTick: new IModifier[] { new AreaEffect(1f, 3f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<string>();
                world.Hit += info => seen.Add(info.Source + " " + info.TargetId);
                world.Spawn(new SpawnRequest { Recipe = recipe, Position = Vector3.zero, Direction = Vector3.forward });

                var random = new System.Random(8);
                var previous = new List<TargetId>();
                for (int tick = 0; tick < 12; tick++)
                {
                    List<Body> bodies = RandomBodies(random, 300, 2.5f, withHeight: true);
                    hits.ClearOverlaps();
                    AddBodies(hits, bodies);
                    seen.Clear();

                    world.Tick(0.1f);

                    CollectionAssert.AreEqual(ExpectedExchange(previous, FirstSeen(bodies)), seen, "tick " + tick);
                }
            }
        }

        [Test]
        public void GroundFieldOccupancy_ManyOccupants_EnterAndExitInTheListOrder()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "hot-field-occupancy",
                speed: 0f,
                lifetime: 30f,
                onTravel: new IModifier[] { new GroundField(3f, 20f, 1f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<string>();
                world.Hit += info => seen.Add(info.Source + " " + info.TargetId);
                var random = new System.Random(9);
                var previous = new List<TargetId>();

                List<Body> first = RandomBodies(random, 300, 2.5f, withHeight: false);
                AddBodies(hits, first);
                world.Spawn(new SpawnRequest { Recipe = recipe, Position = Vector3.zero, Direction = Vector3.forward });
                CollectionAssert.AreEqual(ExpectedExchange(previous, FirstSeen(first)), seen, "plant");

                for (int tick = 0; tick < 12; tick++)
                {
                    List<Body> bodies = RandomBodies(random, 300, 2.5f, withHeight: false);
                    hits.ClearOverlaps();
                    AddBodies(hits, bodies);
                    seen.Clear();

                    world.Tick(0.1f);

                    CollectionAssert.AreEqual(ExpectedExchange(previous, FirstSeen(bodies)), seen, "tick " + tick);
                }
            }
        }

        [Test]
        public void OccupancyExchange_MatchesTheListScan()
        {
            var random = new System.Random(21);
            var previous = new TargetSet();
            var current = new TargetSet();
            var referencePrevious = new List<TargetId>();
            var overlaps = new List<HitInfo>();
            var status = new StubStatus();
            var actual = new List<string>();
            var expected = new List<string>();

            for (int frame = 0; frame < 40; frame++)
            {
                overlaps.Clear();
                int count = random.Next(0, 500);
                for (int i = 0; i < count; i++)
                {
                    overlaps.Add(new HitInfo
                    {
                        Kind = random.Next(10) == 0 ? HitKind.World : HitKind.Target,
                        Protection = (ProtectionLevel)random.Next(3),
                        TargetId = new TargetId(random.Next(300))
                    });
                }

                actual.Clear();
                OccupancyExchange.Exchange(
                    previous,
                    overlaps,
                    current,
                    new ProjectileId(4),
                    2f,
                    0.5f,
                    3f,
                    frame % 2 == 0 ? status : null,
                    hit => actual.Add(hit.Source + " " + hit.TargetId + " " + hit.Damage),
                    info => actual.Add("status " + info.TargetId));

                expected.Clear();
                ReferenceExchange(referencePrevious, overlaps, frame % 2 == 0, expected);

                CollectionAssert.AreEqual(expected, actual, "frame " + frame);
                Assert.AreEqual(referencePrevious.Count, previous.Count);
                for (int i = 0; i < previous.Count; i++)
                    Assert.AreEqual(referencePrevious[i], previous[i]);
            }
        }

        [Test]
        public void LookupsById_AfterPoolingAndRespawn_SeeOnlyTheLiveInstance()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("hot-lookup", radius: 0.4f, lifetime: 0.5f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 1);
            using (world)
            {
                var expired = new List<ExpireInfo>();
                world.Expired += expired.Add;
                ProjectileId first = Fire(world, recipe);
                AssertLive(world, first, true);

                world.Despawn(first);
                Assert.AreEqual(0, world.ActiveCount);
                AssertLive(world, first, false);

                ProjectileId second = Fire(world, recipe);
                Assert.AreEqual(0, world.AvailableCount, "the second shot reuses the pooled entity");
                AssertLive(world, first, false);
                AssertLive(world, second, true);

                world.Despawn(first);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(1, expired.Count);

                world.Tick(0.6f);
                Assert.AreEqual(0, world.ActiveCount);
                AssertLive(world, second, false);
                Assert.AreEqual(2, expired.Count);

                ProjectileId third = Fire(world, recipe);
                AssertLive(world, second, false);
                AssertLive(world, third, true);
                world.Despawn(third);
                AssertLive(world, third, false);
                Assert.AreEqual(3, expired.Count);
                Assert.AreEqual(third, expired[2].InstanceId);
                Assert.AreEqual(ExpireReason.Despawned, expired[2].Reason);
            }
        }

        [Test]
        public void HitscanInstance_IsNotLiveAfterSpawnReturns()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("hot-lookup-hitscan", lifetime: 5f);
            recipe.Delivery = Delivery.Hitscan;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 1);
            using (world)
            {
                ProjectileId id = Fire(world, recipe);
                AssertLive(world, id, false);
                world.Despawn(id);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void DespawnFromTheSpawnedEvent_FindsTheNewInstance()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("hot-lookup-spawned", lifetime: 5f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 1);
            using (world)
            {
                var expired = new List<ExpireInfo>();
                world.Expired += expired.Add;
                world.Spawned += info => world.Despawn(info.InstanceId);

                ProjectileId id = Fire(world, recipe);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(1, expired.Count);
                Assert.AreEqual(id, expired[0].InstanceId);
                Assert.AreEqual(ExpireReason.Despawned, expired[0].Reason);
                AssertLive(world, id, false);
            }
        }

        [Test]
        public void AimBeam_StaleIdOnARecycledEntity_ChangesNothing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "hot-lookup-beam",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(720f) });
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 1);
            using (world)
            {
                var target = new TargetId(5);
                world.SetHitTarget(target, new Vector3(0f, 0f, -30f), 0.5f);
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += locks.Add;

                ProjectileId stale = Fire(world, recipe);
                world.Despawn(stale);
                ProjectileId live = Fire(world, recipe);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(live, out Vector3 before, out Vector3 beforeVelocity, out _));
                locks.Clear();

                world.AimBeam(stale, new Vector3(5f, 0f, 0f), Vector3.right, HomingTarget.Assigned(target));
                world.AimBeam(stale, new Vector3(5f, 0f, 0f), Vector3.right);
                Assert.AreEqual(0, locks.Count);
                Assert.IsTrue(world.TryGetHomingTarget(live, out HomingTarget unchanged));
                Assert.IsFalse(unchanged.IsAssigned);
                Assert.IsTrue(world.TryGetState(live, out Vector3 after, out Vector3 afterVelocity, out _));
                Assert.AreEqual(before, after);
                Assert.AreEqual(beforeVelocity, afterVelocity);

                world.AimBeam(live, new Vector3(5f, 0f, 0f), Vector3.right, HomingTarget.Assigned(target));
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(live, locks[0].InstanceId);
                Assert.AreEqual(target, locks[0].TargetId);
            }
        }

        [Test]
        public void PendingSpawns_AppendedDuringTheFlush_SpawnInQueueOrder()
        {
            Recipe leaf = SimulationTestHelper.CreateRecipe("hot-flush-leaf");
            Recipe middle = SimulationTestHelper.CreateRecipe("hot-flush-middle", onSpawn: new IModifier[] { new RequestChildren(leaf, 2) });
            Recipe root = SimulationTestHelper.CreateRecipe("hot-flush-root", onSpawn: new IModifier[] { new RequestChildren(middle, 3) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(root, prewarm: 1);
            using (world)
            {
                var order = new List<string>();
                world.Spawned += info => order.Add(info.InstanceId + "<" + info.ParentId);

                Fire(world, root);

                CollectionAssert.AreEqual(
                    new[] { "1<0", "2<1", "3<1", "4<1", "5<2", "6<2", "7<3", "8<3", "9<4", "10<4" },
                    order);
                Assert.AreEqual(10, world.ActiveCount);
            }
        }

        [Test]
        public void PendingSpawns_ASpawnFromTheSpawnedEvent_FlushesTheRestOfTheQueueFirst()
        {
            Recipe leaf = SimulationTestHelper.CreateRecipe("hot-reflush-leaf");
            Recipe extra = SimulationTestHelper.CreateRecipe("hot-reflush-extra");
            Recipe middle = SimulationTestHelper.CreateRecipe("hot-reflush-middle", onSpawn: new IModifier[] { new RequestChildren(leaf, 1) });
            Recipe root = SimulationTestHelper.CreateRecipe("hot-reflush-root", onSpawn: new IModifier[] { new RequestChildren(middle, 3) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(root, prewarm: 1);
            using (world)
            {
                var order = new List<string>();
                bool spawnedExtra = false;
                world.Spawned += info =>
                {
                    order.Add(info.InstanceId + "<" + info.ParentId);
                    if (spawnedExtra || info.Recipe != middle)
                        return;

                    spawnedExtra = true;
                    Fire(world, extra);
                };

                Fire(world, root);

                // 2 is the first middle. Its Spawned handler fires 3, whose own flush drains the queue (middles 4 and 5,
                // then their leaves 6 and 7) before 2's OnSpawn queues its leaf 8.
                CollectionAssert.AreEqual(
                    new[] { "1<0", "2<1", "3<0", "4<1", "5<1", "6<4", "7<5", "8<2" },
                    order);
                Assert.AreEqual(8, world.ActiveCount);
            }
        }

        static ProjectileId Fire(ProjectileWorld world, Recipe recipe)
        {
            return world.Spawn(new SpawnRequest { Recipe = recipe, Position = Vector3.zero, Direction = Vector3.forward });
        }

        static void AssertLive(ProjectileWorld world, ProjectileId id, bool live)
        {
            Assert.AreEqual(live, world.TryGetState(id, out _, out _, out _), "state " + id);
            Assert.AreEqual(live, world.TryGetRadius(id, out float radius), "radius " + id);
            Assert.AreEqual(live, world.TryGetManaCost(id, out _), "mana " + id);
            Assert.AreEqual(live, world.TryGetGravity(id, out _), "gravity " + id);
            Assert.AreEqual(live, world.TryGetSpread(id, out _), "spread " + id);
            Assert.AreEqual(live, world.TryGetCombatStats(id, out _, out _, out _, out _), "combat " + id);
            Assert.AreEqual(live, world.TryGetRemaining(id, out _, out _), "remaining " + id);
            Assert.AreEqual(live, world.TryGetHomingTarget(id, out _), "homing " + id);
            if (live)
                Assert.Greater(radius, 0f);
        }

        static List<Body> RandomBodies(System.Random random, int ids, float reach, bool withHeight)
        {
            var bodies = new List<Body>();
            for (int id = 0; id < ids; id++)
            {
                if (random.Next(4) == 0)
                    continue;

                int copies = 1 + random.Next(3);
                for (int c = 0; c < copies; c++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                    float distance = (float)random.NextDouble() * reach;
                    float height = withHeight ? (float)(random.NextDouble() - 0.5) : 0f;
                    bodies.Add(new Body
                    {
                        Id = new TargetId(id),
                        Position = new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance),
                        Protection = random.Next(8) == 0 ? ProtectionLevel.Strong : (ProtectionLevel)random.Next(2)
                    });
                }
            }

            for (int i = bodies.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (bodies[i], bodies[j]) = (bodies[j], bodies[i]);
            }

            return bodies;
        }

        static void AddBodies(ScriptedHitQuery hits, List<Body> bodies)
        {
            for (int i = 0; i < bodies.Count; i++)
                hits.AddOverlapTarget(bodies[i].Id, bodies[i].Position, bodies[i].Protection);
        }

        static List<TargetId> FirstSeen(List<Body> bodies)
        {
            var ids = new List<TargetId>();
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].Protection != ProtectionLevel.Strong && !ids.Contains(bodies[i].Id))
                    ids.Add(bodies[i].Id);
            }

            return ids;
        }

        static List<string> ExpectedExchange(List<TargetId> previous, List<TargetId> current)
        {
            var events = new List<string>();
            for (int i = 0; i < current.Count; i++)
            {
                if (!previous.Contains(current[i]))
                    events.Add(HitSource.OccupancyEnter + " " + current[i]);
            }

            for (int i = 0; i < previous.Count; i++)
            {
                if (!current.Contains(previous[i]))
                    events.Add(HitSource.OccupancyExit + " " + previous[i]);
            }

            previous.Clear();
            previous.AddRange(current);
            return events;
        }

        static void ReferenceExchange(List<TargetId> previous, List<HitInfo> overlaps, bool withStatus, List<string> events)
        {
            var current = new List<TargetId>();
            for (int i = 0; i < overlaps.Count; i++)
            {
                HitInfo hit = overlaps[i];
                if (hit.Kind != HitKind.Target || hit.Protection == ProtectionLevel.Strong)
                    continue;
                if (current.Contains(hit.TargetId))
                    continue;

                current.Add(hit.TargetId);
                if (previous.Contains(hit.TargetId))
                    continue;

                events.Add(HitSource.OccupancyEnter + " " + hit.TargetId + " " + 2f);
                if (withStatus)
                    events.Add("status " + hit.TargetId);
            }

            for (int i = 0; i < previous.Count; i++)
            {
                if (!current.Contains(previous[i]))
                    events.Add(HitSource.OccupancyExit + " " + previous[i] + " " + 0f);
            }

            previous.Clear();
            previous.AddRange(current);
        }

        sealed class StubStatus : IStatusEffect
        {
            public string Id => "stub";
        }
    }
}
