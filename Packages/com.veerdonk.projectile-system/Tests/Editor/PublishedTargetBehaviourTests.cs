using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PublishedTargetBehaviourTests
    {
        [Test]
        public void Homing_AcquiresPublishedHitTarget_WithoutHomingCandidate()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-home",
                speed: 10f,
                lifetime: 5f,
                onTick: new IModifier[] { new Homing(90f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(4);
                world.SetHitTarget(id, new Vector3(10f, 0f, 10f), 0.5f);
                ProjectileId shot = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetHomingTarget(shot, out HomingTarget locked));
                Assert.AreEqual(id, locked.Id);
                Assert.IsTrue(world.TryGetState(shot, out _, out Vector3 velocity, out _));
                Assert.AreEqual(9f, Vector3.Angle(Vector3.forward, velocity), 0.05f);
            }
        }

        [Test]
        public void Homing_SameIdInBothCatalogs_IsOneCandidate_HitPositionWins()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-home-dedup",
                speed: 10f,
                lifetime: 5f,
                onTick: new IModifier[] { new Homing(100f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var shared = new TargetId(1);
                world.SetHitTarget(shared, new Vector3(0f, 0f, 4f), 0.5f);
                world.SetHomingCandidate(shared, new Vector3(0f, 0f, 20f));
                world.SetHomingCandidate(new TargetId(2), new Vector3(0f, 0f, 6f));
                ProjectileId shot = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(shot, out HomingTarget locked));
                Assert.AreEqual(shared, locked.Id);
            }
        }

        [Test]
        public void Homing_PublishedTargetHit_DoesNotReacquireWhileItStaysAhead()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-home-drop",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(90f) },
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                ProjectileId shot = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(shot, out HomingTarget locked));
                Assert.AreEqual(id, locked.Id);

                world.Tick(0.3f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(shot, out locked));
                Assert.IsFalse(locked.IsAssigned);

                world.Tick(0.05f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(shot, out locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void ApplyStatus_PublishedTarget_RaisesStatus_WorldDoesNot()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-status",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new ApplyStatus(effect) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var statuses = new List<StatusInfo>();
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.4f);

                Assert.AreEqual(1, statuses.Count);
                Assert.AreEqual(id, statuses[0].TargetId);
                Assert.AreSame(effect, statuses[0].Effect);

                statuses.Clear();
                world.SetHitTarget(id, new Vector3(0f, 0f, 8f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.World, new Vector3(0f, 0f, 1f), Vector3.back);
                world.Tick(0.2f);

                Assert.AreEqual(0, statuses.Count);
            }
        }

        [Test]
        public void ApplyStatus_PierceThroughTwoPublishedVolumes_RaisesTwice()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-status-pierce",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(2), new ApplyStatus(effect) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                var second = new TargetId(2);
                var statuses = new List<StatusInfo>();
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(first, new Vector3(0f, 0f, 2f), 0.5f);
                world.SetHitTarget(second, new Vector3(0f, 0f, 5f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.3f);
                world.Tick(0.3f);

                Assert.AreEqual(2, statuses.Count);
                Assert.AreEqual(first, statuses[0].TargetId);
                Assert.AreEqual(second, statuses[1].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void BasicPublishedVolume_RunsOnHit_SkipsStatus()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-basic",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(1), new ApplyStatus(effect) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(3);
                var seen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                world.Hit += info => seen.Add(info);
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(id, new Vector3(0f, 0f, 2f), 0.5f, ProtectionLevel.Basic);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.4f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(ProtectionLevel.Basic, seen[0].Protection);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(0, statuses.Count);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void StrongPublishedVolume_EatsShot_WithNoHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-strong",
                speed: 10f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(1), new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.StatusApplied += info => statuses.Add(info);
                world.Expired += _ => expired++;
                world.SetHitTarget(new TargetId(3), new Vector3(0f, 0f, 2f), 0.5f, ProtectionLevel.Strong);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.4f);

                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(0, statuses.Count);
                Assert.AreEqual(1, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void AreaEffectAndGroundField_Status_IncludePublishedVolumes()
        {
            var effect = new DummyStatusEffect();
            Recipe areaRecipe = SimulationTestHelper.CreateRecipe(
                "published-area-status",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(4f, effect) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(areaRecipe);
            using (world)
            {
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                var statuses = new List<StatusInfo>();
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(inside, new Vector3(0f, 0f, 5.4f), 0.6f);
                world.SetHitTarget(outside, new Vector3(0f, 0f, 6.5f), 0.4f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = areaRecipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(HasStatus(statuses, inside));
                Assert.IsFalse(HasStatus(statuses, outside));
            }

            Recipe fieldRecipe = SimulationTestHelper.CreateRecipe(
                "published-ground-status",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, effect, 0.5f) });
            (ProjectileWorld fields, ScriptedHitQuery fieldHits) = SimulationTestHelper.CreateWorld(fieldRecipe);
            using (fields)
            {
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                var statuses = new List<StatusInfo>();
                fields.StatusApplied += info => statuses.Add(info);
                fields.SetHitTarget(inside, new Vector3(1.8f, 8f, 2f), 0.5f);
                fields.SetHitTarget(outside, new Vector3(3.2f, 0f, 2f), 0.3f);
                fields.Spawn(new SpawnRequest
                {
                    Recipe = fieldRecipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                fieldHits.Queue(HitKind.Target, new Vector3(0f, 1f, 2f), Vector3.back, new TargetId(1));
                fields.Tick(0.1f);

                Assert.IsTrue(HasStatus(statuses, inside));
                Assert.IsFalse(HasStatus(statuses, outside));
            }
        }

        [Test]
        public void OnTickAreaEffect_WithoutOccupancy_HitsEveryTick()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-ontick-area",
                speed: 0f,
                lifetime: 5f,
                effectRadius: 4f,
                onTick: new IModifier[] { new AreaEffect(2f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(9);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(id, new Vector3(3f, 0f, 0f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(HitSource.Area, seen[1].Source);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(id, seen[1].TargetId);
                Assert.AreEqual(2f, seen[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void StrongPublishedVolume_IsSkippedByOnTickArea()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-strong-area",
                speed: 0f,
                lifetime: 5f,
                effectRadius: 4f,
                onTick: new IModifier[] { new AreaEffect(2f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(9), new Vector3(3f, 0f, 0f), 0.5f, ProtectionLevel.Strong);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(0, seen.Count);
            }
        }

        [Test]
        public void AreaOccupancy_EnterOnce_SilentWhileInside_ExitOnLeave()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-occupancy-area",
                speed: 0f,
                lifetime: 5f,
                effectRadius: 4f,
                onTick: new IModifier[] { new AreaEffect(3f, effect, occupancy: true) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(9);
                var seen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                world.Hit += info => seen.Add(info);
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(id, new Vector3(10f, 0f, 0f), 0.4f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                Assert.AreEqual(0, seen.Count);

                world.SetHitTarget(id, new Vector3(3f, 0f, 0f), 0.4f);
                world.Tick(0.1f);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.OccupancyEnter, seen[0].Source);
                Assert.AreEqual(3f, seen[0].Damage, 1e-4f);
                Assert.AreEqual(1, statuses.Count);
                Assert.AreSame(effect, statuses[0].Effect);

                world.Tick(0.1f);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(1, statuses.Count);

                world.SetHitTarget(id, new Vector3(10f, 0f, 0f), 0.4f);
                world.Tick(0.1f);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.OccupancyExit, seen[1].Source);
                Assert.AreEqual(0f, seen[1].Damage, 1e-4f);
                Assert.AreEqual(id, seen[1].TargetId);
                Assert.AreEqual(1, statuses.Count);
            }
        }

        [Test]
        public void GroundFieldOccupancy_EnterOnce_SilentWhileInside_ExitOnLeave_SkipsStrong()
        {
            var effect = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "published-occupancy-ground",
                speed: 0f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 10f, 4f, effect, 0.05f, occupancy: true) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(4);
                var strong = new TargetId(5);
                var seen = new List<HitInfo>();
                var statuses = new List<StatusInfo>();
                world.Hit += info => seen.Add(info);
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(id, new Vector3(10f, 0f, 0f), 0.2f);
                world.SetHitTarget(strong, new Vector3(0.5f, 0f, 0f), 0.2f, ProtectionLevel.Strong);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1f, 0f),
                    Direction = Vector3.forward
                });

                world.Tick(0.2f);
                Assert.AreEqual(0, seen.Count);

                world.SetHitTarget(id, new Vector3(1f, 5f, 0f), 0.2f);
                world.Tick(0.2f);
                Assert.AreEqual(1, CountSource(seen, HitSource.OccupancyEnter));
                Assert.AreEqual(0, CountSource(seen, HitSource.Area));
                Assert.AreEqual(1, statuses.Count);
                Assert.AreEqual(id, statuses[0].TargetId);
                Assert.IsFalse(HasTarget(seen, strong));

                int afterStay = seen.Count;
                world.Tick(0.2f);
                Assert.AreEqual(afterStay, seen.Count);
                Assert.AreEqual(1, statuses.Count);

                world.SetHitTarget(id, new Vector3(10f, 0f, 0f), 0.2f);
                world.Tick(0.2f);
                Assert.AreEqual(1, CountSource(seen, HitSource.OccupancyExit));
                Assert.AreEqual(0f, Last(seen, HitSource.OccupancyExit).Damage, 1e-4f);
                Assert.AreEqual(1, statuses.Count);
            }
        }

        [Test]
        public void AreaOccupancy_TracksScriptedOverlapBody()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "occupancy-scripted",
                speed: 0f,
                lifetime: 5f,
                effectRadius: 5f,
                onTick: new IModifier[] { new AreaEffect(1f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(8);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                hits.AddOverlapTarget(id, new Vector3(1f, 0f, 0f));
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.OccupancyEnter, seen[0].Source);
                Assert.AreEqual(id, seen[0].TargetId);

                world.Tick(0.1f);
                Assert.AreEqual(1, seen.Count);

                hits.ClearOverlaps();
                world.Tick(0.1f);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.OccupancyExit, seen[1].Source);
                Assert.AreEqual(0f, seen[1].Damage, 1e-4f);
            }
        }

        static bool HasStatus(List<StatusInfo> statuses, TargetId id)
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].TargetId == id)
                    return true;
            }

            return false;
        }

        static bool HasTarget(List<HitInfo> hits, TargetId id)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].TargetId == id)
                    return true;
            }

            return false;
        }

        static int CountSource(List<HitInfo> hits, HitSource source)
        {
            int count = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Source == source)
                    count++;
            }

            return count;
        }

        static HitInfo Last(List<HitInfo> hits, HitSource source)
        {
            HitInfo last = default;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Source == source)
                    last = hits[i];
            }

            return last;
        }
    }
}
