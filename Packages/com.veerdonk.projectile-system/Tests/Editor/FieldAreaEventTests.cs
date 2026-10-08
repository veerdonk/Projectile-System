using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class FieldAreaEventTests
    {
        [Test]
        public void Field_PlantedPulsedEnded_ShareFieldId_AndEmptyPulsesRaise()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-field",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 1f, 3f, intervalSeconds: 0.5f, presentationId: "fx") });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                var pulsed = new List<GroundFieldPulseInfo>();
                var ended = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.GroundFieldPulsed += pulsed.Add;
                world.GroundFieldEnded += ended.Add;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(1f, 4f, 2f),
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, planted.Count);
                Assert.AreEqual(1, pulsed.Count);
                Assert.AreEqual(0, ended.Count);
                Assert.AreEqual(id, planted[0].SourceId);
                Assert.AreEqual(0f, planted[0].Center.y, 1e-4f);
                Assert.AreEqual(2f, planted[0].Radius, 1e-4f);
                Assert.AreEqual(1f, planted[0].LifetimeSeconds, 1e-4f);
                Assert.AreEqual(0.5f, planted[0].IntervalSeconds, 1e-4f);
                Assert.AreEqual("fx", planted[0].PresentationId);
                Assert.IsFalse(planted[0].Occupancy);
                Assert.AreEqual(planted[0].FieldId, pulsed[0].FieldId);
                Assert.AreEqual(0, pulsed[0].TargetsHit);

                world.Tick(0.5f);
                Assert.AreEqual(2, pulsed.Count);
                Assert.AreEqual(planted[0].FieldId, pulsed[1].FieldId);

                world.Tick(0.6f);
                Assert.AreEqual(1, ended.Count);
                Assert.AreEqual(planted[0].FieldId, ended[0].FieldId);
                Assert.AreEqual(GroundFieldEndReason.Expired, ended[0].Reason);
                Assert.AreEqual(planted[0].Center, ended[0].Center);

                int pulseCount = pulsed.Count;
                world.Tick(1f);
                Assert.AreEqual(pulseCount, pulsed.Count);
                Assert.AreEqual(1, ended.Count);
            }
        }

        [Test]
        public void TwoFieldsFromOneShot_HaveDifferentIds()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-two-fields",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[]
                {
                    new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f),
                    new GroundField(1f, 2f, 3f, intervalSeconds: 0.5f)
                });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(2, planted.Count);
                Assert.AreEqual(id, planted[0].SourceId);
                Assert.AreEqual(id, planted[1].SourceId);
                Assert.AreNotEqual(planted[0].FieldId, planted[1].FieldId);
            }
        }

        [Test]
        public void Pulse_IsRaisedAfterItsAreaHits_WithTargetsHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-pulse-order",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var order = new List<string>();
                var pulsed = new List<GroundFieldPulseInfo>();
                world.GroundFieldPlanted += _ => order.Add("planted");
                world.Hit += info => order.Add("hit:" + info.Source);
                world.GroundFieldPulsed += info =>
                {
                    order.Add("pulsed");
                    pulsed.Add(info);
                };
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0.5f, 0f, 0f));
                hits.AddOverlapTarget(new TargetId(2), new Vector3(-0.5f, 0f, 0f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                CollectionAssert.AreEqual(
                    new[] { "planted", "hit:Area", "hit:Area", "pulsed" },
                    order);
                Assert.AreEqual(2, pulsed[0].TargetsHit);
            }
        }

        [Test]
        public void Dispose_EndsLiveFields_WithDisposedReason()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-dispose",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 10f, 3f, intervalSeconds: 0.5f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            var ended = new List<GroundFieldInfo>();
            GroundFieldId plantedId = default;
            world.GroundFieldPlanted += info => plantedId = info.FieldId;
            world.GroundFieldEnded += ended.Add;

            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });
            world.Dispose();

            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(plantedId, ended[0].FieldId);
            Assert.AreEqual(GroundFieldEndReason.Disposed, ended[0].Reason);
        }

        [Test]
        public void OccupancyField_RaisesPlantedAndEnded_ButNeverPulsed()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-occupancy-field",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 1f, 3f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int planted = 0;
                int pulsed = 0;
                int ended = 0;
                bool occupancy = false;
                world.GroundFieldPlanted += info =>
                {
                    planted++;
                    occupancy = info.Occupancy;
                };
                world.GroundFieldPulsed += _ => pulsed++;
                world.GroundFieldEnded += _ => ended++;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);
                world.Tick(0.6f);

                Assert.AreEqual(1, planted);
                Assert.IsTrue(occupancy);
                Assert.AreEqual(0, pulsed);
                Assert.AreEqual(1, ended);
            }
        }

        [Test]
        public void AreaBurst_OnHit_RaisesWithNoTargetInside_AtContactPoint()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-burst-onhit",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                int areaHits = 0;
                world.AreaBurst += bursts.Add;
                world.Hit += info =>
                {
                    if (info.Source == HitSource.Area)
                        areaHits++;
                };

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Vector3 point = new Vector3(0f, 0f, 2f);
                hits.Queue(HitKind.World, point, Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(0, areaHits);
                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(id, bursts[0].InstanceId);
                Assert.AreEqual(point, bursts[0].Center);
                Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
                Assert.AreEqual(RecipeStage.OnHit, bursts[0].Stage);
                Assert.IsFalse(bursts[0].Occupancy);
            }
        }

        [Test]
        public void AreaBurst_IsRaisedBeforeItsAreaHits()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-burst-order",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onTravel: new IModifier[] { new AreaEffect(3f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var order = new List<string>();
                RecipeStage stage = default;
                world.AreaBurst += info =>
                {
                    order.Add("burst");
                    stage = info.Stage;
                };
                world.Hit += info => order.Add("hit:" + info.Source);
                hits.AddOverlapTarget(new TargetId(1), new Vector3(1f, 0f, 0f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                CollectionAssert.AreEqual(new[] { "burst", "hit:Area" }, order);
                Assert.AreEqual(RecipeStage.OnTravel, stage);
            }
        }

        [Test]
        public void AreaBurst_Occupancy_RaisesOnceNotEveryTick()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "event-burst-occupancy",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onTick: new IModifier[] { new AreaEffect(3f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                world.AreaBurst += bursts.Add;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.1f);
                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(1, bursts.Count);
                Assert.IsTrue(bursts[0].Occupancy);
                Assert.AreEqual(RecipeStage.OnTick, bursts[0].Stage);
            }
        }
    }
}
