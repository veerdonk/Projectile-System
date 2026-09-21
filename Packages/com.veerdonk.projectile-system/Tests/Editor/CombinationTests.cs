using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class CombinationTests
    {
        [Test]
        public void Pierce1_Homing_ReleasesLockOnTargetHit_ThenAcquiresTheNextCandidate()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-homing",
                speed: 10f,
                lifetime: 10f,
                onTick: new IModifier[] { new Homing(90f) },
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var pierced = new TargetId(1);
                var next = new TargetId(2);
                world.SetHomingCandidate(pierced, new Vector3(0f, 0f, 8f));
                world.SetHomingCandidate(next, new Vector3(10f, 0f, 10f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetHomingTarget(id, out HomingTarget locked));
                Assert.AreEqual(pierced, locked.Id);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, pierced);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.IsFalse(locked.IsAssigned);

                world.Tick(0.1f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetHomingTarget(id, out locked));
                Assert.AreEqual(next, locked.Id);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 afterRetarget, out _));
                Assert.AreEqual(9f, Vector3.Angle(Vector3.forward, afterRetarget), 0.05f);
            }
        }

        [Test]
        public void Bounce2_Multiply2_OnHit_SpawnsTwoChildrenPerWorldHit_ChildrenDoNotInherit()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("bounce-multiply-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "bounce-multiply-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(2), new Multiply(2, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false, seed: 9u);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 16);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetRemaining(id, out _, out int bounce));
                Assert.AreEqual(2, bounce);

                Vector3 firstPoint = new Vector3(0f, 0f, 2f);
                Vector3 firstNormal = Vector3.back;
                hits.QueueFor(id, HitKind.World, firstPoint, firstNormal);
                world.Tick(0.1f);

                Assert.AreEqual(3, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out bounce));
                Assert.AreEqual(1, bounce);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Vector3 expected = Vector3.Reflect(Vector3.forward * parent.Speed, firstNormal.normalized);
                Assert.AreEqual(expected.x, velocity.x, 0.0001f);
                Assert.AreEqual(expected.y, velocity.y, 0.0001f);
                Assert.AreEqual(expected.z, velocity.z, 0.0001f);
                Assert.AreEqual(2, CountRecipe(world, child.Id));
                AssertChildrenHaveNoBounce(world, child.Id);

                hits.QueueFor(id, HitKind.World, new Vector3(0f, 0f, 1f), Vector3.forward);
                world.Tick(0.1f);

                Assert.AreEqual(5, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out bounce));
                Assert.AreEqual(0, bounce);
                Assert.AreEqual(4, CountRecipe(world, child.Id));
                AssertChildrenHaveNoBounce(world, child.Id);

                hits.QueueFor(id, HitKind.World, new Vector3(0f, 0f, 0f), Vector3.back);
                world.Tick(0.1f);

                Assert.IsFalse(world.TryGetState(id, out _, out _, out _));
                Assert.AreEqual(6, world.ActiveCount);
                Assert.AreEqual(6, CountRecipe(world, child.Id));
            }
        }

        static int CountRecipe(ProjectileWorld world, string recipeId)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            int count = 0;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id == recipeId)
                    count++;
            }

            return count;
        }

        static void AssertChildrenHaveNoBounce(ProjectileWorld world, string childRecipeId)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id != childRecipeId)
                    continue;

                Assert.IsTrue(world.TryGetRemaining(active[i].id, out _, out int bounce));
                Assert.AreEqual(0, bounce);
            }
        }
    }
}
