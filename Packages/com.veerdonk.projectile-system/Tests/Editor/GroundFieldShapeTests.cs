using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class GroundFieldShapeTests
    {
        const float Tolerance = 1e-4f;

        static readonly Vector3 Ground = new Vector3(0f, 3f, 0f);
        static readonly Vector3 Slope = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.up;
        static readonly Vector3 Downhill = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.forward;

        sealed class AreaSurface : IGroundFieldAreaSurface
        {
            readonly Vector3 point;
            readonly Vector3 normal;

            public AreaSurface(Vector3 point, Vector3 normal)
            {
                this.point = point;
                this.normal = normal;
            }

            public readonly List<Vector3> Asked = new List<Vector3>();
            public readonly List<float> Radii = new List<float>();

            public bool TryGetGround(Vector3 query, float radius, out Vector3 groundPoint, out Vector3 groundNormal)
            {
                Asked.Add(query);
                Radii.Add(radius);
                groundPoint = point;
                groundNormal = normal;
                return true;
            }

            public bool TryGetGround(Vector3 query, out Vector3 groundPoint, out Vector3 groundNormal)
            {
                return TryGetGround(query, 0f, out groundPoint, out groundNormal);
            }
        }

        static GroundField Rectangle(float length, float width, bool occupancy = false)
        {
            return new GroundField(0f, 2f, 3f, intervalSeconds: 0.5f, occupancy: occupancy)
            {
                Shape = GroundFieldShape.Rectangle,
                Length = length,
                Width = width
            };
        }

        static GroundField Sector(float length, float angleDegrees)
        {
            return new GroundField(0f, 2f, 3f, intervalSeconds: 0.5f)
            {
                Shape = GroundFieldShape.Sector,
                Length = length,
                AngleDegrees = angleDegrees
            };
        }

        static Recipe Carrier(GroundField field, float speed = 0f)
        {
            return SimulationTestHelper.CreateRecipe(
                "ground-shape",
                speed: speed,
                lifetime: 5f,
                onExpire: new IModifier[] { field });
        }

        static void PlantAt(ProjectileWorld world, Recipe recipe, Vector3 position, Vector3 direction)
        {
            ProjectileId id = world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction
            });
            world.Despawn(id);
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

        static List<TargetId> HitBy(GroundField field, Vector3 direction, params Vector3[] bodies)
        {
            Recipe recipe = Carrier(field);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                for (int i = 0; i < bodies.Length; i++)
                    hits.AddOverlapTarget(new TargetId(i + 1), bodies[i]);
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                PlantAt(world, recipe, Vector3.zero, direction);
                return AreaTargets(seen);
            }
        }

        static Vector3 OnBearing(float degrees, float distance)
        {
            return Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.forward * distance;
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, Tolerance, "z");
        }

        [Test]
        public void Rectangle_StartsAtThePlantPoint_AndRunsAlongTheDirection()
        {
            List<TargetId> hit = HitBy(
                Rectangle(6f, 2f),
                Vector3.right,
                new Vector3(0.1f, 0f, 0.9f),
                new Vector3(5.9f, 0f, -0.9f),
                new Vector3(6.2f, 0f, 0f),
                new Vector3(-0.2f, 0f, 0f),
                new Vector3(3f, 0f, 1.2f),
                new Vector3(0f, 0f, 3f));

            CollectionAssert.AreEquivalent(new[] { new TargetId(1), new TargetId(2) }, hit);
        }

        [Test]
        public void Sector_HitsInsideTheAngleAndRadius_FromTheApex()
        {
            List<TargetId> hit = HitBy(
                Sector(5f, 60f),
                Vector3.forward,
                OnBearing(0f, 4.9f),
                OnBearing(28f, 4f),
                OnBearing(-28f, 1f),
                OnBearing(32f, 4f),
                OnBearing(0f, 5.2f),
                OnBearing(180f, 1f));

            CollectionAssert.AreEquivalent(new[] { new TargetId(1), new TargetId(2), new TargetId(3) }, hit);
        }

        [Test]
        public void Sector_WiderThanAHalfTurn_HitsBesideAndBehindWithinItsAngle()
        {
            List<TargetId> hit = HitBy(
                Sector(5f, 270f),
                Vector3.forward,
                OnBearing(90f, 3f),
                OnBearing(130f, 3f),
                OnBearing(-130f, 3f),
                OnBearing(140f, 3f),
                OnBearing(180f, 3f));

            CollectionAssert.AreEquivalent(new[] { new TargetId(1), new TargetId(2), new TargetId(3) }, hit);
        }

        [Test]
        public void Sector_OfAFullTurnOrMore_IsADisk()
        {
            List<TargetId> hit = HitBy(
                Sector(5f, 400f),
                Vector3.forward,
                OnBearing(180f, 4.9f),
                OnBearing(90f, 4.9f),
                OnBearing(180f, 5.1f));

            CollectionAssert.AreEquivalent(new[] { new TargetId(1), new TargetId(2) }, hit);
        }

        [Test]
        public void ShapedField_PointsAlongTravel_WhenTheCarrierMoves()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-shape-travel",
                speed: 10f,
                lifetime: 5f,
                onTravel: new IModifier[] { Rectangle(6f, 2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(-4f, 0f, 0f));
                hits.AddOverlapTarget(new TargetId(2), new Vector3(4f, 0f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1f, 0f),
                    Direction = new Vector3(-1f, 1f, 0f)
                });

                Assert.AreEqual(1, planted.Count);
                AssertVector(Vector3.left, planted[0].Direction);
                CollectionAssert.AreEquivalent(new[] { new TargetId(1) }, AreaTargets(seen));
            }
        }

        [Test]
        public void ShapedField_FallingStraightDown_KeepsTheAimDirection()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ground-shape-fall",
                speed: 0f,
                lifetime: 5f,
                gravity: 9.81f,
                onExpire: new IModifier[] { Rectangle(4f, 1f) });
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 5f, 0f),
                    Direction = Vector3.right
                });
                world.Tick(0.1f);
                world.Despawn(id);

                Assert.AreEqual(1, planted.Count);
                AssertVector(Vector3.right, planted[0].Direction);
            }
        }

        [Test]
        public void ShapedField_StraightUpOrDown_PointsWorldForward()
        {
            Recipe recipe = Carrier(Rectangle(4f, 1f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                PlantAt(world, recipe, Vector3.zero, Vector3.down);

                AssertVector(Vector3.forward, planted[0].Direction);
            }
        }

        [Test]
        public void Events_CarryTheShapeAndItsMeasures()
        {
            Recipe recipe = Carrier(Rectangle(6f, 2f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                var pulsed = new List<GroundFieldPulseInfo>();
                var ended = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.GroundFieldPulsed += pulsed.Add;
                world.GroundFieldEnded += ended.Add;

                PlantAt(world, recipe, new Vector3(1f, 2f, 1f), Vector3.right);
                for (int i = 0; i < 5; i++)
                    world.Tick(0.5f);

                Assert.AreEqual(1, planted.Count);
                Assert.Greater(pulsed.Count, 1);
                Assert.AreEqual(1, ended.Count);
                GroundFieldInfo info = planted[0];
                Assert.AreEqual(GroundFieldShape.Rectangle, info.Shape);
                AssertVector(new Vector3(1f, 0f, 1f), info.Center);
                AssertVector(Vector3.right, info.Direction);
                Assert.AreEqual(6f, info.Length, Tolerance);
                Assert.AreEqual(2f, info.Width, Tolerance);
                Assert.AreEqual(0f, info.AngleDegrees, Tolerance);
                Assert.AreEqual(Mathf.Sqrt(37f), info.Radius, Tolerance);
                Assert.AreEqual(GroundFieldShape.Rectangle, pulsed[0].Shape);
                AssertVector(Vector3.right, pulsed[0].Direction);
                Assert.AreEqual(6f, pulsed[0].Length, Tolerance);
                Assert.AreEqual(2f, pulsed[0].Width, Tolerance);
                Assert.AreEqual(GroundFieldShape.Rectangle, ended[0].Shape);
                Assert.AreEqual(6f, ended[0].Length, Tolerance);
            }
        }

        [Test]
        public void SectorEvents_ReportTheCappedAngle_AndTheLengthAsRadius()
        {
            Recipe recipe = Carrier(Sector(5f, 500f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                PlantAt(world, recipe, Vector3.zero, Vector3.forward);

                Assert.AreEqual(GroundFieldShape.Sector, planted[0].Shape);
                Assert.AreEqual(360f, planted[0].AngleDegrees, Tolerance);
                Assert.AreEqual(5f, planted[0].Length, Tolerance);
                Assert.AreEqual(5f, planted[0].Radius, Tolerance);
                Assert.AreEqual(0f, planted[0].Width, Tolerance);
            }
        }

        [Test]
        public void Disk_StaysTheDefault_WithNoShapeMeasures()
        {
            var field = new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f);
            Assert.AreEqual(GroundFieldShape.Disk, field.Shape);
            Recipe recipe = Carrier(field);
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new AreaSurface(Ground, Vector3.up);
                world.GroundFieldSurface = surface;
                var planted = new List<GroundFieldInfo>();
                world.GroundFieldPlanted += planted.Add;

                PlantAt(world, recipe, new Vector3(1f, 5f, 1f), Vector3.right);

                AssertVector(new Vector3(1f, 5f, 1f), surface.Asked[0]);
                Assert.AreEqual(2f, surface.Radii[0], Tolerance);
                Assert.AreEqual(GroundFieldShape.Disk, planted[0].Shape);
                Assert.AreEqual(2f, planted[0].Radius, Tolerance);
                Assert.AreEqual(0f, planted[0].Length);
                Assert.AreEqual(0f, planted[0].Width);
                Assert.AreEqual(0f, planted[0].AngleDegrees);
                AssertVector(Ground, planted[0].Center);
            }
        }

        [Test]
        public void ShapedField_AsksTheSurfaceAtItsMiddle_AndLiesOnTheReturnedPlane()
        {
            Recipe recipe = Carrier(Rectangle(4f, 2f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new AreaSurface(Ground + Downhill * 2f, Slope);
                world.GroundFieldSurface = surface;
                var inside = new TargetId(1);
                var past = new TargetId(2);
                world.SetHitTarget(inside, Ground + Downhill * 3.5f, 0.1f, 1.8f, 1f);
                world.SetHitTarget(past, Ground + Downhill * 4.5f, 0.1f, 1.8f, 1f);
                var planted = new List<GroundFieldInfo>();
                var seen = new List<HitInfo>();
                world.GroundFieldPlanted += planted.Add;
                world.Hit += seen.Add;

                PlantAt(world, recipe, new Vector3(0f, 6f, 0f), Vector3.forward);

                Assert.AreEqual(1, surface.Asked.Count);
                AssertVector(new Vector3(0f, 6f, 2f), surface.Asked[0]);
                Assert.AreEqual(Mathf.Sqrt(5f), surface.Radii[0], Tolerance);
                AssertVector(Ground, planted[0].Center);
                AssertVector(Slope, planted[0].Normal);
                AssertVector(Downhill, planted[0].Direction);
                CollectionAssert.AreEquivalent(new[] { inside }, AreaTargets(seen));
            }
        }

        [Test]
        public void SectorSurfaceRadius_CoversTheShape_AroundItsMiddle()
        {
            Recipe recipe = Carrier(Sector(4f, 180f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var surface = new AreaSurface(Vector3.zero, Vector3.up);
                world.GroundFieldSurface = surface;

                PlantAt(world, recipe, Vector3.zero, Vector3.forward);

                AssertVector(new Vector3(0f, 0f, 2f), surface.Asked[0]);
                Assert.AreEqual(Mathf.Sqrt(20f), surface.Radii[0], Tolerance);
            }
        }

        [Test]
        public void PublishedCapsule_CountsItsRadius_AtTheEdges()
        {
            Recipe recipe = Carrier(Rectangle(6f, 2f));
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var touching = new TargetId(1);
                var clear = new TargetId(2);
                var pastCorner = new TargetId(3);
                world.SetHitTarget(touching, new Vector3(3f, 0f, 1.3f), 0.4f, 1.8f, 1f);
                world.SetHitTarget(clear, new Vector3(3f, 0f, 1.5f), 0.4f, 1.8f, 1f);
                world.SetHitTarget(pastCorner, new Vector3(6.3f, 0f, 1.3f), 0.4f, 1.8f, 1f);
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                PlantAt(world, recipe, Vector3.zero, Vector3.right);

                CollectionAssert.AreEquivalent(new[] { touching }, AreaTargets(seen));
            }
        }

        [Test]
        public void SectorContains_WithExtraRadius_MeasuresToTheNearerEdge()
        {
            GroundDisk sector = GroundDisk.Shaped(
                Vector3.zero,
                Vector3.up,
                Vector3.forward,
                new GroundFieldSize(GroundFieldShape.Sector, 0f, 5f, 0f, 60f),
                0f);
            Vector3 edge = OnBearing(30f, 3f);
            Vector3 outward = Quaternion.AngleAxis(90f, Vector3.up) * OnBearing(30f, 1f);

            Assert.IsTrue(sector.Contains(edge + outward * 0.3f, Vector3.zero, 0.4f, out _));
            Assert.IsFalse(sector.Contains(edge + outward * 0.5f, Vector3.zero, 0.4f, out _));
            Assert.IsFalse(sector.Contains(edge + outward * 0.3f, Vector3.zero, 0f, out _));
            Assert.IsTrue(sector.Contains(new Vector3(0f, 0f, -0.3f), Vector3.zero, 0.4f, out _));
            Assert.IsFalse(sector.Contains(new Vector3(0f, 0f, -0.5f), Vector3.zero, 0.4f, out _));
            Assert.IsTrue(sector.Contains(new Vector3(0f, 0f, 5.3f), Vector3.zero, 0.4f, out _));
        }

        [Test]
        public void Occupancy_RaisesEnterAndExit_ForAShapedField()
        {
            Recipe recipe = Carrier(Rectangle(6f, 2f, occupancy: true));
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                hits.AddOverlapTarget(new TargetId(1), new Vector3(3f, 0f, 0f));
                hits.AddOverlapTarget(new TargetId(2), new Vector3(-3f, 0f, 0f));
                var seen = new List<HitInfo>();
                world.Hit += seen.Add;

                PlantAt(world, recipe, Vector3.zero, Vector3.right);
                hits.ClearOverlaps();
                hits.AddOverlapTarget(new TargetId(1), new Vector3(3f, 0f, 3f));
                world.Tick(0.1f);
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.OccupancyEnter, seen[0].Source);
                Assert.AreEqual(new TargetId(1), seen[0].TargetId);
                Assert.AreEqual(HitSource.OccupancyExit, seen[1].Source);
                Assert.AreEqual(new TargetId(1), seen[1].TargetId);
            }
        }

        [Test]
        public void UnusableMeasures_PlantNothing()
        {
            var fields = new[]
            {
                Rectangle(0f, 2f),
                Rectangle(6f, 0f),
                Rectangle(float.NaN, 2f),
                Rectangle(6f, float.PositiveInfinity),
                Sector(5f, 0f),
                Sector(5f, float.NaN),
                Sector(-1f, 60f),
                new GroundField(2f, 2f, 3f) { Shape = (GroundFieldShape)99, Length = 5f, Width = 2f, AngleDegrees = 60f }
            };

            for (int i = 0; i < fields.Length; i++)
            {
                Recipe recipe = Carrier(fields[i]);
                (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
                using (world)
                {
                    var planted = new List<GroundFieldInfo>();
                    world.GroundFieldPlanted += planted.Add;

                    PlantAt(world, recipe, Vector3.zero, Vector3.forward);

                    Assert.AreEqual(0, planted.Count, "field " + i);
                }
            }
        }

        [Test]
        public void ShapedField_IgnoresTheDiskRadius()
        {
            GroundField field = Rectangle(6f, 2f);
            field.Radius = 0f;
            List<TargetId> hit = HitBy(field, Vector3.right, new Vector3(5f, 0f, 0f));

            CollectionAssert.AreEquivalent(new[] { new TargetId(1) }, hit);
        }
    }
}
