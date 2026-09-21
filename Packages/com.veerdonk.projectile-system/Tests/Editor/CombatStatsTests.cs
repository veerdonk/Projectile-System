using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class CombatStatsTests
    {
        [Test]
        public void TargetHit_CarriesRecipeCombatStats()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "combat-stats",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                criticalChance: 0.25f,
                effectRadius: 3f);
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
                Assert.AreEqual(5f, hit.Value.Damage, 1e-4f);
                Assert.AreEqual(0.25f, hit.Value.CriticalChance, 1e-4f);
                Assert.AreEqual(3f, hit.Value.EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void RecipeWithoutAuthoredStats_HitDamageIsOne()
        {
            var recipe = new Recipe
            {
                Id = "combat-defaults",
                Speed = 20f,
                LifetimeSeconds = 5f,
                PresentationId = "sphere"
            };
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
                Assert.AreEqual(1f, hit.Value.Damage, 1e-4f);
                Assert.AreEqual(0f, hit.Value.CriticalChance, 1e-4f);
                Assert.AreEqual(0f, hit.Value.EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void HitDamage_ComesFromTheInstanceCopy_NotTheLiveRecipe()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "combat-instance-copy",
                speed: 20f,
                lifetime: 5f,
                damage: 5f);
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

                recipe.Damage = 99f;

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.IsTrue(hit.HasValue);
                Assert.AreEqual(5f, hit.Value.Damage, 1e-4f);
            }
        }

        [Test]
        public void ManaCost_StaysOnTheRecipe_AndTickDoesNotChangeIt()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "combat-mana",
                speed: 20f,
                lifetime: 5f,
                manaCost: 12f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                Assert.AreEqual(12f, recipe.ManaCost, 1e-4f);

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.1f);
                world.Tick(0.1f);
                Assert.AreEqual(12f, recipe.ManaCost, 1e-4f);

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 4f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(12f, recipe.ManaCost, 1e-4f);
            }
        }

        [Test]
        public void ModifierContext_ExposesTheInstanceCombatStats()
        {
            var probe = new StatProbe();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "combat-context",
                speed: 20f,
                lifetime: 5f,
                damage: 5f,
                criticalChance: 0.25f,
                manaCost: 12f,
                effectRadius: 3f,
                onTravel: new IModifier[] { probe });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(5f, probe.Damage, 1e-4f);
                Assert.AreEqual(0.25f, probe.CriticalChance, 1e-4f);
                Assert.AreEqual(12f, probe.ManaCost, 1e-4f);
                Assert.AreEqual(3f, probe.EffectRadius, 1e-4f);
                Assert.AreEqual(0.25f, probe.Radius, 1e-4f);
            }
        }

        sealed class StatProbe : IModifier
        {
            public float Damage { get; private set; }
            public float CriticalChance { get; private set; }
            public float ManaCost { get; private set; }
            public float EffectRadius { get; private set; }
            public float Radius { get; private set; }

            public void Apply(ModifierContext context)
            {
                Damage = context.Damage;
                CriticalChance = context.CriticalChance;
                ManaCost = context.ManaCost;
                EffectRadius = context.EffectRadius;
                Radius = context.Radius;
            }
        }
    }
}
