using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class DeliveryTests
    {
        static readonly Vector3 SpawnPosition = new Vector3(0f, 1f, 0f);
        static readonly Vector3 DummyTargetPosition = new Vector3(0f, 1f, 8f);
        const float AimedSpeed = 32f;
        const float AimedGravity = 80f;
        const float MeetTolerance = 0.5f;
        const float SimDt = 0.01f;

        [Test]
        public void Arc_WithAimPoint_MeetsReachableDummy_WithoutCallerSolvingDirection()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "arc-aim",
                speed: AimedSpeed,
                lifetime: 5f,
                gravity: AimedGravity);
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = SpawnPosition,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = DummyTargetPosition
                });

                Assert.AreNotEqual(default(ProjectileId), id);
                float closest = float.MaxValue;
                Vector3 previous = SpawnPosition;
                for (int i = 0; i < 500; i++)
                {
                    if (!world.TryGetState(id, out _, out _, out _))
                        break;

                    world.Tick(SimDt);
                    if (!world.TryGetState(id, out Vector3 position, out _, out _))
                        break;

                    closest = Mathf.Min(closest, DistanceToSegment(previous, position, DummyTargetPosition));
                    previous = position;
                    if (closest <= MeetTolerance)
                        break;
                }

                Assert.LessOrEqual(closest, MeetTolerance);
            }
        }

        [Test]
        public void Arc_UnreachableAimPoint_DoesNotSpawn_AndDoesNotExpire()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "arc-unreachable",
                speed: 1f,
                lifetime: 5f,
                gravity: AimedGravity);
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int expired = 0;
                world.Expired += _ => expired++;
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = SpawnPosition,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = DummyTargetPosition
                });

                Assert.AreEqual(default(ProjectileId), id);
                Assert.AreEqual(0, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Straight_WithPositiveGravity_StaysLevel_ArcDrops()
        {
            const float gravity = 10f;
            const float dt = 0.2f;
            Recipe straight = SimulationTestHelper.CreateRecipe(
                "straight-gravity",
                speed: 10f,
                lifetime: 5f,
                gravity: gravity);
            Assert.AreEqual(Delivery.Straight, straight.Delivery);

            (ProjectileWorld straightWorld, _) = SimulationTestHelper.CreateWorld(straight);
            using (straightWorld)
            {
                ProjectileId id = straightWorld.Spawn(new SpawnRequest
                {
                    Recipe = straight,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 4; i++)
                    straightWorld.Tick(dt);

                Assert.IsTrue(straightWorld.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                Assert.AreEqual(0f, position.y, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
            }

            Recipe arc = SimulationTestHelper.CreateRecipe(
                "arc-gravity",
                speed: 10f,
                lifetime: 5f,
                gravity: gravity);
            arc.Delivery = Delivery.Arc;
            (ProjectileWorld arcWorld, _) = SimulationTestHelper.CreateWorld(arc);
            using (arcWorld)
            {
                ProjectileId id = arcWorld.Spawn(new SpawnRequest
                {
                    Recipe = arc,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                arcWorld.Tick(dt);
                Assert.IsTrue(arcWorld.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                Assert.Less(position.y, 0f);
                Assert.Less(velocity.y, 0f);
            }
        }

        [Test]
        public void Hitscan_PublishedSphere_RaisesTargetHit_BeforeSpawnReturns()
        {
            Recipe recipe = HitscanRecipe("hitscan-published");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.Expired += _ => expired++;
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreNotEqual(default(ProjectileId), instance);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(1, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_Pierce2_DiesOnTheThirdPublishedVolume()
        {
            Recipe recipe = HitscanRecipe(
                "hitscan-pierce",
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

                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);
                Assert.AreEqual(second, seen[1].TargetId);
                Assert.AreEqual(third, seen[2].TargetId);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitKind.Target, seen[1].Kind);
                Assert.AreEqual(HitKind.Target, seen[2].Kind);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_WorldWall_EndsTheRay_EvenWithPierceRemaining()
        {
            Recipe recipe = HitscanRecipe(
                "hitscan-wall",
                onHit: new IModifier[] { new Pierce(2) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var target = new TargetId(4);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(target, new Vector3(0f, 0f, 8f), 0.5f);
                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_Bounce1_ReflectsOffWorld_ThenHitsPublishedVolume()
        {
            Recipe recipe = HitscanRecipe(
                "hitscan-bounce",
                onHit: new IModifier[] { new Bounce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var target = new TargetId(5);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(target, new Vector3(0f, 0f, -2f), 0.5f);
                hits.Queue(HitKind.World, new Vector3(0f, 0f, 4f), Vector3.back);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(HitKind.Target, seen[1].Kind);
                Assert.AreEqual(target, seen[1].TargetId);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_Homing_KinksAfterPierce_WithinDegrees()
        {
            const float kink = 40f;
            Recipe recipe = HitscanRecipe(
                "hitscan-kink",
                onTick: new IModifier[] { new Homing(kink) },
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                var second = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(first, new Vector3(0f, 0f, 4f), 0.5f);
                world.SetHitTarget(second, new Vector3(3f, 0f, 10f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);
                Assert.AreEqual(second, seen[1].TargetId);
                float bend = Vector3.Angle(Vector3.forward, seen[1].Point - seen[0].Point);
                Assert.Greater(bend, 5f);
                Assert.LessOrEqual(bend, kink + 1f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_Homing_SmallKink_MissesOffAxisVolume()
        {
            Recipe recipe = HitscanRecipe(
                "hitscan-kink-small",
                onTick: new IModifier[] { new Homing(10f) },
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 4f), 0.5f);
                world.SetHitTarget(new TargetId(2), new Vector3(3f, 0f, 10f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(new TargetId(1), seen[0].TargetId);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_Miss_ExpiresWithNoHit()
        {
            Recipe recipe = HitscanRecipe("hitscan-miss");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
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

                Assert.AreNotEqual(default(ProjectileId), id);
                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(1, expired);
                Assert.IsFalse(world.TryGetState(id, out _, out _, out _));
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Hitscan_StrongPublishedVolume_EatsTheShot_WithNoHit()
        {
            Recipe recipe = HitscanRecipe(
                "hitscan-strong",
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

                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(0, statuses.Count);
                Assert.AreEqual(1, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void ToRecipe_AndClone_CopyDelivery()
        {
            var asset = ScriptableObject.CreateInstance<RecipeAsset>();
            try
            {
                asset.Id = "hitscan-asset";
                asset.Delivery = Delivery.Hitscan;
                asset.BeamPulseSeconds = 0.25f;
                Recipe recipe = asset.ToRecipe();
                Assert.AreEqual(Delivery.Hitscan, recipe.Delivery);
                Assert.AreEqual(0.25f, recipe.BeamPulseSeconds);

                Recipe clone = recipe.Clone();
                clone.Delivery = Delivery.Arc;
                clone.BeamPulseSeconds = 0.5f;
                Assert.AreEqual(Delivery.Hitscan, recipe.Delivery);
                Assert.AreEqual(0.25f, recipe.BeamPulseSeconds);
                Assert.AreEqual(Delivery.Arc, clone.Delivery);
                Assert.AreEqual(0.5f, clone.BeamPulseSeconds);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Beam_Pulse_RaisesDirectHit_AndStaysAtSpawn()
        {
            Recipe recipe = BeamRecipe("beam-published");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var id = new TargetId(7);
                var seen = new List<HitInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.Expired += _ => expired++;
                world.SetHitTarget(id, new Vector3(0f, 0f, 3f), 0.5f);
                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreNotEqual(default(ProjectileId), instance);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.Target, seen[0].Kind);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
                Assert.AreEqual(id, seen[0].TargetId);
                Assert.AreEqual(0, expired);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(instance, out Vector3 position, out _, out _));
                Assert.AreEqual(0f, position.z, 0.0001f);
            }
        }

        [Test]
        public void Beam_Pierce2_EndsPulseOnThird_AndTheNextPulseHitsAgain()
        {
            Recipe recipe = BeamRecipe("beam-pierce", onHit: new IModifier[] { new Pierce(2) });
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
                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);
                Assert.AreEqual(third, seen[2].TargetId);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.1f);

                Assert.AreEqual(6, seen.Count);
                Assert.AreEqual(first, seen[3].TargetId);
                Assert.AreEqual(third, seen[5].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(instance, out _, out _, out _));
            }
        }

        [Test]
        public void Beam_WorldWall_ClipsThePulse_InstanceStaysLive()
        {
            Recipe recipe = BeamRecipe("beam-wall", onHit: new IModifier[] { new Pierce(2) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(4), new Vector3(0f, 0f, 8f), 0.5f);
                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.back);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_Lifetime_ExpiresWithoutDespawn()
        {
            Recipe recipe = BeamRecipe("beam-life", lifetime: 0.2f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                int expired = 0;
                world.Hit += info => seen.Add(info);
                world.Expired += _ => expired++;
                world.SetHitTarget(new TargetId(8), new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(0, expired);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.2f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(1, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_Despawn_RaisesExpired_AndRunsOnExpire()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("beam-expire-child", lifetime: 5f);
            Recipe recipe = BeamRecipe("beam-despawn", onExpire: new IModifier[] { new Multiply(1, child) });
            recipe.MultiplyGenerations = 1;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            world.Prewarm(child, 2);
            using (world)
            {
                int expired = 0;
                world.Expired += _ => expired++;
                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Despawn(instance);

                Assert.AreEqual(1, expired);
                Assert.IsFalse(world.TryGetState(instance, out _, out _, out _));
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Despawn_UnknownOrAlreadyPooled_IsNoOp_AndWorksOnStraight()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("despawn-straight", lifetime: 5f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int expired = 0;
                world.Expired += _ => expired++;
                world.Despawn(new ProjectileId(404));
                world.Despawn(default);
                Assert.AreEqual(0, expired);

                ProjectileId instance = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Despawn(instance);
                world.Despawn(instance);

                Assert.AreEqual(1, expired);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_ApplyStatus_OnPulsedTarget()
        {
            Recipe recipe = BeamRecipe(
                "beam-status",
                onHit: new IModifier[] { new ApplyStatus(new DummyStatusEffect()) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var statuses = new List<StatusInfo>();
                world.StatusApplied += info => statuses.Add(info);
                world.SetHitTarget(new TargetId(9), new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, statuses.Count);
                Assert.IsInstanceOf<DummyStatusEffect>(statuses[0].Effect);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_StrongPublishedVolume_EatsThePulse_WithNoHit()
        {
            Recipe recipe = BeamRecipe(
                "beam-strong",
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

                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(0, statuses.Count);
                Assert.AreEqual(0, expired);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_NonPositivePulse_HitsOnEveryTick()
        {
            Recipe recipe = BeamRecipe("beam-every-tick", pulse: 0f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(11), new Vector3(0f, 0f, 3f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.05f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_WithoutPierce_SecondPulseHitsTheSameVolume()
        {
            Recipe recipe = BeamRecipe("beam-no-pierce");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var first = new TargetId(1);
                var second = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(first, new Vector3(0f, 0f, 3f), 0.5f);
                world.SetHitTarget(second, new Vector3(0f, 0f, 8f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(first, seen[0].TargetId);
                world.Tick(0.1f);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(first, seen[1].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_Homing_CurvesFromTheBarrel_InsteadOfAStraightRetarget()
        {
            Recipe recipe = BeamRecipe("beam-curve", onTick: new IModifier[] { new Homing(100f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var blocking = new TargetId(1);
                var aimed = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(blocking, new Vector3(0f, 0f, 2f), 0.5f);
                world.SetHitTarget(aimed, new Vector3(4f, 0f, 6f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = new Vector3(4f, 0f, 6f)
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(blocking, seen[0].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_Homing_ReachesAnOffAxisVolume()
        {
            Recipe straight = BeamRecipe("beam-no-curve");
            Recipe curved = BeamRecipe("beam-reaches", onTick: new IModifier[] { new Homing(300f) });
            var aimed = new TargetId(4);
            var aim = new Vector3(3f, 0f, 12f);

            (ProjectileWorld missed, _) = SimulationTestHelper.CreateWorld(straight);
            using (missed)
            {
                var seen = new List<HitInfo>();
                missed.Hit += info => seen.Add(info);
                missed.SetHitTarget(aimed, aim, 0.75f);
                missed.Spawn(new SpawnRequest
                {
                    Recipe = straight,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Assert.AreEqual(0, seen.Count);
            }

            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(curved);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(aimed, aim, 0.75f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = curved,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = aim
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(aimed, seen[0].TargetId);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void Beam_ColliderLine_WithoutPierce_HitsTheFirstDummyAgain()
        {
            var parent = new GameObject("beam-collider-line");
            GameObject near = null;
            GameObject far = null;
            try
            {
                near = GameObject.CreatePrimitive(PrimitiveType.Cube);
                near.transform.position = new Vector3(0f, 200f, 4f);
                near.AddComponent<BeamLineDummy>().Id = new TargetId(1);
                far = GameObject.CreatePrimitive(PrimitiveType.Cube);
                far.transform.position = new Vector3(0f, 200f, 8f);
                far.AddComponent<BeamLineDummy>().Id = new TargetId(2);
                Physics.SyncTransforms();

                Recipe recipe = BeamRecipe("beam-collider-line");
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    var seen = new List<HitInfo>();
                    world.Hit += info => seen.Add(info);
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = new Vector3(0f, 200f, 0f),
                        Direction = Vector3.forward
                    });

                    Assert.AreEqual(1, seen.Count);
                    Assert.AreEqual(HitKind.Target, seen[0].Kind);
                    Assert.AreEqual(new TargetId(1), seen[0].TargetId);

                    world.Tick(0.1f);
                    Assert.AreEqual(2, seen.Count);
                    Assert.AreEqual(new TargetId(1), seen[1].TargetId);
                    Assert.AreEqual(1, world.ActiveCount);
                }
            }
            finally
            {
                if (near != null)
                    UnityEngine.Object.DestroyImmediate(near);
                if (far != null)
                    UnityEngine.Object.DestroyImmediate(far);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_HomingView_DrawsThePathToTheTarget_AndPastAPierce()
        {
            var parent = new GameObject("beam-homing-view");
            try
            {
                Recipe curved = BeamRecipe("beam-homing-view", onTick: new IModifier[] { new Homing(300f) });
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    world.SetHitTarget(new TargetId(1), new Vector3(2f, 0f, 8f), 0.6f);
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = curved,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        HomingAcquirePointSet = true,
                        HomingAcquirePoint = new Vector3(2f, 0f, 8f)
                    });

                    Assert.IsTrue(BeamReaches(parent.transform, new Vector3(2f, 0f, 8f), 1.5f));
                }

                Recipe piercing = BeamRecipe(
                    "beam-pierce-view",
                    onTick: new IModifier[] { new Homing(100f) },
                    onHit: new IModifier[] { new Pierce(1) });
                var pierced = new ProjectileWorld(parent.transform);
                using (pierced)
                {
                    pierced.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 3f), 0.45f);
                    pierced.SetHitTarget(new TargetId(2), new Vector3(0f, 0f, 8f), 0.45f);
                    pierced.Spawn(new SpawnRequest
                    {
                        Recipe = piercing,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Assert.IsTrue(BeamReaches(parent.transform, new Vector3(0f, 0f, 8f), 1.5f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_AimBeam_RedrawsWithoutAHit_AndTheNextPulseUsesTheNewPose()
        {
            Recipe recipe = BeamRecipe("beam-aim");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var forward = new TargetId(1);
                var behind = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(forward, new Vector3(0f, 0f, 3f), 0.5f);
                world.SetHitTarget(behind, new Vector3(0f, 0f, -3f), 0.5f);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(forward, seen[0].TargetId);

                world.AimBeam(id, Vector3.zero, Vector3.back);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.1f);
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(behind, seen[1].TargetId);
            }
        }

        [Test]
        public void Beam_Homing_FirstSpan_LeavesTheMuzzle_AlongSpawnDirection()
        {
            var parent = new GameObject("beam-muzzle");
            try
            {
                Recipe recipe = BeamRecipe("beam-muzzle", onTick: new IModifier[] { new Homing(300f) });
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    world.SetHitTarget(new TargetId(1), new Vector3(4f, 0f, 8f), 0.5f);
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        HomingAcquirePointSet = true,
                        HomingAcquirePoint = new Vector3(4f, 0f, 8f)
                    });

                    Assert.IsTrue(FirstSpanLeaves(parent.transform, Vector3.zero, Vector3.forward));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_Homing_Redraw_KeepsTheLock_WhenTheMuzzleSlides()
        {
            Recipe recipe = BeamRecipe("beam-sticky", onTick: new IModifier[] { new Homing(300f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var aimed = new TargetId(1);
                var other = new TargetId(2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(aimed, new Vector3(0f, 0f, 5f), 0.75f);
                world.SetHitTarget(other, new Vector3(6f, 0f, 4f), 0.75f);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(aimed, seen[0].TargetId);

                world.AimBeam(id, new Vector3(5f, 0f, 1f), Vector3.forward);
                world.Tick(0.1f);
                Assert.GreaterOrEqual(seen.Count, 2);
                Assert.AreEqual(aimed, seen[seen.Count - 1].TargetId);
            }
        }

        [Test]
        public void Beam_SkipsPresentationColliders()
        {
            var parent = new GameObject("beam-presentation");
            GameObject decoyRoot = null;
            GameObject dummy = null;
            try
            {
                decoyRoot = new GameObject("decoy-view");
                decoyRoot.AddComponent<ProjectileView>();
                GameObject decoy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                decoy.name = "DecoyCollider";
                decoy.transform.SetParent(decoyRoot.transform, true);
                decoy.transform.position = new Vector3(0f, 0f, 3f);
                decoy.transform.localScale = Vector3.one * 2f;

                dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.transform.position = new Vector3(0f, 0f, 8f);
                dummy.AddComponent<BeamLineDummy>().Id = new TargetId(7);
                Physics.SyncTransforms();

                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    var seen = new List<HitInfo>();
                    world.Hit += info => seen.Add(info);
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = BeamRecipe("beam-presentation"),
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Assert.AreEqual(1, seen.Count);
                    Assert.AreEqual(HitKind.Target, seen[0].Kind);
                    Assert.AreEqual(new TargetId(7), seen[0].TargetId);

                    Transform[] segments = parent.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i].name != "BeamSegment")
                            continue;
                        Assert.IsNull(segments[i].GetComponent<Collider>());
                    }
                }
            }
            finally
            {
                if (decoyRoot != null)
                    UnityEngine.Object.DestroyImmediate(decoyRoot);
                if (dummy != null)
                    UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_SegmentDiameter_StaysAcrossRedraws()
        {
            var root = new GameObject("beam-width");
            try
            {
                root.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var viewObject = new GameObject("view");
                viewObject.transform.SetParent(root.transform, false);
                viewObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var view = viewObject.AddComponent<ProjectileView>();
                var points = new System.Collections.Generic.List<Vector3>
                {
                    Vector3.zero,
                    new Vector3(0f, 0f, 2f),
                    new Vector3(0f, 0f, 4f)
                };
                view.SetBeamSegments(points, 0.15f);
                Assert.AreEqual(0.3f, SegmentDiameter(viewObject.transform), 0.03f);

                view.ScaleToRadius = true;
                view.SetRadius(0.15f);
                Assert.AreEqual(0.3f, SegmentDiameter(viewObject.transform), 0.03f);

                points[2] = new Vector3(0.2f, 0f, 4f);
                view.SetBeamSegments(points, 0.15f);
                Assert.AreEqual(0.3f, SegmentDiameter(viewObject.transform), 0.03f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Beam_TextureCoordinate_RunsMuzzleToTip()
        {
            var root = new GameObject("beam-uv");
            try
            {
                var view = root.AddComponent<ProjectileView>();
                view.SetBeamSegments(
                    new[]
                    {
                        Vector3.zero,
                        new Vector3(0f, 0f, 2f),
                        new Vector3(0f, 0f, 6f)
                    },
                    0.15f);

                Transform first = root.transform.GetChild(0);
                Transform second = root.transform.GetChild(1);
                AssertLengthCoordinate(first, 0f, 2f / 6f);
                AssertLengthCoordinate(second, 2f / 6f, 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Beam_Homing_MeshReachesTheTarget_WithoutAGap()
        {
            var parent = new GameObject("beam-gap");
            try
            {
                Recipe recipe = BeamRecipe("beam-gap", onTick: new IModifier[] { new Homing(300f) });
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    var aim = new Vector3(2f, 200f, 8f);
                    var origin = new Vector3(0f, 200f, 0f);
                    world.SetHitTarget(new TargetId(1), aim, 0.5f);
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = origin,
                        Direction = Vector3.forward,
                        HomingAcquirePointSet = true,
                        HomingAcquirePoint = aim
                    });

                    AssertBeamChain(parent.transform, origin, aim, 0.75f);
                    world.Tick(0.1f);
                    AssertBeamChain(parent.transform, origin, aim, 0.75f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_HomingPierce_MeshContinuesPastTheFirstTarget()
        {
            var parent = new GameObject("beam-past");
            GameObject first = null;
            GameObject second = null;
            try
            {
                first = GameObject.CreatePrimitive(PrimitiveType.Cube);
                first.transform.position = new Vector3(0f, 200f, 4f);
                first.AddComponent<BeamLineDummy>().Id = new TargetId(1);
                second = GameObject.CreatePrimitive(PrimitiveType.Cube);
                second.transform.position = new Vector3(0f, 200f, 9f);
                second.AddComponent<BeamLineDummy>().Id = new TargetId(2);
                Physics.SyncTransforms();

                Recipe recipe = BeamRecipe(
                    "beam-past",
                    onTick: new IModifier[] { new Homing(120f) },
                    onHit: new IModifier[] { new Pierce(1) });
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = new Vector3(0f, 200f, 0f),
                        Direction = Vector3.forward
                    });

                    Assert.IsTrue(BeamReaches(parent.transform, new Vector3(0f, 200f, 9f), 1.2f));
                }
            }
            finally
            {
                if (first != null)
                    UnityEngine.Object.DestroyImmediate(first);
                if (second != null)
                    UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_Bounce_Redraw_KeepsOneReflection()
        {
            var parent = new GameObject("beam-bounce");
            GameObject wall = null;
            try
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 200f, 4f);
                wall.transform.localScale = new Vector3(6f, 6f, 0.4f);
                Physics.SyncTransforms();

                Recipe recipe = BeamRecipe("beam-bounce", onHit: new IModifier[] { new Bounce(1) });
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    var origin = new Vector3(0f, 200f, 0f);
                    ProjectileId id = world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = origin,
                        Direction = Vector3.forward
                    });

                    world.AimBeam(id, origin, Vector3.forward);
                    System.Collections.Generic.List<Vector3> first = CaptureEnds(parent.transform);
                    world.AimBeam(id, origin, Vector3.forward);
                    System.Collections.Generic.List<Vector3> second = CaptureEnds(parent.transform);

                    Assert.IsTrue(HasCoordinate(first, 2, -1f));
                    Assert.AreEqual(first.Count, second.Count);
                    for (int i = 0; i < first.Count; i++)
                        Assert.Less((first[i] - second[i]).magnitude, 0.2f);
                }
            }
            finally
            {
                if (wall != null)
                    UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        static float SegmentDiameter(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform segment = transforms[i];
                if (segment.name != "BeamSegment" || !segment.gameObject.activeInHierarchy)
                    continue;

                return segment.TransformVector(Vector3.right).magnitude;
            }

            return 0f;
        }

        static void AssertLengthCoordinate(Transform segment, float startV, float endV)
        {
            Mesh mesh = segment.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            float minY = vertices[0].y;
            float maxY = vertices[0].y;
            for (int i = 1; i < vertices.Length; i++)
            {
                if (vertices[i].y < minY)
                    minY = vertices[i].y;
                if (vertices[i].y > maxY)
                    maxY = vertices[i].y;
            }

            bool sawStart = false;
            bool sawEnd = false;
            float span = maxY - minY;
            for (int i = 0; i < vertices.Length; i++)
            {
                float t = span > 1e-6f ? (vertices[i].y - minY) / span : 0f;
                if (t < 0.02f)
                {
                    sawStart = true;
                    Assert.AreEqual(startV, uv[i].y, 0.02f);
                }
                else if (t > 0.98f)
                {
                    sawEnd = true;
                    Assert.AreEqual(endV, uv[i].y, 0.02f);
                }
            }

            Assert.IsTrue(sawStart);
            Assert.IsTrue(sawEnd);
        }

        static void AssertBeamChain(Transform root, Vector3 origin, Vector3 target, float reach)
        {
            System.Collections.Generic.List<Vector3> ends = CaptureEnds(root);
            Assert.Greater(ends.Count, 1);
            float originGap = float.MaxValue;
            float targetGap = float.MaxValue;
            for (int i = 0; i < ends.Count; i++)
            {
                originGap = Mathf.Min(originGap, (ends[i] - origin).magnitude);
                targetGap = Mathf.Min(targetGap, (ends[i] - target).magnitude);
            }

            Assert.Less(originGap, reach);
            Assert.Less(targetGap, reach);

            float worstJoint = 0f;
            for (int i = 0; i < ends.Count; i++)
            {
                float nearest = float.MaxValue;
                for (int j = 0; j < ends.Count; j++)
                {
                    if (i == j)
                        continue;
                    nearest = Mathf.Min(nearest, (ends[i] - ends[j]).magnitude);
                }

                if (nearest > worstJoint)
                    worstJoint = nearest;
            }

            Assert.Less(worstJoint, 0.65f);
        }

        static System.Collections.Generic.List<Vector3> CaptureEnds(Transform root)
        {
            var ends = new System.Collections.Generic.List<Vector3>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform segment = transforms[i];
                if (segment.name != "BeamSegment" || !segment.gameObject.activeInHierarchy)
                    continue;

                Vector3 half = segment.TransformVector(Vector3.up);
                ends.Add(segment.position - half);
                ends.Add(segment.position + half);
            }

            return ends;
        }

        static bool HasCoordinate(System.Collections.Generic.List<Vector3> points, int axis, float limit)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i][axis] < limit)
                    return true;
            }

            return false;
        }

        static bool FirstSpanLeaves(Transform root, Vector3 origin, Vector3 direction)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Vector3 forward = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform segment = transforms[i];
                if (segment.name != "BeamSegment" || !segment.gameObject.activeInHierarchy)
                    continue;

                Vector3 half = segment.up * segment.lossyScale.y;
                Vector3 axis = half * 2f;
                if (axis.sqrMagnitude < 1e-8f)
                    return false;
                if (Mathf.Abs(Vector3.Dot(axis.normalized, forward)) < 0.99f)
                    return false;

                return DistanceToSegment(segment.position - half, segment.position + half, origin) < 0.05f;
            }

            return false;
        }

        static bool BeamReaches(Transform root, Vector3 target, float slack)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform segment = transforms[i];
                if (segment.name != "BeamSegment" || !segment.gameObject.activeInHierarchy)
                    continue;

                Vector3 half = segment.up * segment.lossyScale.y;
                if (DistanceToSegment(segment.position - half, segment.position + half, target) <= slack)
                    return true;
            }

            return false;
        }

        [Test]
        public void Beam_View_IsACylinderPerSegment_ScaledToRadius()
        {
            var parent = new GameObject("beam-pool");
            try
            {
                Recipe recipe = BeamRecipe("beam-view");
                recipe.Radius = 0.4f;
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    int cylinders = 0;
                    float diameter = 0f;
                    Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        if (transforms[i].name != "BeamSegment")
                            continue;

                        cylinders++;
                        diameter = transforms[i].lossyScale.x;
                    }

                    Assert.GreaterOrEqual(cylinders, 1);
                    Assert.AreEqual(0.8f, diameter, 0.02f);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        static Recipe HitscanRecipe(
            string id,
            IModifier[] onTick = null,
            IModifier[] onHit = null)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                id,
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: onTick,
                onHit: onHit);
            recipe.Delivery = Delivery.Hitscan;
            return recipe;
        }

        static Recipe BeamRecipe(
            string id,
            float lifetime = 5f,
            float pulse = 0.1f,
            IModifier[] onTick = null,
            IModifier[] onHit = null,
            IModifier[] onExpire = null)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                id,
                speed: 20f,
                lifetime: lifetime,
                radius: 0.25f,
                onTick: onTick,
                onHit: onHit,
                onExpire: onExpire);
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = pulse;
            return recipe;
        }

        static float DistanceToSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 1e-12f)
                return (point - a).magnitude;

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return (a + ab * t - point).magnitude;
        }
    }

    sealed class BeamLineDummy : MonoBehaviour, IDamageable
    {
        public TargetId Id;

        public TargetId TargetId => Id;
    }
}
