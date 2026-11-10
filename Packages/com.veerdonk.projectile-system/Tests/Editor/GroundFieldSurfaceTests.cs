using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class GroundFieldSurfaceTests
    {
        const float TargetRadius = 0.4f;

        static readonly Vector3 Ground = new Vector3(0f, 3f, 0f);
        static readonly Vector3 Slope = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.up;
        static readonly Vector3 Downhill = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.forward;

        sealed class FixedSurface : IGroundFieldSurface
        {
            readonly bool found;
            readonly Vector3 point;
            readonly Vector3 normal;

            public FixedSurface(bool found, Vector3 point, Vector3 normal)
            {
                this.found = found;
                this.point = point;
                this.normal = normal;
            }

            public readonly List<Vector3> Asked = new List<Vector3>();

            public bool TryGetGround(Vector3 query, out Vector3 groundPoint, out Vector3 groundNormal)
            {
                Asked.Add(query);
                groundPoint = point;
                groundNormal = normal;
                return found;
            }
        }

        sealed class AreaSurface : IGroundFieldAreaSurface
        {
            readonly bool found;
            readonly Vector3 point;
            readonly Vector3 normal;

            public AreaSurface(bool found, Vector3 point, Vector3 normal)
            {
                this.found = found;
                this.point = point;
                this.normal = normal;
            }

            public readonly List<float> Radii = new List<float>();
            public readonly List<Vector3> Asked = new List<Vector3>();
            public int PointOnlyCalls;

            public bool TryGetGround(Vector3 query, float radius, out Vector3 groundPoint, out Vector3 groundNormal)
            {
                Asked.Add(query);
                Radii.Add(radius);
                groundPoint = point;
                groundNormal = normal;
                return found;
            }

            public bool TryGetGround(Vector3 query, out Vector3 groundPoint, out Vector3 groundNormal)
            {
                PointOnlyCalls++;
                groundPoint = Vector3.zero;
                groundNormal = Vector3.up;
                return false;
            }
        }

        static Recipe TravelField(float heightMargin)
        {
            return SimulationTestHelper.CreateRecipe(
                "ground-surface",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f, heightMargin: heightMargin) });
        }

        static ProjectileId FireUp(ProjectileWorld world, Recipe recipe)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = new Vector3(0f, 10f, 0f),
                Direction = Vector3.up
            });
        }

        static void StandOnCapsule(ProjectileWorld world, TargetId id, Vector3 bottom)
        {
            world.SetHitTarget(id, bottom, TargetRadius, 1.8f, 1f);
        }

        static List<TargetId> AreaTargets(List<HitInfo> seen)
        {
            var ids = new List<TargetId>();
            for (int i = 0; i < seen.Count; i++)
            {
                if (seen[i].Source == HitSource.Area)
                    ids.Add(seen[i].TargetId);
            }

            return ids;
        }

        [Test]
        public void TiltedField_HitsCapsulesOnThePlane_AtUphillAndDownhillEdges()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var uphill = new TargetId(1);
                var downhill = new TargetId(2);
                var pastUphill = new TargetId(3);
                StandOnCapsule(world, uphill, Ground - Downhill * 2.2f);
                StandOnCapsule(world, downhill, Ground + Downhill * 2.2f);
                StandOnCapsule(world, pastUphill, Ground - Downhill * 2.6f);
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                List<TargetId> hit = AreaTargets(seen);
                CollectionAssert.AreEquivalent(new[] { uphill, downhill }, hit);
            }
        }

        [Test]
        public void TiltedField_MissesCapsuleInsideXZRadius_TwoMetersBelowThePlane()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var below = new TargetId(1);
                var onPlane = new TargetId(2);
                var scriptedBelow = new TargetId(3);
                StandOnCapsule(world, below, new Vector3(0.5f, Ground.y - 2f, 0f));
                StandOnCapsule(world, onPlane, new Vector3(0.5f, Ground.y, 0f));
                hits.AddOverlapTarget(scriptedBelow, new Vector3(-0.5f, Ground.y - 2f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                CollectionAssert.AreEquivalent(new[] { onPlane }, AreaTargets(seen));
            }
        }

        [Test]
        public void HeightMarginZero_DoesNotTestHeight()
        {
            Recipe recipe = TravelField(0f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var below = new TargetId(1);
                StandOnCapsule(world, below, new Vector3(0.5f, Ground.y - 2f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                CollectionAssert.AreEquivalent(new[] { below }, AreaTargets(seen));
            }
        }

        [Test]
        public void HeightMargin_WithoutSurface_TestsTheGroundPlane()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var standing = new TargetId(1);
                var ledge = new TargetId(2);
                hits.AddOverlapTarget(standing, new Vector3(1f, 0.5f, 0f));
                hits.AddOverlapTarget(ledge, new Vector3(-1f, 3f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                CollectionAssert.AreEquivalent(new[] { standing }, AreaTargets(seen));
            }
        }

        [Test]
        public void Events_ReportGroundPointAndNormal_OnPlantedPulsedAndEnded()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new FixedSurface(true, Ground, Slope * 4f);
                world.GroundFieldSurface = surface;
                var planted = new List<GroundFieldInfo>();
                var pulsed = new List<GroundFieldPulseInfo>();
                var ended = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.GroundFieldPulsed += pulsed.Add;
                world.GroundFieldEnded += ended.Add;

                FireUp(world, recipe);
                for (int i = 0; i < 5; i++)
                    world.Tick(0.5f);

                Assert.AreEqual(1, surface.Asked.Count);
                Assert.AreEqual(new Vector3(0f, 10f, 0f), surface.Asked[0]);
                Assert.AreEqual(1, planted.Count);
                Assert.Greater(pulsed.Count, 1);
                Assert.AreEqual(1, ended.Count);
                AssertVector(Ground, planted[0].Center);
                AssertVector(Slope, planted[0].Normal);
                Assert.AreEqual(1f, planted[0].HeightMargin, 1e-5f);
                for (int i = 0; i < pulsed.Count; i++)
                {
                    AssertVector(Ground, pulsed[i].Center);
                    AssertVector(Slope, pulsed[i].Normal);
                }

                AssertVector(Ground, ended[0].Center);
                AssertVector(Slope, ended[0].Normal);
            }
        }

        [Test]
        public void SurfaceReturningFalse_PlantsAtYZero_NormalUp()
        {
            Recipe recipe = TravelField(0f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(false, Ground, Slope);
                var high = new TargetId(1);
                hits.AddOverlapTarget(high, new Vector3(1f, 6f, 0f));
                var planted = new List<GroundFieldInfo>();
                var seen = new List<HitInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.Hit += seen.Add;

                FireUp(world, recipe);

                AssertVector(Vector3.zero, planted[0].Center);
                AssertVector(Vector3.up, planted[0].Normal);
                CollectionAssert.AreEquivalent(new[] { high }, AreaTargets(seen));
            }
        }

        [Test]
        public void NoSurface_ReportsNormalUp_AtYZero()
        {
            Recipe recipe = TravelField(0f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                FireUp(world, recipe);

                AssertVector(Vector3.zero, planted[0].Center);
                AssertVector(Vector3.up, planted[0].Normal);
                Assert.AreEqual(0f, planted[0].HeightMargin);
            }
        }

        [Test]
        public void Normal_FacingDown_IsFlipped_AndNearHorizontal_BecomesUp()
        {
            Recipe recipe = TravelField(0f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                world.GroundFieldSurface = new FixedSurface(true, Ground, -Slope);
                FireUp(world, recipe);
                world.GroundFieldSurface = new FixedSurface(true, Ground, new Vector3(1f, 0.05f, 0f));
                FireUp(world, recipe);
                world.GroundFieldSurface = new FixedSurface(true, Ground, Vector3.zero);
                FireUp(world, recipe);

                Assert.AreEqual(3, planted.Count);
                AssertVector(Slope, planted[0].Normal);
                AssertVector(Vector3.up, planted[1].Normal);
                AssertVector(Vector3.up, planted[2].Normal);
            }
        }

        [Test]
        public void DespawnOnExpire_AsksTheSurface_AtTheInstancePosition()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-surface-despawn",
                speed: 0f,
                lifetime: 5f,
                onExpire: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f, heightMargin: 1f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new FixedSurface(true, Ground, Slope);
                world.GroundFieldSurface = surface;
                var onPlane = new TargetId(1);
                StandOnCapsule(world, onPlane, Ground + Downhill * 1.5f);
                var planted = new List<GroundFieldInfo>();
                var seen = new List<HitInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.Hit += seen.Add;

                Vector3 cast = new Vector3(0f, 12f, 0f);
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = cast,
                    Direction = Vector3.up
                });
                world.Despawn(id);

                Assert.AreEqual(1, surface.Asked.Count);
                AssertVector(cast, surface.Asked[0]);
                AssertVector(Ground, planted[0].Center);
                CollectionAssert.AreEquivalent(new[] { onPlane }, AreaTargets(seen));
            }
        }

        [Test]
        public void TiltedField_MissesCapsuleOnALedgeAbove()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var ledge = new TargetId(1);
                var onPlane = new TargetId(2);
                StandOnCapsule(world, ledge, new Vector3(0.5f, Ground.y + 2f, 0f));
                StandOnCapsule(world, onPlane, new Vector3(-0.5f, Ground.y, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                CollectionAssert.AreEquivalent(new[] { onPlane }, AreaTargets(seen));
            }
        }

        [Test]
        public void HeightMargin_IsMeasuredAlongTheNormal()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var verticallyAbove = new TargetId(1);
                var inside = new TargetId(2);
                var outside = new TargetId(3);
                hits.AddOverlapTarget(verticallyAbove, Ground + Vector3.up * 1.1f);
                hits.AddOverlapTarget(inside, Ground + Vector3.right * 0.5f + Slope * 0.9f);
                hits.AddOverlapTarget(outside, Ground - Vector3.right * 0.5f + Slope * 1.1f);
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);

                CollectionAssert.AreEquivalent(new[] { verticallyAbove, inside }, AreaTargets(seen));
            }
        }

        [Test]
        public void OnHit_AsksTheSurface_AtTheContactPoint()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-surface-onhit",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f, heightMargin: 1f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new FixedSurface(true, Ground, Slope);
                world.GroundFieldSurface = surface;
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Vector3 contact = new Vector3(0f, 4f, 1.5f);
                hits.Queue(HitKind.World, contact, Vector3.back);
                world.Tick(0.1f);

                Assert.AreEqual(1, surface.Asked.Count);
                AssertVector(contact, surface.Asked[0]);
                AssertVector(Ground, planted[0].Center);
                AssertVector(Slope, planted[0].Normal);
            }
        }

        [Test]
        public void OccupancyOnTiltedField_EntersOnlyTargetsOnThePlane()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-surface-occupancy",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f, occupancy: true, heightMargin: 1f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, Ground, Slope);
                var onPlane = new TargetId(1);
                var below = new TargetId(2);
                StandOnCapsule(world, onPlane, Ground + Downhill * 1.5f);
                StandOnCapsule(world, below, new Vector3(0.5f, Ground.y - 2f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                FireUp(world, recipe);
                world.Tick(0.1f);
                StandOnCapsule(world, onPlane, new Vector3(0.5f, Ground.y - 2f, 0f));
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.OccupancyEnter, seen[0].Source);
                Assert.AreEqual(onPlane, seen[0].TargetId);
                Assert.AreEqual(HitSource.OccupancyExit, seen[1].Source);
                Assert.AreEqual(onPlane, seen[1].TargetId);
            }
        }

        [Test]
        public void NonFiniteGroundPoint_PlantsAtYZero_NormalUp()
        {
            Recipe recipe = TravelField(0f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.GroundFieldSurface = new FixedSurface(true, new Vector3(float.NaN, 3f, 0f), Slope);
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                FireUp(world, recipe);

                AssertVector(Vector3.zero, planted[0].Center);
                AssertVector(Vector3.up, planted[0].Normal);
            }
        }

        [Test]
        public void HeightMargin_IsClampedAndCloned()
        {
            var field = new GroundField(2f, 2f, 3f, heightMargin: -1f);
            Assert.AreEqual(0f, field.HeightMargin);
            field.HeightMargin = float.PositiveInfinity;
            Assert.AreEqual(0f, field.HeightMargin);
            field.HeightMargin = 1.5f;

            Recipe recipe = SimulationTestHelper.CreateRecipe("ground-margin-clone", onHit: new IModifier[] { field });
            var copy = (GroundField)recipe.Clone().OnHit[0];

            Assert.AreNotSame(field, copy);
            Assert.AreEqual(1.5f, copy.HeightMargin);
        }

        [Test]
        public void AreaSurface_GetsTheFieldRadius_AndItsGroundIsUsed()
        {
            Recipe recipe = TravelField(1f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new AreaSurface(true, Ground, -Slope * 3f);
                world.GroundFieldSurface = surface;
                var uphill = new TargetId(1);
                StandOnCapsule(world, uphill, Ground - Downhill * 2.2f);
                var planted = new List<GroundFieldInfo>();
                var seen = new List<HitInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.Hit += seen.Add;

                FireUp(world, recipe);

                Assert.AreEqual(0, surface.PointOnlyCalls);
                CollectionAssert.AreEqual(new[] { 2f }, surface.Radii);
                Assert.AreEqual(new Vector3(0f, 10f, 0f), surface.Asked[0]);
                AssertVector(Ground, planted[0].Center);
                AssertVector(Slope, planted[0].Normal);
                CollectionAssert.AreEquivalent(new[] { uphill }, AreaTargets(seen));
            }
        }

        [Test]
        public void AreaSurface_IsAskedWithEachFieldsOwnRadius()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-area-radii",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new GroundField(2f, 2f, 3f), new GroundField(3.5f, 2f, 3f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new AreaSurface(true, Ground, Vector3.up);
                world.GroundFieldSurface = surface;

                FireUp(world, recipe);

                CollectionAssert.AreEqual(new[] { 2f, 3.5f }, surface.Radii);
            }
        }

        [Test]
        public void AreaSurface_ReturningFalseOrNonFinite_PlantsAtYZero_NormalUp()
        {
            Recipe recipe = TravelField(0f);
            var surfaces = new[]
            {
                new AreaSurface(false, Ground, Slope),
                new AreaSurface(true, new Vector3(0f, float.PositiveInfinity, 0f), Slope)
            };
            for (int s = 0; s < surfaces.Length; s++)
            {
                (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
                using (world)
                {
                    world.GroundFieldSurface = surfaces[s];
                    var planted = new List<GroundFieldInfo>();
                    world.GroundFieldPlanted += planted.Add;

                    FireUp(world, recipe);

                    Assert.AreEqual(0, surfaces[s].PointOnlyCalls);
                    AssertVector(Vector3.zero, planted[0].Center);
                    AssertVector(Vector3.up, planted[0].Normal);
                }
            }
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.Less((expected - actual).magnitude, 1e-4f, $"expected {expected}, got {actual}");
        }
    }
}
