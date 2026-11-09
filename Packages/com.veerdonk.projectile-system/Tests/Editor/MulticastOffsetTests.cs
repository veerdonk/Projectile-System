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

            var spawned = new List<SpawnInfo>();
            List<Shot> shots = Fire(count, new RandomConePattern(), Muzzle, Vector3.forward, spawned);

            Assert.AreEqual(count, shots.Count);
            for (int i = 0; i < shots.Count; i++)
                Assert.AreEqual(Muzzle, shots[i].Position);
            Assert.Less(Vector3.Angle(expected[0], Original(shots).Velocity), 0.01f);

            Assert.AreEqual(count, spawned.Count);
            Assert.AreEqual(SpawnCause.Game, spawned[0].Cause);
            for (int i = 1; i < count; i++)
            {
                Assert.AreEqual(SpawnCause.Multicast, spawned[i].Cause);
                Assert.AreEqual(Muzzle, spawned[i].Position);
                Assert.Less(Vector3.Angle(expected[i], spawned[i].Direction), 0.01f, $"Extra {i} out of the 0.2.13 order");
            }
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

        [Test]
        public void Hitscan_EachLanePolylineStartsAtItsOffset()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("offset-hitscan-extra", speed: 10f, lifetime: 2f, radius: 0.1f);
            extra.Delivery = Delivery.Hitscan;
            extra.MaxRange = 20f;
            Recipe shot = SimulationTestHelper.CreateRecipe("offset-hitscan", speed: 10f, lifetime: 2f, radius: 0.1f);
            shot.Delivery = Delivery.Hitscan;
            shot.MaxRange = 20f;
            shot.OnSpawn = new IModifier[] { new Multicast(3, extra, new GridMulticastPattern(Row(), 0f)) };

            Vector3[] lanes =
            {
                Muzzle + Across(Vector3.forward, -Spacing),
                Muzzle,
                Muzzle + Across(Vector3.forward, Spacing)
            };
            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                var hits = new List<HitInfo>();
                world.Hit += info => hits.Add(info);
                for (int i = 0; i < lanes.Length; i++)
                    world.SetHitTarget(new TargetId(10 + i), lanes[i] + Vector3.forward * 5f, 0.3f);

                ProjectileId original = world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = Muzzle,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, hits.Count);
                var seen = new bool[lanes.Length];
                for (int i = 0; i < hits.Count; i++)
                {
                    Assert.AreEqual(HitKind.Target, hits[i].Kind);
                    int lane = hits[i].TargetId == new TargetId(10) ? 0 : hits[i].TargetId == new TargetId(11) ? 1 : 2;
                    Assert.AreEqual(new TargetId(10 + lane), hits[i].TargetId);
                    Assert.IsFalse(seen[lane], "Two shots hit one lane");
                    seen[lane] = true;
                    Assert.AreEqual(lanes[lane].x, hits[i].Point.x, 1e-3f);
                    Assert.AreEqual(lanes[lane].y, hits[i].Point.y, 1e-3f);
                    if (hits[i].InstanceId == original)
                        Assert.AreEqual(0, lane, "The original belongs in cell 1's lane");
                }

                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void GridLanes_ForwardZ_LiteralWorldPositions_LeftColumnLandsOnMinusX()
        {
            List<Shot> shots = Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, Vector3.forward);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(new Vector3(-1f, 2f, 3f), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            Assert.AreEqual(2, extras.Count);
            AssertContains(extras, new Vector3(1f, 2f, 3f));
            AssertContains(extras, new Vector3(3f, 2f, 3f));
        }

        [Test]
        public void GridLanes_ForwardZ_TopRowLandsAbove_RightColumnAimsRight()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, Spacing, new[] { 0, 2, 0, 0, 1, 3, 0, 0, 0 });
            List<Shot> shots = Fire(3, new GridMulticastPattern(grid, 20f), Muzzle, Vector3.forward);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(Muzzle, Original(shots).Position);
            Shot top = default;
            Shot right = default;
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Position.y > Muzzle.y + 1f)
                    top = shots[i];
                if (shots[i].Position.x > Muzzle.x + 1f)
                    right = shots[i];
            }

            AssertPosition(new Vector3(1f, 4f, 3f), top.Position);
            AssertPosition(new Vector3(3f, 2f, 3f), right.Position);
            Assert.Greater(top.Velocity.y, 0f);
            Assert.AreEqual(0f, top.Velocity.x, 1e-3f);
            Assert.Greater(right.Velocity.x, 0f);
            Assert.AreEqual(0f, right.Velocity.y, 1e-3f);
        }

        [Test]
        public void EveryPatternCall_SeesThePositionAndVelocityFromBeforeMulticast()
        {
            var pattern = new RecordingPattern();
            List<Shot> shots = Fire(3, pattern, Muzzle, Vector3.forward);

            Assert.AreEqual(6, pattern.Positions.Count);
            for (int i = 0; i < pattern.Positions.Count; i++)
            {
                Assert.AreEqual(Muzzle, pattern.Positions[i], $"Call {i}");
                Assert.AreEqual(Vector3.forward * 20f, pattern.Velocities[i], $"Call {i}");
            }

            Shot original = Original(shots);
            AssertPosition(Muzzle + new Vector3(1f, 0f, 0f), original.Position);
            Assert.AreEqual(10f, Vector3.Angle(Vector3.forward, original.Velocity), 0.05f);
            List<Vector3> extras = ExtraPositions(shots);
            AssertContains(extras, Muzzle + new Vector3(2f, 0f, 0f));
            AssertContains(extras, Muzzle + new Vector3(3f, 0f, 0f));
        }

        [Test]
        public void Spawned_OriginalReportsTheRequestPosition_ExtrasReportTheirLanes()
        {
            var spawned = new List<SpawnInfo>();
            Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, Vector3.forward, spawned);

            Assert.AreEqual(3, spawned.Count);
            Assert.AreEqual(SpawnCause.Game, spawned[0].Cause);
            Assert.AreEqual(Muzzle, spawned[0].Position);
            Assert.AreEqual(SpawnCause.Multicast, spawned[1].Cause);
            AssertPosition(Muzzle, spawned[1].Position);
            Assert.AreEqual(SpawnCause.Multicast, spawned[2].Cause);
            AssertPosition(Muzzle + Across(Vector3.forward, Spacing), spawned[2].Position);
        }

        [Test]
        public void CountBelowTheNumberedCells_UsesOnlyTheFirstCells()
        {
            List<Shot> shots = Fire(2, new GridMulticastPattern(Row(), 0f), Muzzle, Vector3.forward);

            Assert.AreEqual(2, shots.Count);
            AssertPosition(Muzzle + Across(Vector3.forward, -Spacing), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            Assert.AreEqual(1, extras.Count);
            AssertPosition(Muzzle, extras[0]);
        }

        [Test]
        public void VerticalForward_LaysTheLanesInAHorizontalPlane()
        {
            List<Shot> shots = null;
            Assert.DoesNotThrow(() => shots = Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, Vector3.up));

            Assert.AreEqual(3, shots.Count);
            int offsetLanes = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                Vector3 offset = shots[i].Position - Muzzle;
                Assert.AreEqual(0f, offset.y, 1e-4f);
                if (offset.sqrMagnitude > 1e-8f)
                {
                    offsetLanes++;
                    Assert.AreEqual(Spacing, offset.magnitude, 1e-4f);
                }

                Assert.Less(Vector3.Angle(Vector3.up, shots[i].Velocity), 0.05f);
            }

            Assert.AreEqual(2, offsetLanes);
            AssertPosition(Muzzle + Across(Vector3.up, -Spacing), Original(shots).Position);
        }

        [Test]
        public void ExactlyVertical_RightColumnLandsOnPlusX_TopRowOnPlusZ()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, Spacing, new[] { 0, 2, 0, 1, 0, 3, 0, 0, 0 });
            List<Shot> shots = Fire(3, new GridMulticastPattern(grid, 0f), Muzzle, Vector3.up);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(new Vector3(-1f, 2f, 3f), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            AssertContains(extras, new Vector3(1f, 2f, 5f));
            AssertContains(extras, new Vector3(3f, 2f, 3f));
        }

        [Test]
        public void NearlyVertical_TiltedTowardPlusZ_RightColumnStillLandsOnPlusX()
        {
            Vector3 forward = new Vector3(0f, 1f, 0.05f).normalized;
            List<Shot> shots = Fire(3, new GridMulticastPattern(Row(), 0f), Muzzle, forward);

            Assert.AreEqual(3, shots.Count);
            AssertPosition(new Vector3(-1f, 2f, 3f), Original(shots).Position);
            List<Vector3> extras = ExtraPositions(shots);
            AssertContains(extras, new Vector3(1f, 2f, 3f));
            AssertContains(extras, new Vector3(3f, 2f, 3f));
        }

        [Test]
        public void Beam_FirstAndLaterPulsesStartInTheOriginalsLane()
        {
            Recipe extra = BeamRecipe("offset-beam-extra");
            Recipe shot = BeamRecipe("offset-beam");
            shot.OnSpawn = new IModifier[] { new Multicast(3, extra, new GridMulticastPattern(Row(), 0f)) };
            Vector3 lane = Muzzle + Across(Vector3.forward, -Spacing);

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                var hits = new List<HitInfo>();
                world.Hit += info => hits.Add(info);
                world.SetHitTarget(new TargetId(20), lane + Vector3.forward * 5f, 0.3f);

                ProjectileId original = world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = Muzzle,
                    Direction = Vector3.forward
                });

                List<HitInfo> first = HitsBy(hits, original);
                Assert.AreEqual(1, first.Count, "First pulse");
                Assert.AreEqual(new TargetId(20), first[0].TargetId);
                Assert.AreEqual(lane.x, first[0].Point.x, 1e-3f);

                world.Tick(0.1f);
                world.Tick(0.1f);

                List<HitInfo> later = HitsBy(hits, original);
                Assert.GreaterOrEqual(later.Count, 2, "Later pulse");
                for (int i = 0; i < later.Count; i++)
                {
                    Assert.AreEqual(new TargetId(20), later[i].TargetId);
                    Assert.AreEqual(lane.x, later[i].Point.x, 1e-3f);
                }

                Assert.IsTrue(world.TryGetState(original, out Vector3 position, out _, out _));
                AssertPosition(lane, position);
            }
        }

        [Test]
        public void OnTravelAreaBurst_IsCentredOnTheOriginalsLane()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("offset-area-extra", speed: 12f, lifetime: 2f);
            Recipe shot = SimulationTestHelper.CreateRecipe(
                "offset-area",
                speed: 20f,
                lifetime: 5f,
                onTravel: new IModifier[] { new AreaEffect(1f, 1.5f) });
            shot.OnSpawn = new IModifier[] { new Multicast(3, extra, new GridMulticastPattern(Row(), 0f)) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                world.AreaBurst += info => bursts.Add(info);
                ProjectileId original = world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = Muzzle,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(original, bursts[0].InstanceId);
                Assert.AreEqual(RecipeStage.OnTravel, bursts[0].Stage);
                AssertPosition(Muzzle + Across(Vector3.forward, -Spacing), bursts[0].Center);
            }
        }

        [Test]
        public void View_BindsAtTheMovedPosition()
        {
            var parent = new GameObject("multicast-offset-views");
            try
            {
                Recipe extra = SimulationTestHelper.CreateRecipe("offset-view-extra", speed: 12f, lifetime: 2f);
                Recipe shot = SimulationTestHelper.CreateRecipe("offset-view", speed: 20f, lifetime: 5f);
                shot.OnSpawn = new IModifier[] { new Multicast(3, extra, new GridMulticastPattern(Row(), 0f)) };
                Vector3 muzzle = new Vector3(0f, 500f, 0f);

                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    world.Spawn(new SpawnRequest
                    {
                        Recipe = shot,
                        Position = muzzle,
                        Direction = Vector3.forward
                    });

                    var positions = new List<Vector3>();
                    ProjectileView[] views = parent.GetComponentsInChildren<ProjectileView>(true);
                    for (int i = 0; i < views.Length; i++)
                    {
                        if (views[i] != null && views[i].gameObject.activeSelf)
                            positions.Add(views[i].transform.position);
                    }

                    Assert.AreEqual(3, positions.Count);
                    AssertContains(positions, muzzle + Across(Vector3.forward, -Spacing));
                    AssertContains(positions, muzzle);
                    AssertContains(positions, muzzle + Across(Vector3.forward, Spacing));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Arc_SpreadZero_EveryLaneKeepsTheLoft_AndTheLanesStayOneSpacingApart()
        {
            Vector3 loft = Loft(Vector3.zero, ArcAim, 15f);
            List<Shot> shots = FireArc(new GridMulticastPattern(Row(), 0f), 15f, 15f, out ProjectileWorld world);
            using (world)
            {
                Assert.AreEqual(3, shots.Count);
                Vector3[] lanes = { Across(loft, -Spacing), Vector3.zero, Across(loft, Spacing) };
                for (int i = 0; i < lanes.Length; i++)
                {
                    Assert.AreEqual(0f, lanes[i].y, 1e-4f);
                    Shot shot = At(shots, lanes[i]);
                    Assert.Less(Vector3.Angle(loft, shot.Velocity), 0.05f, $"Lane {i}");
                }

                world.Tick(0.3f);
                List<Vector3> after = new List<Vector3>();
                foreach (var active in SimulationTestHelper.CollectActive(world))
                    after.Add(active.position);
                after.Sort((a, b) => a.x.CompareTo(b.x));
                Assert.AreEqual(3, after.Count);
                for (int i = 1; i < after.Count; i++)
                {
                    Assert.AreEqual(Spacing, after[i].x - after[i - 1].x, 1e-3f);
                    Assert.AreEqual(after[0].y, after[i].y, 1e-3f);
                    Assert.AreEqual(after[0].z, after[i].z, 1e-3f);
                }
            }
        }

        [Test]
        public void Arc_PositiveSpread_TheLanesFanAroundTheLoft()
        {
            Vector3 loft = Loft(Vector3.zero, ArcAim, 15f);
            var pattern = new GridMulticastPattern(Row(), 20f);
            List<Shot> shots = FireArc(pattern, 15f, 15f, out ProjectileWorld world);
            using (world)
            {
                Assert.AreEqual(3, shots.Count);
                Vector3[] lanes = { Across(loft, -Spacing), Vector3.zero, Across(loft, Spacing) };
                for (int i = 0; i < lanes.Length; i++)
                {
                    Vector3 expected = pattern.GetDirection(Context(), loft, i, 3);
                    Assert.Less(Vector3.Angle(expected, At(shots, lanes[i]).Velocity), 0.05f, $"Lane {i}");
                }

                Assert.Less(At(shots, lanes[0]).Velocity.x, -0.1f);
                Assert.Greater(At(shots, lanes[2]).Velocity.x, 0.1f);
                Assert.Less(Vector3.Angle(loft, At(shots, lanes[1]).Velocity), 0.05f);
            }
        }

        [Test]
        public void Arc_UnreachableLane_StillSpawns_AlongTheShotsLoft()
        {
            const float slow = 9.9f;
            Vector3 loft = Loft(Vector3.zero, ArcAim, 15f);
            Assert.IsFalse(BallisticAim.TrySolve(Vector3.zero, ArcAim, slow, ArcGravity, out _));

            List<Shot> shots = FireArc(new GridMulticastPattern(Row(), 0f), 15f, slow, out ProjectileWorld world);
            using (world)
            {
                Assert.AreEqual(3, shots.Count);
                for (int i = 0; i < shots.Count; i++)
                    Assert.Less(Vector3.Angle(loft, shots[i].Velocity), 0.05f);
                List<Vector3> extras = ExtraPositions(shots);
                AssertContains(extras, Vector3.zero);
                AssertContains(extras, Across(loft, Spacing));
            }
        }

        [Test]
        public void Arc_ExtraWithItsOwnSpeed_SolvesToTheAimPointPlusItsOffset()
        {
            Vector3 loft = Loft(Vector3.zero, ArcAim, 15f);
            Vector3 extraLoft = Loft(Vector3.zero, ArcAim, 20f);
            Assert.Greater(Vector3.Angle(loft, extraLoft), 1f);

            List<Shot> shots = FireArc(new GridMulticastPattern(Row(), 0f), 15f, 20f, out ProjectileWorld world);
            using (world)
            {
                Assert.AreEqual(3, shots.Count);
                Shot original = Original(shots);
                AssertPosition(Across(loft, -Spacing), original.Position);
                Assert.Less(Vector3.Angle(loft, original.Velocity), 0.05f);

                Vector3[] lanes = { Vector3.zero, Across(loft, Spacing) };
                for (int i = 0; i < lanes.Length; i++)
                {
                    Shot extra = At(shots, lanes[i]);
                    Assert.IsFalse(extra.Original);
                    Vector3 expected = Loft(lanes[i], ArcAim + lanes[i], 20f);
                    Assert.Less(Vector3.Angle(expected, extra.Velocity), 0.05f, $"Lane {i + 1}");
                    Assert.Less(Vector3.Angle(extraLoft, extra.Velocity), 0.05f, $"Lane {i + 1}");
                    Assert.AreEqual(20f, extra.Velocity.magnitude, 1e-3f);
                }
            }
        }

        [Test]
        public void Arc_ExtraHomingFirstLock_StillReadsTheOriginalAimPoint()
        {
            var atAim = new TargetId(31);
            var atLane = new TargetId(32);
            Vector3 loft = Loft(Vector3.zero, ArcAim, 15f);
            List<Shot> shots = FireArc(
                new GridMulticastPattern(Row(), 0f),
                15f,
                15f,
                out ProjectileWorld world,
                ArcAim,
                new IModifier[] { new Homing(90f) },
                w =>
                {
                    w.SetHomingCandidate(atAim, ArcAim);
                    w.SetHomingCandidate(atLane, ArcAim + Across(loft, Spacing));
                });
            using (world)
            {
                Assert.AreEqual(3, shots.Count);
                world.Tick(0.02f);
                int checkedCount = 0;
                foreach (var active in SimulationTestHelper.CollectActive(world))
                {
                    Assert.IsTrue(world.TryGetHomingTarget(active.id, out HomingTarget locked));
                    Assert.AreEqual(atAim, locked.Id, $"Instance at {active.position}");
                    checkedCount++;
                }

                Assert.AreEqual(3, checkedCount);
            }
        }

        static readonly Vector3 ArcAim = new Vector3(0f, 0f, 10f);
        const float ArcGravity = 9.81f;

        static Vector3 Loft(Vector3 from, Vector3 to, float speed)
        {
            Assert.IsTrue(BallisticAim.TrySolve(from, to, speed, ArcGravity, out Vector3 direction));
            return direction.normalized;
        }

        static List<Shot> FireArc(
            IMulticastPattern pattern,
            float speed,
            float extraSpeed,
            out ProjectileWorld world,
            Vector3? aim = null,
            IModifier[] onTick = null,
            System.Action<ProjectileWorld> setup = null)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("arc-offset-extra", speed: extraSpeed, lifetime: 5f, gravity: ArcGravity, onTick: onTick);
            extra.Delivery = Delivery.Arc;
            Recipe shot = SimulationTestHelper.CreateRecipe("arc-offset-parent", speed: speed, lifetime: 5f, gravity: ArcGravity, onTick: onTick);
            shot.Delivery = Delivery.Arc;
            shot.OnSpawn = new IModifier[] { new Multicast(3, extra, pattern) };

            world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            setup?.Invoke(world);
            world.Spawn(new SpawnRequest
            {
                Recipe = shot,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                HomingAcquirePointSet = true,
                HomingAcquirePoint = aim ?? ArcAim
            });
            return Collect(world, shot);
        }

        static Shot At(List<Shot> shots, Vector3 position)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if ((shots[i].Position - position).sqrMagnitude < 1e-6f)
                    return shots[i];
            }

            Assert.Fail($"No instance at {position}");
            return default;
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

        sealed class RecordingPattern : IMulticastOffsetPattern
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Velocities = new List<Vector3>();

            public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
            {
                Positions.Add(context.Position);
                Velocities.Add(context.Velocity);
                return Quaternion.AngleAxis(10f * (index + 1), Vector3.up) * forward;
            }

            public Vector3 GetOffset(ModifierContext context, Vector3 forward, int index, int count)
            {
                Positions.Add(context.Position);
                Velocities.Add(context.Velocity);
                return new Vector3(index + 1f, 0f, 0f);
            }
        }

        static List<Shot> Fire(
            int count,
            IMulticastPattern pattern,
            Vector3 position,
            Vector3 direction,
            List<SpawnInfo> spawned = null)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("offset-extra", speed: 12f, lifetime: 2f);
            Recipe shot = SimulationTestHelper.CreateRecipe("offset-parent", speed: 20f, lifetime: 5f);
            shot.OnSpawn = new IModifier[] { new Multicast(count, extra, pattern) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: Seed);
            using (world)
            {
                if (spawned != null)
                    world.Spawned += info => spawned.Add(info);
                world.Spawn(new SpawnRequest
                {
                    Recipe = shot,
                    Position = position,
                    Direction = direction
                });
                return Collect(world, shot);
            }
        }

        static Recipe BeamRecipe(string id)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(id, speed: 10f, lifetime: 5f, radius: 0.1f);
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            recipe.MaxRange = 20f;
            return recipe;
        }

        static List<HitInfo> HitsBy(List<HitInfo> hits, ProjectileId instance)
        {
            var result = new List<HitInfo>();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].InstanceId == instance)
                    result.Add(hits[i]);
            }

            return result;
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
            return MultiplyPatternPlane.MirrorX(forward, MultiplyPatternPlane.FromLocal(forward, new Vector2(localX, 0f)));
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
