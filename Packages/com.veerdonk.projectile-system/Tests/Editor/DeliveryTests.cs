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
                Recipe recipe = asset.ToRecipe();
                Assert.AreEqual(Delivery.Hitscan, recipe.Delivery);

                Recipe clone = recipe.Clone();
                clone.Delivery = Delivery.Arc;
                Assert.AreEqual(Delivery.Hitscan, recipe.Delivery);
                Assert.AreEqual(Delivery.Arc, clone.Delivery);
            }
            finally
            {
                Object.DestroyImmediate(asset);
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
}
