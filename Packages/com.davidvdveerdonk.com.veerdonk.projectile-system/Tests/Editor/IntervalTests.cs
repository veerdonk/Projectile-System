using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class IntervalTests
    {
        [Test]
        public void Interval_DrivesEveryModifierOnTheOnIntervalList()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("interval-list-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "interval-list-parent",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Multiply(2, child), new Speed(0.5f) });

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 8);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.25f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(10f, velocity.magnitude, 0.0001f);

                world.Tick(0.25f);
                Assert.AreEqual(3, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(id, out _, out velocity, out _));
                Assert.AreEqual(5f, velocity.magnitude, 0.0001f);
                Assert.AreEqual(2, CountRecipe(world, child.Id));
            }
        }

        [Test]
        public void OnInterval_WithoutInterval_NeverFires()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("no-interval-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "no-interval-parent",
                speed: 10f,
                lifetime: 5f,
                onInterval: new IModifier[] { new Multiply(2, child), new Speed(0.5f) });

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 8);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 8; i++)
                    world.Tick(0.25f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(10f, velocity.magnitude, 0.0001f);
            }
        }

        [Test]
        public void Interval_SetsThePeriodForThatRecipe()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "interval-period",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(1f) },
                onInterval: new IModifier[] { new Speed(0.5f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(10f, velocity.magnitude, 0.0001f);

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetState(id, out _, out velocity, out _));
                Assert.AreEqual(5f, velocity.magnitude, 0.0001f);
            }
        }

        [Test]
        public void Interval_RemovedFromRecipe_DoesNotSurviveOnAPooledEntity()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "interval-recycle",
                speed: 10f,
                lifetime: 0.6f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Speed(0.5f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 3; i++)
                    world.Tick(0.25f);
                Assert.AreEqual(0, world.ActiveCount);

                recipe.OnSpawn = Array.Empty<IModifier>();
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.25f);
                world.Tick(0.25f);

                Assert.IsTrue(world.TryGetState(id, out _, out Vector3 velocity, out _));
                Assert.AreEqual(10f, velocity.magnitude, 0.0001f);
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
    }
}
