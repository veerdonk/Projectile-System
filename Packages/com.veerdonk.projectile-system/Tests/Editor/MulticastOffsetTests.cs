using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class MulticastOffsetTests
    {
        const float Spacing = 2f;
        const uint Seed = 9u;
        static readonly Vector3 Muzzle = new Vector3(1f, 2f, 3f);

        [Test]
        public void ThreeSideBySideCells_SpreadZero_FlyThreeParallelLanes_TheOriginalIncluded()
        {
            List<Shot> shots = Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, Vector3.forward);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(Muzzle + Across(Vector3.forward, -Spacing), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            AssertContains(extras, Muzzle);
            AssertContains(extras, Muzzle + Across(Vector3.forward, Spacing));
            for (int i = 0; i < shots.Count; i++)
                Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, shots[i].Velocity), 0.05f);

            Assert.AreEqual(Spacing, Vector3.Distance(Original(shots).Position, Muzzle), 1e-4f);
        }

        [Test]
        public void ThreeSideBySideCells_PositiveSpread_OffsetsTheLanes_AndTheyDiverge()
        {
            List<Shot> shots = Fire(3, new GridMulticastPattern(Row(), 20f), Muzzle, Vector3.forward);

            Assert.AreEqual(3, shots.Count);
            Vector3 left = Across(Vector3.forward, -Spacing);
            Vector3 right = Across(Vector3.forward, Spacing);
            Shot original = Original(shots);
            AssertPosition(Muzzle + left, original.Position);
            Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, original.Velocity), 0.05f);
            Assert.Greater(Vector3.Dot(original.Velocity.normalized, left.normalized), 0.3f);

            bool sawCenter = false;
            bool sawRight = false;
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Original)
                    continue;
                if ((shots[i].Position - Muzzle).sqrMagnitude < 1e-8f)
                {
                    sawCenter = true;
                    Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, shots[i].Velocity), 0.05f);
                }
                else
                {
                    sawRight = true;
                    AssertPosition(Muzzle + right, shots[i].Position);
                    Assert.AreEqual(20f, Vector3.Angle(Vector3.forward, shots[i].Velocity), 0.05f);
                    Assert.Greater(Vector3.Dot(shots[i].Velocity.normalized, right.normalized), 0.3f);
                }
            }

            Assert.IsTrue(sawCenter);
            Assert.IsTrue(sawRight);
        }

        [Test]
        public void GridOffset_PastTheNumberedCells_IsZero()
        {
            var pattern = new GridMulticastPattern(Row(), 0f);
            Assert.AreEqual(Vector3.zero, pattern.GetOffset(Context(), Vector3.forward, 3, 5));
            Assert.AreEqual(Vector3.zero, pattern.GetOffset(Context(), Vector3.forward, 4, 5));
            Assert.AreEqual(Vector3.zero, pattern.GetOffset(Context(), Vector3.forward, -1, 5));

            List<Shot> shots = Fire(5, pattern, Muzzle, Vector3.forward);
            Assert.AreEqual(5, shots.Count);
            int atMuzzle = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                if ((shots[i].Position - Muzzle).sqrMagnitude < 1e-8f)
                    atMuzzle++;
            }

            Assert.AreEqual(3, atMuzzle);
        }

        [Test]
        public void RandomCone_SpawnsAtTheShotPosition_WithTheSameDirectionsAsBefore()
        {
            const int count = 5;
            var expected = new Vector3[count];
            ModifierContext reference = Context(Seed);
            for (int i = 0; i < count; i++)
                expected[i] = RandomConePattern.Sample(reference, Vector3.forward, RandomConePattern.DefaultHalfAngleDegrees);

            List<Shot> shots = Fire(count, new RandomConePattern(), Muzzle, Vector3.forward);

            Assert.AreEqual(count, shots.Count);
            var unmatched = new List<Vector3>();
            for (int i = 1; i < count; i++)
                unmatched.Add(expected[i]);

            for (int i = 0; i < shots.Count; i++)
            {
                Assert.AreEqual(Muzzle, shots[i].Position);
                Vector3 direction = shots[i].Velocity.normalized;
                if (shots[i].Original)
                {
                    Assert.Less(Vector3.Angle(expected[0], direction), 0.01f);
                    continue;
                }

                int match = -1;
                for (int j = 0; j < unmatched.Count; j++)
                {
                    if (Vector3.Angle(unmatched[j], direction) < 0.01f)
                    {
                        match = j;
                        break;
                    }
                }

                Assert.GreaterOrEqual(match, 0, "Extra direction not in the 0.2.13 cone sequence");
                unmatched.RemoveAt(match);
            }

            Assert.AreEqual(0, unmatched.Count);
        }

        [Test]
        public void PatternWithoutOffsets_SpawnsEveryInstanceAtTheShotPosition()
        {
            List<Shot> shots = Fire(3, new SidewaysPattern(), Muzzle, Vector3.forward);

            Assert.AreEqual(3, shots.Count);
            for (int i = 0; i < shots.Count; i++)
                Assert.AreEqual(Muzzle, shots[i].Position);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, Original(shots).Velocity), 0.05f);
        }

        [Test]
        public void ShotInANonAxisDirection_LaysTheLanesAcrossThatDirection()
        {
            Vector3 forward = new Vector3(1f, 0.4f, 2f).normalized;
            List<Shot> shots = Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, forward);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(Muzzle + Across(forward, -Spacing), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            AssertContains(extras, Muzzle);
            AssertContains(extras, Muzzle + Across(forward, Spacing));
            for (int i = 0; i < shots.Count; i++)
            {
                Vector3 offset = shots[i].Position - Muzzle;
                Assert.AreEqual(0f, Vector3.Dot(offset, forward), 1e-4f);
                Assert.AreEqual(0f, offset.y, 1e-4f);
                Assert.Less(Vector3.Angle(forward, shots[i].Velocity), 0.05f);
            }
        }

        [Test]
        public void MovedOriginal_DoesNotHitATargetBetweenTheMuzzleAndItsLane()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("offset-extra", speed: 10f, lifetime: 2f, radius: 0.1f);
            Recipe shot = SimulationTestHelper.CreateRecipe("offset-parent", speed: 10f, lifetime: 2f, radius: 0.1f);
            shot.OnSpawn = new IModifier[] { new Multicast(3, extra, new GridMulticastPattern(Row(), 0f)) };
            Vector3 lane = Across(Vector3.forward, -Spacing);

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                var hits = new List<HitInfo>();
                world.Hit += info => hits.Add(info);
                world.SetHitTarget(new TargetId(5), Muzzle + lane * 0.5f, 0.3f);
                world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = Muzzle,
                    Direction = Vector3.forward
                });

                for (int i = 0; i < 5; i++)
                    world.Tick(0.05f);

                Assert.AreEqual(0, hits.Count);
                Shot original = Original(Collect(world, shot));
                Assert.AreEqual((Muzzle + lane).x, original.Position.x, 1e-4f);
                Assert.Greater(original.Position.z, Muzzle.z + 2f);
            }
        }

        struct Shot
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool Original;
        }

        sealed class SidewaysPattern : IMulticastPattern
        {
            public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
            {
                return index == 0 ? forward : Quaternion.AngleAxis(10f * index, Vector3.up) * forward;
            }
        }

        static List<Shot> Fire(int count, IMulticastPattern pattern, Vector3 position, Vector3 direction)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("offset-extra", speed: 12f, lifetime: 2f);
            Recipe shot = SimulationTestHelper.CreateRecipe("offset-parent", speed: 20f, lifetime: 5f);
            shot.OnSpawn = new IModifier[] { new Multicast(count, extra, pattern) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = position,
                    Direction = direction
                });
                return Collect(world, shot);
            }
        }

        static List<Shot> Collect(ProjectileWorld world, Recipe shot)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            var shots = new List<Shot>(active.Count);
            for (int i = 0; i < active.Count; i++)
            {
                shots.Add(new Shot
                {
                    Position = active[i].position,
                    Velocity = active[i].velocity,
                    Original = active[i].recipe.Id == shot.Id
                });
            }

            return shots;
        }

        static Shot Original(List<Shot> shots)
        {
            int found = -1;
            for (int i = 0; i < shots.Count; i++)
            {
                if (!shots[i].Original)
                    continue;
                Assert.AreEqual(-1, found, "More than one original");
                found = i;
            }

            Assert.GreaterOrEqual(found, 0, "Missing original");
            return shots[found];
        }

        static List<Vector3> ExtraPositions(List<Shot> shots)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < shots.Count; i++)
            {
                if (!shots[i].Original)
                    positions.Add(shots[i].Position);
            }

            return positions;
        }

        static void AssertContains(List<Vector3> positions, Vector3 expected)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i] - expected).sqrMagnitude < 1e-8f)
                    return;
            }

            Assert.Fail($"No instance at {expected}");
        }

        static void AssertPosition(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }

        static Vector3 Across(Vector3 forward, float localX)
        {
            return MultiplyPatternPlane.FromLocal(forward, new Vector2(localX, 0f));
        }

        static GridMultiplyPattern Row()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, Spacing, new[] { 0, 0, 0, 1, 2, 3, 0, 0, 0 });
            return grid;
        }

        static ModifierContext Context(uint seed = 1u)
        {
            return new ModifierContext(new List<SpawnRequest>(), new HomingCatalog(), seed);
        }
    }
}
