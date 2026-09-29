using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class OnHitSpawnPoseTests
    {
        [Test]
        public void MultiplyOnHit_SpawnsChildAtPointAlongNormal_UsingChildRecipe()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(1, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                ProjectileId parentId = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1));

                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out ProjectileId childId, out Vector3 position, out Vector3 velocity, out Recipe recipe));
                Assert.AreNotEqual(parentId, childId);
                Assert.AreEqual(child.Id, recipe.Id);
                Vector3 expected = SimulationTestHelper.OffSurface(point, normal, child.Radius);
                Assert.AreEqual(expected.x, position.x, 0.0001f);
                Assert.AreEqual(expected.y, position.y, 0.0001f);
                Assert.AreEqual(expected.z, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.y, 0.0001f);
                Assert.AreEqual(0f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Multiply0_OnHit_DoesNotSpawnChild()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("child-unused", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "parent-multiply-zero",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(0, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                int availableBefore = world.AvailableCount;

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
            }
        }

        [Test]
        public void Pierce1_Multiply_SpawnsAlongTravel_RegardlessOfListOrder()
        {
            AssertPierceThroughPose(pierceFirst: true);
            AssertPierceThroughPose(pierceFirst: false);
        }

        [Test]
        public void Pierce1_WorldHit_StillSpawnsAlongNormal()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("pierce-world-child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "pierce-world-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new Multiply(1, child) });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.World, point, normal);
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(TryFindRecipe(world, child.Id, out Vector3 position, out Vector3 velocity));
                Vector3 expected = SimulationTestHelper.OffSurface(point, normal, child.Radius);
                Assert.AreEqual(expected.x, position.x, 0.0001f);
                Assert.AreEqual(expected.y, position.y, 0.0001f);
                Assert.AreEqual(expected.z, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.y, 0.0001f);
                Assert.AreEqual(0f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Pierce1_PublishedVolume_SpawnsChildPastTheFarSide()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("pierce-far-child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "pierce-far-parent",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(1), new Multiply(1, child) });

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                Vector3 center = new Vector3(0f, 0f, 4f);
                const float targetRadius = 0.5f;
                world.SetHitTarget(new TargetId(1), center, targetRadius);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.2f);

                Assert.AreEqual(2, world.ActiveCount);
                Assert.IsTrue(TryFindRecipe(world, child.Id, out Vector3 position, out Vector3 velocity));
                Vector3 exit = center + Vector3.forward * targetRadius;
                Vector3 expected = SimulationTestHelper.OffSurface(exit, Vector3.forward, child.Radius);
                Assert.AreEqual(expected.x, position.x, 0.02f);
                Assert.AreEqual(expected.y, position.y, 0.02f);
                Assert.AreEqual(expected.z, position.z, 0.02f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Pierce1_SecondPublishedHit_WithNoCharges_SpawnsAlongNormal()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("pierce-last-child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "pierce-last-parent",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onHit: new IModifier[] { new Pierce(1), new Multiply(1, child) });

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 8);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 2f), 0.5f);
                world.SetHitTarget(new TargetId(2), new Vector3(0f, 0f, 6f), 0.5f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.15f);
                world.Tick(0.25f);

                Assert.AreEqual(2, world.ActiveCount);
                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                int forward = 0;
                int back = 0;
                for (int i = 0; i < active.Count; i++)
                {
                    Assert.AreEqual(child.Id, active[i].recipe.Id);
                    float along = Vector3.Dot(active[i].velocity.normalized, Vector3.forward);
                    if (along > 0.9f)
                        forward++;
                    if (along < -0.9f)
                        back++;
                }

                Assert.AreEqual(1, forward);
                Assert.AreEqual(1, back);
            }
        }

        static void AssertPierceThroughPose(bool pierceFirst)
        {
            Recipe child = SimulationTestHelper.CreateRecipe(
                pierceFirst ? "pierce-travel-child" : "multiply-first-child",
                speed: 8f,
                lifetime: 2f,
                radius: 0.2f);
            IModifier pierce = new Pierce(1);
            IModifier multiply = new Multiply(1, child);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                pierceFirst ? "pierce-travel-parent" : "multiply-first-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: pierceFirst
                    ? new IModifier[] { pierce, multiply }
                    : new IModifier[] { multiply, pierce });

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                hits.Queue(HitKind.Target, point, Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(2, world.ActiveCount);
                Assert.IsTrue(TryFindRecipe(world, child.Id, out Vector3 position, out Vector3 velocity));
                Vector3 expected = SimulationTestHelper.OffSurface(point, Vector3.forward, child.Radius);
                Assert.AreEqual(expected.x, position.x, 0.0001f);
                Assert.AreEqual(expected.y, position.y, 0.0001f);
                Assert.AreEqual(expected.z, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.z, 0.0001f);
            }
        }

        static bool TryFindRecipe(ProjectileWorld world, string recipeId, out Vector3 position, out Vector3 velocity)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id != recipeId)
                    continue;

                position = active[i].position;
                velocity = active[i].velocity;
                return true;
            }

            position = default;
            velocity = default;
            return false;
        }
    }
}
