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
        static readonly TargetId D = new TargetId(4);
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
        public void AssignedLock_HomingPoseRemoved_ReacquiresTheHitTargetInTheCone_AndHitsIt()
        {
            Recipe recipe = MissileRecipe("reacquire-pose");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                world.SetHomingCandidate(A, AheadA);
                world.SetHitTarget(B, BesideA, 0.5f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHomingCandidate(A);
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
            }
        }

        [Test]
        public void AcquiredLock_TargetRemoved_ReacquiresTheOther()
        {
            Recipe recipe = MissileRecipe("reacquire-acquired");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, AheadA, 0.5f);
                world.SetHitTarget(B, BesideA, 0.5f);
                ProjectileId id = Fire(world, recipe, default);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(A, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
                locks.Clear();

                world.RemoveHitTarget(A);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(B, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
            }
        }

        [Test]
        public void RemovingTheIdFromOneCatalog_KeepsTheLock_WhileTheOtherStillHasIt()
        {
            Recipe recipe = MissileRecipe("reacquire-one-catalog");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, AheadA, 0.5f);
                world.SetHomingCandidate(A, AheadA);
                world.SetHomingCandidate(B, new Vector3(2f, 2f, 0f));
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                world.Tick(0.05f);
                locks.Clear();

                world.RemoveHitTarget(A);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(A, locked.Id);

                world.SetHitTarget(A, AheadA, 0.5f);
                world.RemoveHomingCandidate(A);
                world.Tick(0.05f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(A, locked.Id);
                Assert.AreEqual(0, locks.Count);
            }
        }

        [Test]
        public void AfterAPierceHit_LockRemoved_AnotherCandidateInTheConeWinsOverTheFallback()
        {
            ProjectileId id = PierceThenLockD(out ProjectileWorld world, out List<HomingLockInfo> locks);
            using (world)
            {
                world.SetHomingCandidate(B, new Vector3(3f, 0f, -4f));
                world.RemoveHomingCandidate(D);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(B, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
            }
        }

        [Test]
        public void AfterAPierceHit_LockRemoved_WithNoOtherCandidate_TakesTheFallback()
        {
            ProjectileId id = PierceThenLockD(out ProjectileWorld world, out List<HomingLockInfo> locks);
            using (world)
            {
                world.RemoveHomingCandidate(D);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(C, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(C, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
            }
        }

        [Test]
        public void MultiplyLockRangeChild_AssignedEnemyRemoved_Reacquires()
        {
            var hitTarget = new TargetId(100);
            Recipe child = SimulationTestHelper.CreateRecipe(
                "reacquire-lock-range-child",
                speed: 8f,
                lifetime: 5f,
                radius: 0.2f,
                onTick: new IModifier[] { new Homing(180f) });
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "reacquire-lock-range-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(1, child, 0f, null, MultiplyDirection.Surface, 10f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 4);
                var locks = new List<HomingLockInfo>();
                world.HomingLocked += info => locks.Add(info);
                Vector3 hitPoint = new Vector3(0f, 0f, 5f);
                world.SetHomingCandidate(A, hitPoint + new Vector3(4f, 0f, -1f));
                world.SetHomingCandidate(B, hitPoint + new Vector3(-5f, 0f, -4f));
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, hitPoint, Vector3.back, hitTarget);
                world.Tick(0.1f);

                ProjectileId childId = default;
                int children = 0;
                foreach (var active in SimulationTestHelper.CollectActive(world))
                {
                    if (active.recipe.Id != child.Id)
                        continue;
                    childId = active.id;
                    children++;
                }

                Assert.AreEqual(1, children);
                Assert.IsTrue(world.TryGetHomingTarget(childId, out HomingTarget locked));
                Assert.AreEqual(A, locked.Id);
                locks.Clear();

                world.RemoveHomingCandidate(A);
                world.Tick(0.05f);

                Assert.IsTrue(world.TryGetHomingTarget(childId, out locked));
                Assert.AreEqual(B, locked.Id);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(childId, locks[0].InstanceId);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);
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
        public void BeamAssignedAtSpawn_TargetRemoved_ReacquiresInTheCone_ThenKeepsItLikeASelfAcquiredBeam()
        {
            Recipe recipe = HomingBeam("reacquire-beam-assigned");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                Vector3 nearAimPoint = new Vector3(-4f, 0f, 10f);
                world.SetHomingCandidate(A, new Vector3(0f, 0f, 30f));
                world.SetHitTarget(B, new Vector3(3f, 0f, 6f), 0.5f);
                world.SetHitTarget(C, nearAimPoint, 0.5f);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    HomingTarget = HomingTarget.Assigned(A),
                    HomingAcquirePointSet = true,
                    HomingAcquirePoint = nearAimPoint
                });
                Assert.AreEqual(0, seen.Count);

                world.RemoveHomingCandidate(A);
                locks.Clear();
                world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(B, seen[0].TargetId);
                Assert.AreEqual(1, locks.Count);
                Assert.AreEqual(B, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);

                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(3, seen.Count);
                Assert.AreEqual(B, seen[1].TargetId);
                Assert.AreEqual(B, seen[2].TargetId);
                Assert.AreEqual(1, locks.Count);
                Assert.IsTrue(world.TryGetHomingTarget(id, out _));
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
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
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
                Assert.AreEqual(2, locks.Count);
                Assert.AreEqual(new TargetId(9), locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Assigned, locks[0].Kind);
                Assert.AreEqual(B, locks[1].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[1].Kind);
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

        static ProjectileId PierceThenLockD(out ProjectileWorld world, out List<HomingLockInfo> locks)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "reacquire-fallback",
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(180f, 180f) },
                onHit: new IModifier[] { new Pierce(3) });
            ScriptedHitQuery hits;
            (world, hits) = SimulationTestHelper.CreateWorld(recipe);
            var seen = new List<HomingLockInfo>();
            world.HomingLocked += info => seen.Add(info);
            world.SetHomingCandidate(C, new Vector3(0f, 0f, 1f));
            world.SetHomingCandidate(D, new Vector3(0f, 0f, 30f));
            ProjectileId id = world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });

            hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, C);
            world.Tick(0.05f);
            Assert.IsTrue(world.TryGetState(id, out Vector3 position, out _, out _));
            Assert.Greater(position.z, 1f);

            world.Tick(0.05f);
            Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
            Assert.AreEqual(D, locked.Id);
            seen.Clear();
            locks = seen;
            return id;
        }

        static Recipe HomingBeam(string id)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                id,
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(720f) });
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            return recipe;
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
