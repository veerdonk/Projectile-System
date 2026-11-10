using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PulseLimitTests
    {
        sealed class RawAreaOverlap : IModifier
        {
            readonly bool occupancy;

            public RawAreaOverlap(bool occupancy)
            {
                this.occupancy = occupancy;
            }

            public void Apply(ModifierContext context)
            {
                context.RequestAreaOverlap(context.Position, 3f, null, occupancy);
            }
        }

        sealed class CountRuns : IModifier
        {
            public int Runs;

            public void Apply(ModifierContext context)
            {
                Runs++;
            }
        }

        [TestCase(float.NaN, false)]
        [TestCase(float.NaN, true)]
        [TestCase(float.PositiveInfinity, false)]
        [TestCase(float.PositiveInfinity, true)]
        public void NonFiniteEffectRadius_BurstsNothing(float effectRadius, bool occupancy)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-nan",
                speed: 20f,
                lifetime: 5f,
                effectRadius: effectRadius,
                onTravel: new IModifier[]
                {
                    new AreaEffect(3f, occupancy: occupancy),
                    new AreaEffect(3f, 2f, occupancy: occupancy),
                    new RawAreaOverlap(occupancy)
                });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                hits.AddOverlapTarget(new TargetId(1), Vector3.zero);
                var bursts = new List<AreaBurstInfo>();
                var seen = new List<HitInfo>();
                world.AreaBurst += bursts.Add;
                world.Hit += seen.Add;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.up
                });
                world.Tick(0.1f);

                Assert.Greater(bursts.Count, 0);
                for (int i = 0; i < bursts.Count; i++)
                    Assert.AreEqual(2f, bursts[i].Radius, "only the AreaEffect with its own finite radius bursts");
                for (int i = 0; i < seen.Count; i++)
                    Assert.AreEqual(2f, seen[i].EffectRadius);
            }
        }

        [Test]
        public void GroundField_IntervalBelowMinimum_PulsesEveryHundredthOfASecond()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-tiny-interval",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 5f, 3f, intervalSeconds: 1e-6f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                var pulsed = new List<GroundFieldPulseInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.GroundFieldPulsed += pulsed.Add;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Despawn(id);
                world.Tick(1f);

                Assert.AreEqual(0.01f, planted[0].IntervalSeconds, 1e-6f);
                Assert.That(pulsed.Count, Is.InRange(100, 102));
            }
        }

        [Test]
        public void Interval_BelowMinimum_FiresEveryHundredthOfASecond()
        {
            var counter = new CountRuns();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "interval-tiny",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(1e-6f) },
                onInterval: new IModifier[] { counter });
            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.1f);

                Assert.That(counter.Runs, Is.InRange(9, 11));
            }
        }

        [Test]
        public void Interval_NaN_NeverFires()
        {
            var counter = new CountRuns();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "interval-nan",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(float.NaN) },
                onInterval: new IModifier[] { counter });
            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                for (int i = 0; i < 5; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, counter.Runs);
            }
        }
    }
}
