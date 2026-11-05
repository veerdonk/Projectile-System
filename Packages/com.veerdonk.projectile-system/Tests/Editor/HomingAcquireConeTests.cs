using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HomingAcquireConeTests
    {
        RecipeAsset asset;

        [TearDown]
        public void TearDown()
        {
            if (asset != null)
                Object.DestroyImmediate(asset);
        }

        [Test]
        public void HalfAngle180_LocksTheOnlyTargetBehindTravel_AndTurnsBack()
        {
            var target = new TargetId(1);
            Vector3 targetPosition = new Vector3(1f, 0f, -5f);

            ProjectileId id = SpawnWithOneCandidate(180f, targetPosition, Vector3.forward, out ProjectileWorld world);
            using (world)
            {
                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(target, locked.Id);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(18f, Vector3.Angle(Vector3.forward, velocity), 0.05f);
                Assert.Greater(Vector3.Dot(velocity, Vector3.right), 0f);
            }
        }

        [Test]
        public void HalfAngle90_DoesNotLockTheOnlyTargetBehindTravel()
        {
            ProjectileId id = SpawnWithOneCandidate(90f, new Vector3(1f, 0f, -5f), Vector3.forward, out ProjectileWorld world);
            using (world)
            {
                world.Tick(0.1f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, velocity), 0.0001f);
            }
        }

        [Test]
        public void ShotFlyingUp_LocksATargetBesideIt_At180_NotAt90()
        {
            Vector3 beside = new Vector3(5f, 0f, 0f);

            ProjectileId wide = SpawnWithOneCandidate(180f, beside, Vector3.up, out ProjectileWorld wideWorld);
            using (wideWorld)
            {
                wideWorld.Tick(0.1f);
                Assert.IsTrue(wideWorld.TryGetHomingTarget(wide, out HomingTarget locked));
                Assert.IsTrue(locked.IsAssigned);
            }

            ProjectileId narrow = SpawnWithOneCandidate(90f, beside, Vector3.up, out ProjectileWorld narrowWorld);
            using (narrowWorld)
            {
                narrowWorld.Tick(0.1f);
                Assert.IsTrue(narrowWorld.TryGetHomingTarget(narrow, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void TargetSixtyDegreesOffTravel_LocksAt90_NotAt45()
        {
            Vector3 sixty = Quaternion.AngleAxis(60f, Vector3.up) * Vector3.forward * 5f;

            ProjectileId at90 = SpawnWithOneCandidate(90f, sixty, Vector3.forward, out ProjectileWorld world90);
            using (world90)
            {
                world90.Tick(0.01f);
                Assert.IsTrue(world90.TryGetHomingTarget(at90, out HomingTarget locked));
                Assert.AreEqual(new TargetId(1), locked.Id);
            }

            ProjectileId at45 = SpawnWithOneCandidate(45f, sixty, Vector3.forward, out ProjectileWorld world45);
            using (world45)
            {
                world45.Tick(0.01f);
                Assert.IsTrue(world45.TryGetHomingTarget(at45, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void HalfAngle_TakesTheNearestInsideTheCone()
        {
            Recipe recipe = HomingRecipe("cone-nearest", 45f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(new TargetId(1), Quaternion.AngleAxis(60f, Vector3.up) * Vector3.forward * 2f);
                world.SetHomingCandidate(new TargetId(2), Quaternion.AngleAxis(30f, Vector3.up) * Vector3.forward * 6f);
                world.SetHomingCandidate(new TargetId(3), Quaternion.AngleAxis(10f, Vector3.up) * Vector3.forward * 9f);
                ProjectileId id = Spawn(world, recipe, Vector3.forward);

                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(new TargetId(2), locked.Id);
            }
        }

        [Test]
        public void Homing_DefaultHalfAngleIs90_AndTheOverloadSetsItClamped()
        {
            Assert.AreEqual(90f, new Homing().AcquireHalfAngleDegrees);
            Assert.AreEqual(90f, new Homing(100f).AcquireHalfAngleDegrees);
            var homing = new Homing(100f, 180f);
            Assert.AreEqual(100f, homing.TurnRateDegreesPerSecond);
            Assert.AreEqual(180f, homing.AcquireHalfAngleDegrees);
            Assert.IsInstanceOf<ClosestInTravelDirectionAcquisition>(homing.Acquisition);
            Assert.AreEqual(180f, new Homing(100f, 250f).AcquireHalfAngleDegrees);
            homing.AcquireHalfAngleDegrees = -5f;
            Assert.AreEqual(0f, homing.AcquireHalfAngleDegrees);
        }

        [Test]
        public void ToRecipe_KeepsTheHalfAngle()
        {
            var authored = new Homing(120f, 180f);
            asset = ScriptableObject.CreateInstance<RecipeAsset>();
            asset.Id = "cone-asset";
            asset.OnTick = new IModifier[] { authored };

            Recipe recipe = asset.ToRecipe();

            var copy = (Homing)recipe.OnTick[0];
            Assert.AreNotSame(authored, copy);
            Assert.AreEqual(120f, copy.TurnRateDegreesPerSecond);
            Assert.AreEqual(180f, copy.AcquireHalfAngleDegrees);
            Assert.AreEqual(180f, ((Homing)recipe.Clone().OnTick[0]).AcquireHalfAngleDegrees);
        }

        [Test]
        public void SerializedHomingWithoutTheHalfAngle_Reads90()
        {
            asset = ScriptableObject.CreateInstance<RecipeAsset>();
            asset.OnTick = new IModifier[] { new Homing(120f, 33f) };
            string json = EditorJsonUtility.ToJson(asset);
            Assert.IsTrue(json.Contains("\"acquireHalfAngleDegrees\":33.0"), json);
            string old = json.Replace(",\"acquireHalfAngleDegrees\":33.0", string.Empty);
            Assert.IsFalse(old.Contains("acquireHalfAngleDegrees"), old);

            var loaded = ScriptableObject.CreateInstance<RecipeAsset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(old, loaded);
                var homing = (Homing)loaded.OnTick[0];
                Assert.AreEqual(120f, homing.TurnRateDegreesPerSecond);
                Assert.AreEqual(90f, homing.AcquireHalfAngleDegrees);
            }
            finally
            {
                Object.DestroyImmediate(loaded);
            }
        }

        [Test]
        public void AfterTargetHit_At180_LocksASecondTargetInTheCone()
        {
            var first = new TargetId(1);
            var second = new TargetId(2);
            Recipe recipe = HomingPierceRecipe("cone-retarget-second", 180f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(first, new Vector3(0f, 0f, 1f));
                world.SetHomingCandidate(second, new Vector3(4f, 0f, -6f));
                ProjectileId id = Spawn(world, recipe, Vector3.forward);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, first);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out _, out _));
                Assert.Greater(position.z, 1f);

                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(second, locked.Id);
            }
        }

        [Test]
        public void AfterTargetHit_At180_WithNoOtherTarget_LocksTheFirstAgainOnceBehind()
        {
            var first = new TargetId(1);
            Recipe recipe = HomingPierceRecipe("cone-retarget-first", 180f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(first, new Vector3(0f, 0f, 1f));
                ProjectileId id = Spawn(world, recipe, Vector3.forward);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, first);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);

                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(first, locked.Id);
            }
        }

        [Test]
        public void AfterTargetHit_At180_TheHitTargetStaysIgnoredWhileAhead()
        {
            var first = new TargetId(1);
            Recipe recipe = HomingPierceRecipe("cone-still-ahead", 180f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.SetHomingCandidate(first, new Vector3(0f, 0f, 8f));
                ProjectileId id = Spawn(world, recipe, Vector3.forward);
                world.Tick(0.01f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, first);
                world.Tick(0.01f);
                world.Tick(0.01f);
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        [Test]
        public void MultiplyChild_At180_LocksAnotherTargetBeforeItsParentsHitTarget()
        {
            var hitTarget = new TargetId(1);
            var other = new TargetId(2);
            ProjectileId child = SplitOnHitTarget(hitTarget, other, publishOther: true, out ProjectileWorld world);
            using (world)
            {
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(child, out HomingTarget locked));
                Assert.AreEqual(other, locked.Id);
            }
        }

        [Test]
        public void MultiplyChild_At180_LocksItsParentsHitTargetWhenNoOtherIsInTheCone()
        {
            var hitTarget = new TargetId(1);
            ProjectileId child = SplitOnHitTarget(hitTarget, new TargetId(2), publishOther: false, out ProjectileWorld world);
            using (world)
            {
                world.Tick(0.01f);

                Assert.IsTrue(world.TryGetHomingTarget(child, out HomingTarget locked));
                Assert.AreEqual(hitTarget, locked.Id);
            }
        }

        static ProjectileId SplitOnHitTarget(TargetId hitTarget, TargetId other, bool publishOther, out ProjectileWorld world)
        {
            Recipe child = HomingRecipe("cone-child", 180f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "cone-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(1, child) });
            ScriptedHitQuery hits;
            (world, hits) = SimulationTestHelper.CreateWorld(parent);
            world.Prewarm(child, 4);
            world.SetHomingCandidate(hitTarget, new Vector3(0f, 0f, 5f));
            if (publishOther)
                world.SetHomingCandidate(other, new Vector3(3f, 0f, 10f));

            Spawn(world, parent, Vector3.forward);
            hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4.5f), Vector3.back, hitTarget);
            world.Tick(0.1f);

            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(child.Id, active[0].recipe.Id);
            return active[0].id;
        }

        static ProjectileId SpawnWithOneCandidate(float halfAngle, Vector3 candidate, Vector3 direction, out ProjectileWorld world)
        {
            Recipe recipe = HomingRecipe("cone-one", halfAngle);
            (world, _) = SimulationTestHelper.CreateWorld(recipe);
            world.SetHomingCandidate(new TargetId(1), candidate);
            return Spawn(world, recipe, direction);
        }

        static ProjectileId Spawn(ProjectileWorld world, Recipe recipe, Vector3 direction)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = direction
            });
        }

        static Recipe HomingRecipe(string id, float halfAngle)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(180f, halfAngle) });
        }

        static Recipe HomingPierceRecipe(string id, float halfAngle)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(180f, halfAngle) },
                onHit: new IModifier[] { new Pierce(3) });
        }
    }
}
