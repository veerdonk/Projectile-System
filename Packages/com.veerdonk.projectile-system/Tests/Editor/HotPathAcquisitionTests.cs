using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    // Pins the acquisition and lock scans to the scan they replaced: every hit target through TryGet, then every
    // homing pose through TryGet that is not a hit target.
    public sealed class HotPathAcquisitionTests
    {
        static readonly ProjectileId Shot = new ProjectileId(1);
        static readonly float[] HalfAngles = { 0f, 30f, 45f, 90f, 120f, 179f, 180f };

        [Test]
        public void RandomCatalogChurn_PicksMatchTheFullScan()
        {
            var random = new System.Random(5);
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            var toPoint = new ClosestToPointAcquisition();
            int compared = 0;

            for (int tick = 0; tick < 60; tick++)
            {
                for (int id = 0; id < 120; id++)
                {
                    var target = new TargetId(id);
                    double roll = random.NextDouble();
                    if (roll < 0.25)
                        hitTargets.Set(target, GridPoint(random), 0.5f);
                    else if (roll < 0.30)
                        hitTargets.Remove(target);
                    else if (roll < 0.32)
                        hitTargets.Set(target, GridPoint(random), 0f);
                    else if (roll < 0.57)
                        homing.Set(target, GridPoint(random));
                    else if (roll < 0.62)
                        homing.Remove(target);
                }

                for (int k = 0; k < 25; k++)
                {
                    BindRandom(context, hitTargets, random);
                    bool referenceFirst = random.Next(2) == 0;
                    if (referenceFirst)
                    {
                        bool expected = ReferenceTravel(context, out HomingTarget expectedTarget);
                        AssertSame(expected, expectedTarget, travel.TryAcquire(context, out HomingTarget actual), actual);
                    }
                    else
                    {
                        bool found = travel.TryAcquire(context, out HomingTarget actual);
                        AssertSame(ReferenceTravel(context, out HomingTarget expectedTarget), expectedTarget, found, actual);
                    }

                    bool pointFound = toPoint.TryAcquire(context, out HomingTarget pointTarget);
                    AssertSame(ReferencePoint(context, out HomingTarget expectedPoint), expectedPoint, pointFound, pointTarget);
                    compared++;
                }
            }

            Assert.AreEqual(60 * 25, compared);
        }

        [Test]
        public void MembershipChanges_NeverLeaveTheHomingOnlyListStale()
        {
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            var a = new TargetId(1);
            var b = new TargetId(2);

            homing.Set(a, new Vector3(0f, 0f, 2f));
            hitTargets.Set(a, new Vector3(0f, 0f, 9f), 0.5f);
            homing.Set(b, new Vector3(0f, 0f, 5f));
            Bind(context, Vector3.zero, Vector3.forward, default, false, default, false);
            Assert.AreEqual(b, Pick(travel, context), "a's hit-target position wins over its nearer pose");

            hitTargets.Remove(a);
            Assert.AreEqual(a, Pick(travel, context), "a is still a homing pose once it stops being a hit target");

            homing.Remove(a);
            hitTargets.Set(a, new Vector3(0f, 0f, 1f), 0.5f);
            Assert.AreEqual(a, Pick(travel, context), "a is only a hit target now");

            hitTargets.Set(a, new Vector3(0f, 0f, 8f), 0.5f);
            Assert.AreEqual(b, Pick(travel, context), "a position-only move is read live");

            homing.Set(b, new Vector3(0f, 0f, 9f));
            Assert.AreEqual(a, Pick(travel, context), "a homing pose position-only move is read live");

            homing.Set(a, new Vector3(0f, 0f, 0.5f));
            Assert.AreEqual(a, Pick(travel, context));
            hitTargets.Remove(a);
            Assert.AreEqual(a, Pick(travel, context), "the pose re-added while a was a hit target is listed once a is not");

            homing.Remove(a);
            Assert.AreEqual(b, Pick(travel, context));

            hitTargets.Set(b, new Vector3(0f, 0f, 20f), 0.5f);
            homing.Set(a, new Vector3(0f, 0f, 30f));
            Assert.AreEqual(b, Pick(travel, context), "b's hit-target position wins");
            hitTargets.Set(b, Vector3.zero, 0f);
            Assert.AreEqual(b, Pick(travel, context), "an unusable radius removes b as a hit target and its pose is back");
            homing.Set(b, new Vector3(0f, 0f, 40f));
            Assert.AreEqual(a, Pick(travel, context));
        }

        [Test]
        public void TheSameHitCatalogInstanceIsNotAssumed()
        {
            var homing = new HomingCatalog();
            var first = new HitTargetCatalog();
            var second = new HitTargetCatalog();
            var a = new TargetId(1);
            homing.Set(a, new Vector3(0f, 0f, 3f));
            homing.Set(new TargetId(2), new Vector3(0f, 0f, 4f));
            first.Set(a, new Vector3(0f, 0f, 10f), 0.5f);
            var travel = new ClosestInTravelDirectionAcquisition();

            ModifierContext withFirst = CreateContext(homing, first);
            Bind(withFirst, Vector3.zero, Vector3.forward, default, false, default, false);
            Assert.AreEqual(new TargetId(2), Pick(travel, withFirst));

            ModifierContext withSecond = CreateContext(homing, second);
            Bind(withSecond, Vector3.zero, Vector3.forward, default, false, default, false);
            Assert.AreEqual(a, Pick(travel, withSecond));

            ModifierContext withNone = CreateContext(homing, null);
            Bind(withNone, Vector3.zero, Vector3.forward, default, false, default, false);
            Assert.AreEqual(a, Pick(travel, withNone));
        }

        [Test]
        public void EqualDistances_KeepTheFirstCandidateInScanOrder()
        {
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            homing.Set(new TargetId(5), new Vector3(3f, 0f, 4f));
            homing.Set(new TargetId(6), new Vector3(-3f, 0f, 4f));
            hitTargets.Set(new TargetId(7), new Vector3(0f, 0f, 5f), 0.5f);
            hitTargets.Set(new TargetId(8), new Vector3(4f, 0f, 3f), 0.5f);
            Bind(context, Vector3.zero, Vector3.forward, default, false, default, false);
            Assert.AreEqual(new TargetId(7), Pick(travel, context));

            hitTargets.Remove(new TargetId(7));
            Assert.AreEqual(new TargetId(8), Pick(travel, context));

            hitTargets.Remove(new TargetId(8));
            Assert.AreEqual(new TargetId(5), Pick(travel, context));

            homing.Remove(new TargetId(5));
            homing.Set(new TargetId(5), new Vector3(3f, 0f, 4f));
            Assert.AreEqual(new TargetId(6), Pick(travel, context), "a removed and re-set pose moves to the end");
        }

        [Test]
        public void IgnoredAndFallbackTargets_MatchTheFullScan()
        {
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            var near = new TargetId(1);
            var far = new TargetId(2);
            var behind = new TargetId(3);
            hitTargets.Set(near, new Vector3(0f, 0f, 2f), 0.5f);
            homing.Set(far, new Vector3(0f, 0f, 6f));
            hitTargets.Set(behind, new Vector3(0f, 0f, -1f), 0.5f);

            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(near), false, default, false);
            Assert.AreEqual(far, Pick(travel, context), "ignored near is skipped");

            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(near), true, default, false);
            Assert.AreEqual(far, Pick(travel, context), "a fallback in the cone loses to any other candidate");

            homing.Remove(far);
            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(near), true, default, false);
            Assert.AreEqual(near, Pick(travel, context), "the fallback is locked when it is the only one in the cone");

            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(behind), true, default, false);
            Assert.AreEqual(near, Pick(travel, context));

            hitTargets.Remove(near);
            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(behind), true, default, false);
            Assert.IsFalse(travel.TryAcquire(context, out _), "a fallback outside the cone is not locked");

            Bind(context, Vector3.zero, Vector3.back, HomingTarget.Assigned(behind), true, default, false);
            Assert.AreEqual(behind, Pick(travel, context));
        }

        [Test]
        public void FallbackNearerThanTheBest_StillOnlyCountsWhenNothingElseIsInTheCone()
        {
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            var fallback = new TargetId(1);
            var other = new TargetId(2);
            hitTargets.Set(other, new Vector3(0f, 0f, 4f), 0.5f);
            hitTargets.Set(fallback, new Vector3(0f, 0f, 9f), 0.5f);
            homing.Set(new TargetId(3), new Vector3(0f, 0f, -2f));

            Bind(context, Vector3.zero, Vector3.forward, HomingTarget.Assigned(fallback), true, default, false);
            Assert.AreEqual(other, Pick(travel, context));

            hitTargets.Set(fallback, new Vector3(0f, 0f, 1f), 0.5f);
            Assert.AreEqual(other, Pick(travel, context));
            Assert.IsTrue(ReferenceTravel(context, out HomingTarget expected));
            Assert.AreEqual(other, expected.Id);
        }

        [Test]
        public void MultiplyLockRange_ChildLocksMatchTheFullScan()
        {
            var random = new System.Random(11);
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            var spawns = new List<SpawnRequest>();
            var context = new ModifierContext(spawns, homing, 1u) { HitTargets = hitTargets };
            Recipe child = SimulationTestHelper.CreateRecipe("hot-lock-child");
            var multiply = new Multiply(7, child, 0.5f, new SquarePattern(), MultiplyDirection.Surface, 4f);

            for (int round = 0; round < 80; round++)
            {
                for (int id = 0; id < 60; id++)
                {
                    var target = new TargetId(id);
                    double roll = random.NextDouble();
                    if (roll < 0.3)
                        hitTargets.Set(target, GridPoint(random), 0.5f);
                    else if (roll < 0.36)
                        hitTargets.Remove(target);
                    else if (roll < 0.66)
                        homing.Set(target, GridPoint(random));
                    else if (roll < 0.72)
                        homing.Remove(target);
                }

                hitTargets.Release(Shot);
                if (random.Next(3) == 0)
                    hitTargets.SetShooter(Shot, new TargetId(random.Next(60)));

                var hit = new HitInfo
                {
                    Kind = random.Next(4) == 0 ? HitKind.World : HitKind.Target,
                    TargetId = new TargetId(random.Next(60)),
                    Point = GridPoint(random),
                    Normal = Vector3.up
                };
                Bind(context, Vector3.zero, Vector3.forward, default, false, default, true, hit);
                spawns.Clear();
                multiply.Apply(context);

                List<TargetId> expected = ReferenceLocks(context, hit.Point, hit.Kind == HitKind.Target ? HomingTarget.Assigned(hit.TargetId) : default, 4f, out bool hitTargetOnly);
                Assert.AreEqual(7, spawns.Count);
                for (int i = 0; i < spawns.Count; i++)
                {
                    HomingTarget actual = spawns[i].HomingTarget;
                    if (expected.Count == 0)
                    {
                        Assert.AreEqual(hitTargetOnly, actual.IsAssigned, "round " + round);
                        if (hitTargetOnly)
                            Assert.AreEqual(hit.TargetId, actual.Id, "round " + round);
                        continue;
                    }

                    Assert.IsTrue(actual.IsAssigned, "round " + round);
                    Assert.AreEqual(expected[i % expected.Count], actual.Id, "round " + round + " child " + i);
                }
            }
        }

        [Test]
        public void SteadyStateAcquisition_WithTwoThousandTargets_DoesNotAllocate()
        {
            var hitTargets = new HitTargetCatalog();
            var homing = new HomingCatalog();
            ModifierContext context = CreateContext(homing, hitTargets);
            var travel = new ClosestInTravelDirectionAcquisition();
            var toPoint = new ClosestToPointAcquisition();
            for (int i = 0; i < 2000; i++)
            {
                hitTargets.Set(new TargetId(i), Spread(i, 0f), 0.5f);
                if (i % 2 == 0)
                    homing.Set(new TargetId(i), Spread(i, 0.25f));
                if (i % 3 == 0)
                    homing.Set(new TargetId(10000 + i), Spread(i, 0.5f));
            }

            void Step(int frame)
            {
                float shift = (frame & 1) == 0 ? 0.5f : 0f;
                for (int i = 0; i < 2000; i++)
                {
                    hitTargets.Set(new TargetId(i), Spread(i, shift), 0.5f);
                    if (i % 2 == 0)
                        homing.Set(new TargetId(i), Spread(i, shift + 0.25f));
                }

                for (int k = 0; k < 20; k++)
                {
                    travel.TryAcquire(context, out _);
                    toPoint.TryAcquire(context, out _);
                }
            }

            Bind(context, new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 2f), default, false, new Vector3(30f, 0f, 30f), false);
            context.HomingAcquireHalfAngleDegrees = 45f;
            for (int frame = 0; frame < 4; frame++)
                Step(frame);

            Assert.That(() =>
            {
                for (int frame = 0; frame < 6; frame++)
                    Step(frame);
            }, Is.Not.AllocatingGCMemory());
        }

        static Vector3 Spread(int i, float shift)
        {
            return new Vector3((i % 50) * 1.5f + shift, 0f, (i / 50) * 1.5f - shift);
        }

        static TargetId Pick(IHomingAcquisition acquisition, ModifierContext context)
        {
            Assert.IsTrue(acquisition.TryAcquire(context, out HomingTarget target));
            Assert.IsTrue(target.IsAssigned);
            Assert.IsTrue(ReferenceTravel(context, out HomingTarget expected));
            Assert.AreEqual(expected.Id, target.Id);
            return target.Id;
        }

        static void AssertSame(bool expectedFound, HomingTarget expected, bool actualFound, HomingTarget actual)
        {
            Assert.AreEqual(expectedFound, actualFound);
            if (!expectedFound)
                return;
            Assert.AreEqual(expected.IsAssigned, actual.IsAssigned);
            Assert.AreEqual(expected.Id, actual.Id);
        }

        static Vector3 GridPoint(System.Random random)
        {
            return new Vector3(random.Next(-6, 7), random.Next(-1, 2), random.Next(-6, 7));
        }

        static ModifierContext CreateContext(HomingCatalog homing, HitTargetCatalog hitTargets)
        {
            return new ModifierContext(new List<SpawnRequest>(), homing, 1u) { HitTargets = hitTargets };
        }

        static void BindRandom(ModifierContext context, HitTargetCatalog hitTargets, System.Random random)
        {
            hitTargets.Release(Shot);
            if (random.Next(4) == 0)
                hitTargets.SetShooter(Shot, new TargetId(random.Next(120)));

            Vector3 position = random.Next(3) == 0 ? Vector3.zero : GridPoint(random);
            Vector3 velocity = random.Next(5) == 0 ? Vector3.zero : GridPoint(random) + new Vector3(0f, 0f, 0.5f);
            HomingTarget ignored = random.Next(3) == 0 ? HomingTarget.Assigned(new TargetId(random.Next(120))) : default;
            Bind(context, position, velocity, ignored, random.Next(2) == 0, GridPoint(random), false);
            context.HomingAcquireHalfAngleDegrees = HalfAngles[random.Next(HalfAngles.Length)];
        }

        static void Bind(
            ModifierContext context,
            Vector3 position,
            Vector3 velocity,
            HomingTarget ignored,
            bool ignoredPassed,
            Vector3 acquirePoint,
            bool hasHit,
            HitInfo hit = default)
        {
            context.Bind(new ModifierBindState
            {
                InstanceId = Shot,
                Position = position,
                Velocity = velocity,
                Speed = velocity.magnitude,
                EffectRadiusScale = 1f,
                MultiplyGenerationsLeft = 1,
                HomingIgnoredTarget = ignored,
                HomingIgnoredPassed = ignored.IsAssigned && ignoredPassed,
                HomingAcquirePointSet = true,
                HomingAcquirePoint = acquirePoint
            }, hasHit, hit, 0.1f);
        }

        // The scan before the homing-only list, the per-pose Contains, and the distance-first early out.
        static bool ReferenceTravel(ModifierContext context, out HomingTarget target)
        {
            target = default;
            Vector3 travel = context.Velocity;
            if (travel.sqrMagnitude < 1e-12f)
                return false;

            float half = Mathf.Clamp(context.HomingAcquireHalfAngleDegrees, 0f, 180f);
            bool all = half >= 180f;
            bool none = half <= 0f;
            float cos = Mathf.Approximately(half, 90f) ? 0f : Mathf.Cos(half * Mathf.Deg2Rad);
            bool found = false;
            TargetId bestId = default;
            float bestSqr = 0f;
            bool fallbackInCone = false;

            void Consider(TargetId id, Vector3 position)
            {
                if (context.HomingIgnoredTarget.IsAssigned && id.Equals(context.HomingIgnoredTarget.Id))
                    return;
                if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                    return;

                Vector3 toTarget = position - context.Position;
                bool inCone;
                if (all)
                    inCone = true;
                else if (none)
                    inCone = false;
                else
                {
                    float dot = Vector3.Dot(toTarget, travel);
                    inCone = cos == 0f ? dot > 0f : dot > cos * toTarget.magnitude * travel.magnitude;
                }

                if (!inCone)
                    return;

                if (context.HomingFallbackTarget.IsAssigned && id.Equals(context.HomingFallbackTarget.Id))
                {
                    fallbackInCone = true;
                    return;
                }

                float sqrDistance = toTarget.sqrMagnitude;
                if (found && sqrDistance >= bestSqr)
                    return;

                bestSqr = sqrDistance;
                bestId = id;
                found = true;
            }

            ScanBoth(context, Consider);
            if (found)
            {
                target = HomingTarget.Assigned(bestId);
                return true;
            }

            if (fallbackInCone)
            {
                target = context.HomingFallbackTarget;
                return true;
            }

            return false;
        }

        static bool ReferencePoint(ModifierContext context, out HomingTarget target)
        {
            target = default;
            if (!context.HomingAcquirePointSet)
                return false;

            Vector3 point = context.HomingAcquirePoint;
            bool found = false;
            TargetId bestId = default;
            float bestSqr = 0f;

            void Consider(TargetId id, Vector3 position)
            {
                if (context.HomingIgnoredTarget.IsAssigned && id.Equals(context.HomingIgnoredTarget.Id))
                    return;
                if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                    return;

                float sqrDistance = (position - point).sqrMagnitude;
                if (found && sqrDistance >= bestSqr)
                    return;

                bestSqr = sqrDistance;
                bestId = id;
                found = true;
            }

            ScanBoth(context, Consider);
            if (!found)
                return false;

            target = HomingTarget.Assigned(bestId);
            return true;
        }

        static List<TargetId> ReferenceLocks(
            ModifierContext context,
            Vector3 origin,
            HomingTarget hitTarget,
            float lockRange,
            out bool hitTargetOnly)
        {
            var ids = new List<TargetId>();
            var distances = new List<float>();
            float maxSqr = lockRange * lockRange;
            bool hitTargetInRange = false;

            void Consider(TargetId id, Vector3 position)
            {
                if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                    return;

                float sqrDistance = (position - origin).sqrMagnitude;
                if (sqrDistance > maxSqr)
                    return;

                if (hitTarget.IsAssigned && id.Equals(hitTarget.Id))
                {
                    hitTargetInRange = true;
                    return;
                }

                int at = ids.Count;
                while (at > 0 && distances[at - 1] > sqrDistance)
                    at--;
                ids.Insert(at, id);
                distances.Insert(at, sqrDistance);
            }

            ScanBoth(context, Consider);
            hitTargetOnly = ids.Count == 0 && hitTargetInRange;
            return ids;
        }

        static void ScanBoth(ModifierContext context, System.Action<TargetId, Vector3> consider)
        {
            HitTargetCatalog hitTargets = context.HitTargets;
            if (hitTargets != null)
            {
                int hitCount = hitTargets.Count;
                for (int i = 0; i < hitCount; i++)
                {
                    if (hitTargets.TryGet(i, out TargetId id, out Vector3 position))
                        consider(id, position);
                }
            }

            HomingCatalog catalog = context.HomingCatalog;
            int count = catalog.Count;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGet(i, out TargetId id, out Vector3 position))
                    continue;
                if (hitTargets != null && hitTargets.Contains(id))
                    continue;
                consider(id, position);
            }
        }
    }
}
