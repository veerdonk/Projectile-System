using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class SizeSpreadScaleTests
    {
        [Test]
        public void Size2_OnTravel_DoublesRadius()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "size-travel",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                onTravel: new IModifier[] { new Size(2f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetRadius(id, out float radius));
                Assert.AreEqual(0.5f, radius, 1e-4f);
                Assert.AreEqual(0.25f, recipe.Radius, 1e-4f);
            }
        }

        [Test]
        public void Spread2_OnTravel_WithMultiply2_SquareSpread2_PlacesPair4mApart()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("spread-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "spread-parent",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new Spread(2f) },
                onHit: new IModifier[] { new Multiply(2, child, 2f, new SquarePattern()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            world.Prewarm(child, 4);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(0f, 0f, 2f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1));
                world.Tick(0.1f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children =
                    Children(world, child.Id);
                Assert.AreEqual(2, children.Count);
                Assert.AreEqual(4f, Vector3.Distance(children[0].position, children[1].position), 1e-4f);
            }
        }

        [Test]
        public void Spread2_OnTravel_WithMultiplySpread0_StillStacks()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("stack-child", speed: 8f, lifetime: 2f, radius: 0.2f);
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "stack-parent",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new Spread(2f) },
                onHit: new IModifier[] { new Multiply(2, child, 0f, new SquarePattern()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            world.Prewarm(child, 4);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1));
                world.Tick(0.1f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children =
                    Children(world, child.Id);
                Assert.AreEqual(2, children.Count);
                Vector3 expected = SimulationTestHelper.OffSurface(point, normal, child.Radius);
                Assert.AreEqual(expected, children[0].position);
                Assert.AreEqual(expected, children[1].position);
            }
        }

        [Test]
        public void EffectRadius2_OnTravel_DoublesHitInfoEffectRadius()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "effect-radius-travel",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 1f,
                onTravel: new IModifier[] { new EffectRadius(2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                HitInfo? hit = null;
                world.Hit += info => hit = info;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(2f, hit.Value.EffectRadius, 1e-4f);
                Assert.AreEqual(1f, recipe.EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void SizeSpreadEffectRadius_NonPositiveScale_IsNoOp()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("noop-child", speed: 8f, lifetime: 2f);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "scale-noop",
                speed: 20f,
                lifetime: 5f,
                radius: 0.25f,
                effectRadius: 1f,
                onTravel: new IModifier[]
                {
                    new Size(0f),
                    new Size(-1f),
                    new Spread(0f),
                    new EffectRadius(0f),
                    new EffectRadius(-2f)
                },
                onHit: new IModifier[] { new Multiply(2, child, 2f, new SquarePattern()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            world.Prewarm(child, 4);
            using (world)
            {
                HitInfo? hit = null;
                world.Hit += info => hit = info;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetRadius(id, out float radius));
                Assert.AreEqual(0.25f, radius, 1e-4f);
                Assert.IsTrue(world.TryGetSpread(id, out float spread));
                Assert.AreEqual(2f, spread, 1e-4f);
                Assert.IsTrue(world.TryGetCombatStats(id, out _, out _, out _, out float effectRadius));
                Assert.AreEqual(1f, effectRadius, 1e-4f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(1f, hit.Value.EffectRadius, 1e-4f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children =
                    Children(world, child.Id);
                Assert.AreEqual(2, children.Count);
                Assert.AreEqual(2f, Vector3.Distance(children[0].position, children[1].position), 1e-4f);
            }
        }

        [Test]
        public void Size05_OnInterval_ScalesAgainEachCrossing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "size-interval",
                speed: 10f,
                lifetime: 10f,
                radius: 0.8f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Size(0.5f) });
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
                Assert.IsTrue(world.TryGetRadius(id, out float radius));
                Assert.AreEqual(0.4f, radius, 1e-4f);

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetRadius(id, out radius));
                Assert.AreEqual(0.2f, radius, 1e-4f);
            }
        }

        static List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> Children(
            ProjectileWorld world,
            string childId)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            var children = new List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)>();
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id == childId)
                    children.Add(active[i]);
            }

            return children;
        }
    }
}
