using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class CombatScaleTests
    {
        [Test]
        public void Damage2_OnTravel_DoublesHitDamage()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "damage-travel",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                onTravel: new IModifier[] { new Damage(2f) });
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
                Assert.AreEqual(10f, hit.Value.Damage, 1e-4f);
                Assert.AreEqual(5f, recipe.Damage, 1e-4f);
            }
        }

        [Test]
        public void Pierce1_ThenDamage2_OnHit_FirstHitUnscaled_SecondDoubled()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "pierce-damage",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                onHit: new IModifier[] { new Pierce(1), new Damage(2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var damages = new List<float>();
                world.Hit += info => damages.Add(info.Damage);

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out _));
                Assert.AreEqual(0, pierce);
                Assert.AreEqual(1, damages.Count);
                Assert.AreEqual(5f, damages[0], 1e-4f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(2));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(2, damages.Count);
                Assert.AreEqual(5f, damages[0], 1e-4f);
                Assert.AreEqual(10f, damages[1], 1e-4f);
            }
        }

        [Test]
        public void Mana05_OnTravel_HalvesInstanceCost_LeavesRecipeUnchanged()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "mana-travel",
                speed: 20f,
                lifetime: 5f,
                manaCost: 12f,
                onTravel: new IModifier[] { new Mana(0.5f) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.IsTrue(world.TryGetManaCost(id, out float manaCost));
                Assert.AreEqual(6f, manaCost, 1e-4f);
                Assert.AreEqual(12f, recipe.ManaCost, 1e-4f);

                world.Tick(0.1f);
                Assert.IsTrue(world.TryGetManaCost(id, out manaCost));
                Assert.AreEqual(6f, manaCost, 1e-4f);
                Assert.AreEqual(12f, recipe.ManaCost, 1e-4f);
            }
        }

        [Test]
        public void Critical2_OnTravel_GoesAboveOne()
        {
            (float crit, float hitCrit, Recipe recipe) = SpawnAndHitCritical(0.75f, new Critical(2f));

            Assert.AreEqual(1.5f, crit, 1e-4f);
            Assert.AreEqual(1.5f, hitCrit, 1e-4f);
            Assert.AreEqual(0.75f, recipe.CriticalChance, 1e-4f);
        }

        [Test]
        public void Critical64_OnTravel_ScalesSmallChanceAboveOne()
        {
            (float crit, float hitCrit, _) = SpawnAndHitCritical(0.025f, new Critical(64f));

            Assert.AreEqual(1.6f, crit, 1e-4f);
            Assert.AreEqual(1.6f, hitCrit, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        public void Critical_NonPositiveScale_LeavesChanceAboveOne(float scale)
        {
            (float crit, float hitCrit, _) = SpawnAndHitCritical(1.5f, new Critical(scale));

            Assert.AreEqual(1.5f, crit, 1e-4f);
            Assert.AreEqual(1.5f, hitCrit, 1e-4f);
        }

        [Test]
        public void Critical2_OnNegativeChance_RaisesToZero()
        {
            (float crit, float hitCrit, _) = SpawnAndHitCritical(-0.5f, new Critical(2f));

            Assert.AreEqual(0f, crit, 1e-4f);
            Assert.AreEqual(0f, hitCrit, 1e-4f);
        }

        static (float crit, float hitCrit, Recipe recipe) SpawnAndHitCritical(float criticalChance, Critical critical)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "critical-scale",
                speed: 20f,
                lifetime: 5f,
                criticalChance: criticalChance,
                onTravel: new IModifier[] { critical });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
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

                Assert.IsTrue(world.TryGetCombatStats(id, out _, out float crit, out _, out _));

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                return (crit, hit.Value.CriticalChance, recipe);
            }
        }

        [Test]
        public void Damage05_OnInterval_ScalesAgainEachCrossing()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "damage-interval",
                speed: 10f,
                lifetime: 10f,
                damage: 8f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new Damage(0.5f) });
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
                Assert.IsTrue(world.TryGetCombatStats(id, out float damage, out _, out _, out _));
                Assert.AreEqual(4f, damage, 1e-4f);

                world.Tick(0.5f);
                Assert.IsTrue(world.TryGetCombatStats(id, out damage, out _, out _, out _));
                Assert.AreEqual(2f, damage, 1e-4f);
            }
        }

        [Test]
        public void CombatScale_NonPositiveScale_IsNoOp()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "scale-noop",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                criticalChance: 0.25f,
                manaCost: 12f,
                onTravel: new IModifier[]
                {
                    new Damage(0f),
                    new Damage(-1f),
                    new Critical(0f),
                    new Mana(0f)
                });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
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

                Assert.IsTrue(world.TryGetCombatStats(id, out float damage, out float crit, out float mana, out _));
                Assert.AreEqual(5f, damage, 1e-4f);
                Assert.AreEqual(0.25f, crit, 1e-4f);
                Assert.AreEqual(12f, mana, 1e-4f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(5f, hit.Value.Damage, 1e-4f);
                Assert.AreEqual(0.25f, hit.Value.CriticalChance, 1e-4f);
            }
        }
    }
}
