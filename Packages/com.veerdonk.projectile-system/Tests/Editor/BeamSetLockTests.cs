using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class BeamSetLockTests
    {
        static readonly TargetId A = new TargetId(1);
        static readonly TargetId B = new TargetId(2);
        static readonly TargetId C = new TargetId(3);
        static readonly Vector3 SideA = new Vector3(6f, 0f, 4f);
        static readonly Vector3 SideB = new Vector3(-6f, 0f, 4f);

        [Test]
        public void AssignedAtSpawn_ThenSetToB_LaterPulsesHitB_NeverA()
        {
            Recipe recipe = HomingBeam("beam-set-b");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(A, SideA, 0.75f);
                world.SetHitTarget(B, SideB, 0.75f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(A));
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(A, seen[0].TargetId);

                world.AimBeam(id, Vector3.zero, Vector3.forward, HomingTarget.Assigned(B));
                int before = seen.Count;
                for (int i = 0; i < 5; i++)
                    world.Tick(0.1f);

                Assert.Greater(seen.Count, before);
                for (int i = before; i < seen.Count; i++)
                    Assert.AreEqual(B, seen[i].TargetId);
            }
        }

        [Test]
        public void AfterSet_ThreeArgumentAimBeam_KeepsB_OffToTheSide()
        {
            Recipe recipe = HomingBeam("beam-set-keep");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(A, new Vector3(0f, 0f, 8f), 0.75f);
                world.SetHitTarget(B, new Vector3(6f, 0f, 0.5f), 0.75f);
                ProjectileId id = Fire(world, recipe);
                Assert.AreEqual(A, seen[0].TargetId);

                world.AimBeam(id, Vector3.zero, Vector3.forward, HomingTarget.Assigned(B));
                world.AimBeam(id, Vector3.zero, Vector3.forward);
                int before = seen.Count;
                world.Tick(0.1f);

                Assert.Greater(seen.Count, before);
                Assert.AreEqual(B, seen[seen.Count - 1].TargetId);
            }
        }

        [Test]
        public void SetThenDefault_SelfAcquiresTheCandidateAhead()
        {
            Recipe recipe = HomingBeam("beam-set-default");
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var locks = new List<HomingLockInfo>();
                world.Hit += info => seen.Add(info);
                world.HomingLocked += info => locks.Add(info);
                world.SetHitTarget(A, new Vector3(1f, 0f, 5f), 0.75f);
                world.SetHitTarget(B, SideB, 0.75f);
                ProjectileId id = Fire(world, recipe, HomingTarget.Assigned(B));
                Assert.AreEqual(B, seen[0].TargetId);

                world.AimBeam(id, Vector3.zero, Vector3.forward, HomingTarget.Assigned(B));
                locks.Clear();
                world.AimBeam(id, Vector3.zero, Vector3.forward, default);
                Assert.AreEqual(1, locks.Count, "the clearing redraw self-acquires A; default itself raises nothing");
                Assert.AreEqual(A, locks[0].TargetId);
                Assert.AreEqual(HomingLockKind.Acquired, locks[0].Kind);

                int before = seen.Count;
                world.Tick(0.1f);
                Assert.Greater(seen.Count, before);
                Assert.AreEqual(A, seen[seen.Count - 1].TargetId);
                Assert.AreEqual(1, locks.Count, "the kept lock raises nothing on the pulse");
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

        static Recipe HomingBeam(string id, IModifier[] onHit = null)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                id,
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTick: new IModifier[] { new Homing(720f) },
                onHit: onHit);
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            return recipe;
        }
    }
}
