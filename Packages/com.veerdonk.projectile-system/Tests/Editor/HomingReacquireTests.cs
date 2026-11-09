using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HomingReacquireTests
    {
        static readonly TargetId A = new TargetId(1);
        static readonly TargetId B = new TargetId(2);
        static readonly TargetId C = new TargetId(3);
        static readonly Vector3 AheadA = new Vector3(0f, 10f, 0f);
        static readonly Vector3 BesideA = new Vector3(6f, 10f, 0f);

        [Test]
        public void AssignedLock_HitTargetRemoved_ReacquiresTheOtherInTheCone_AndHitsIt()
        {
            Recipe recipe = MissileRecipe("reacquire-hit-target");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, AheadA, 0.5f);
                world.SetHitTarget(B, BesideA, 0.5f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHitTarget(A);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(B, locked.Id);

                for (int i = 0; i < 60 && seen.Count == 0; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(B, seen[0].TargetId);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
                Assert.AreEqual(id, locks[0].InstanceId);
            }
        }

        [Test]
        public void AssignedLock_HomingPoseRemoved_ReacquiresTheOtherPose()
        {
            Recipe recipe = MissileRecipe("reacquire-pose");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHomingCandidate(A, AheadA);
                world.SetHomingCandidate(B, BesideA);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHomingCandidate(A);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(B, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.Greater(velocity.x, 0f);
            }
        }

        [Test]
        public void AssignedLock_TargetRemoved_WithNoOtherCandidate_FliesStraightUnlocked()
        {
            Recipe recipe = MissileRecipe("reacquire-none");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, AheadA, 0.5f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHitTarget(A);
                for (int i = 0; i < 5; i++)
                    world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
                Assert.AreEqual(0, locks.Count);
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                Assert.AreEqual(0f, Vector3.Angle(Vector3.up, velocity), 0.0001f);
                Assert.AreEqual(0f, position.x, 1e-4f);
            }
        }

        [Test]
        public void PublishedLock_IsKept_EvenWhenAnotherIsNearer_OrItMovesBehind()
        {
            Recipe recipe = MissileRecipe("reacquire-kept");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, new Vector3(0f, 20f, 0f), 0.5f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.SetHomingCandidate(B, new Vector3(2f, 2f, 0f));
                world.SetHitTarget(C, new Vector3(-2f, 3f, 0f), 0.5f);
                for (int i = 0; i < 3; i++)
                    world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(A, locked.Id);

                world.SetHitTarget(A, new Vector3(5f, -10f, 0f), 0.5f);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(A, locked.Id);
                Assert.AreEqual(0, locks.Count);
            }
        }

        [Test]
        public void Reacquire_DoesNotUseTheAimPointAcquisition()
        {
            Recipe recipe = MissileRecipe("reacquire-not-aim-point");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                Vector3 aimPoint = new Vector3(-6f, 12f, 0f);
                world.SetHitTarget(A, AheadA, 0.5f);
                world.SetHomingCandidate(B, new Vector3(3f, 4f, 0f));
                world.SetHomingCandidate(C, aimPoint);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.up,
                    HomingTarget = HomingTarget.Assigned(A),
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = aimPoint
                });
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHitTarget(A);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(B, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
            }
        }

        [Test]
        public void BeamSetLock_TargetRemoved_KeepsTheLock_AndDoesNotTurnToAnother()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "reacquire-beam-set",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(720f) });
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, new Vector3(6f, 0f, 4f), 0.75f);
                world.SetHitTarget(B, new Vector3(-6f, 0f, 4f), 0.75f);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(B)
                });
                world.AimBeam(id, Vector3.zero, Vector3.forward, HomingTarget.Assigned(B));

                world.RemoveHitTarget(B);
                seen.Clear();
                locks.Clear();
                for (int i = 0; i < 5; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, seen.Count);
                Assert.AreEqual(0, locks.Count);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(B, locked.Id);
            }
        }

        [Test]
        public void Hitscan_AssignedLockNotPublished_KinksToTheCandidateInTheCone()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "reacquire-hitscan",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(40f) });
            recipe.Delivery = Delivery.Hitscan;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(B, new Vector3(3f, 0f, 10f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(new TargetId(9))
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(B, seen[0].TargetId);
            }
        }

        [Test]
        public void CustomAcquisition_PickingAnUnpublishedId_RaisesNoLock_AndFliesStraight()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "reacquire-custom-unpublished",
                speed: 10f,
                lifetime: 10f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(270f, 180f, new PickUnpublished()) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, BesideA, 0.5f);
                ProjectileId id = Fire(world, recipe, default);
                for (int i = 0; i < 5; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(0, locks.Count);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(0f, Vector3.Angle(Vector3.up, velocity), 0.0001f);
            }
        }

        sealed class PickUnpublished : IHomingAcquisition
        {
            public bool TryAcquire(ModifierContext context, out HomingTarget target)
            {
                target = HomingTarget.Assigned(new TargetId(99));
                return true;
            }
        }

        static ProjectileId Fire(ProjectileWorld world, Recipe recipe, HomingTarget homing)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.up,
                HomingTarget = homing
            });
        }

        static Recipe MissileRecipe(string id)
        {
            return SimulationTestHelper.CreateRecipe(
                id,
                speed: 10f,
                lifetime: 10f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(270f, 180f) });
        }
    }
}
