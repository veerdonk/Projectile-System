using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HitTargetBroadPhaseTests
    {
        static readonly TargetId A = new TargetId(1);
        static readonly ProjectileId Shot = new ProjectileId(1);

        [Test]
        public void RandomCatalogsAndQueries_MatchTheLinearScanBitForBit()
        {
            var rng = new System.Random(103);
            var fast = new HitTargetCatalog();
            var slow = new HitTargetCatalog(broadPhase: false);
            var fastHits = new List<HitInfo>();
            var slowHits = new List<HitInfo>();
            const int Ids = 600;

            for (int i = 0; i < Ids; i++)
                Publish(rng, fast, slow, new TargetId(i));

            for (int round = 0; round < 60; round++)
            {
                for (int k = 0; k < 80; k++)
                    Mutate(rng, fast, slow, Ids);

                for (int k = 0; k < 40; k++)
                {
                    var instance = new ProjectileId(rng.Next(1, 5));
                    if (rng.Next(6) == 0)
                    {
                        var id = new TargetId(rng.Next(Ids));
                        Vector3 point = RandomPoint(rng);
                        fast.NoteHit(instance, id, point);
                        slow.NoteHit(instance, id, point);
                    }
                    else if (rng.Next(6) == 0)
                    {
                        var id = new TargetId(rng.Next(Ids));
                        fast.SetShooter(instance, id);
                        slow.SetShooter(instance, id);
                    }
                    else if (rng.Next(8) == 0)
                    {
                        fast.Release(instance);
                        slow.Release(instance);
                    }

                    RandomSweep(rng, fast, out Vector3 origin, out Vector3 direction, out float distance, out float radius);
                    bool fastFound = fast.TrySweep(instance, origin, direction, distance, radius, out HitInfo fastHit, out float fastTravel, out float fastClose);
                    bool slowFound = slow.TrySweep(instance, origin, direction, distance, radius, out HitInfo slowHit, out float slowTravel, out float slowClose);
                    string what = $"round {round} sweep {k} from {origin:R} dir {direction:R} dist {distance:R} radius {radius:R}";
                    Assert.AreEqual(slowFound, fastFound, what);
                    AssertSame(slowHit, fastHit, what);
                    AssertSame(slowTravel, fastTravel, what);
                    AssertSame(slowClose, fastClose, what);
                }

                for (int k = 0; k < 15; k++)
                {
                    Vector3 center = RandomPoint(rng);
                    float radius = Pick(rng, 0.1f, 1f, 2f, 3.5f, 10f, 150f);
                    fastHits.Clear();
                    slowHits.Clear();
                    fast.AppendSphere(Shot, center, radius, fastHits);
                    slow.AppendSphere(Shot, center, radius, slowHits);
                    AssertSame(slowHits, fastHits, $"round {round} sphere {k} at {center:R} radius {radius:R}");
                }

                for (int k = 0; k < 15; k++)
                {
                    GroundDisk disk = RandomDisk(rng);
                    fastHits.Clear();
                    slowHits.Clear();
                    fast.AppendDisk(Shot, disk, fastHits);
                    slow.AppendDisk(Shot, disk, slowHits);
                    AssertSame(slowHits, fastHits, $"round {round} disk {k} {disk.Shape} at {disk.Center:R} normal {disk.Normal:R} reach {disk.Radius:R}");
                }

                Assert.AreEqual(slow.Count, fast.Count);
            }

            Assert.Less(fast.NarrowPhaseTests, slow.NarrowPhaseTests / 2);
        }

        [Test]
        public void TwoThousandTargets_SweepsThatMiss_TestAboutAsManyVolumesAsOneHundred()
        {
            int hundred = MissingSweepTests(100, out int hundredHits);
            int crowd = MissingSweepTests(2000, out int crowdHits);

            Assert.AreEqual(0, hundredHits);
            Assert.AreEqual(0, crowdHits);
            Assert.Greater(hundred, 0);
            Assert.LessOrEqual(crowd, hundred + hundred / 4, $"100 targets: {hundred} tests, 2000 targets: {crowd} tests");
            Assert.Less(crowd, 100 * 100);
        }

        [Test]
        public void DenseCrowd_ShortSweepsBetweenTargets_TestAHandfulEach()
        {
            HitTargetCatalog catalog = DenseCrowd(broadPhase: true);
            HitTargetCatalog reference = DenseCrowd(broadPhase: false);
            int worst = 0;
            int total = 0;
            for (int k = 0; k < 100; k++)
            {
                CrowdSweep(k, out Vector3 origin, out Vector3 direction, out float distance);
                int before = catalog.NarrowPhaseTests;
                Assert.IsFalse(catalog.TrySweep(Shot, origin, direction, distance, 0.1f, out _, out _, out _), $"sweep {k}");
                Assert.IsFalse(reference.TrySweep(Shot, origin, direction, distance, 0.1f, out _, out _, out _), $"reference sweep {k}");
                int tests = catalog.NarrowPhaseTests - before;
                total += tests;
                worst = Math.Max(worst, tests);
            }

            Assert.LessOrEqual(worst, 8, $"worst sweep tested {worst} volumes, {total} over 100 sweeps");
        }

        [Test]
        public void DenseCrowd_LongDiagonalSweep_WalksCellsAlongIt()
        {
            HitTargetCatalog catalog = DenseCrowd(broadPhase: true);
            HitTargetCatalog reference = DenseCrowd(broadPhase: false);
            var origin = new Vector3(-15f, 1f, -16f);
            Vector3 direction = new Vector3(1f, 0f, 1f).normalized;

            int before = catalog.NarrowPhaseTests;
            Assert.IsFalse(catalog.TrySweep(Shot, origin, direction, 30f, 0.1f, out _, out _, out _));
            int tests = catalog.NarrowPhaseTests - before;
            Assert.IsFalse(reference.TrySweep(Shot, origin, direction, 30f, 0.1f, out _, out _, out _));

            Assert.LessOrEqual(tests, 60, $"30 m diagonal tested {tests} of {catalog.Count} volumes");
        }

        [Test]
        public void MoveAcrossCells_HitsTheNewPlace_NotTheOld()
        {
            HitTargetCatalog catalog = CatalogWithFarFiller();
            catalog.SetCapsule(A, new Vector3(1f, 0f, 1f), 0.5f, 2f, 1f);
            Assert.IsTrue(SweepDown(catalog, new Vector3(1f, 5f, 1f), out TargetId hit));
            Assert.AreEqual(A, hit);

            catalog.SetCapsule(A, new Vector3(9f, 0f, -7f), 0.5f, 2f, 1f);

            int before = catalog.NarrowPhaseTests;
            Assert.IsFalse(SweepDown(catalog, new Vector3(1f, 5f, 1f), out _));
            Assert.IsTrue(SweepDown(catalog, new Vector3(9f, 5f, -7f), out hit));
            Assert.AreEqual(A, hit);
            Assert.Less(catalog.NarrowPhaseTests - before, 10);

            catalog.Set(A, new Vector3(1.9f, 1f, 1.9f), 0.5f);
            catalog.Set(A, new Vector3(2.1f, 1f, 2.1f), 0.5f);
            Assert.IsTrue(SweepDown(catalog, new Vector3(2.1f, 5f, 2.1f), out hit));
            Assert.AreEqual(A, hit);
            Assert.IsFalse(SweepDown(catalog, new Vector3(1.2f, 5f, 1.2f), out _));
        }

        [Test]
        public void Remove_ThenSweep_Misses()
        {
            HitTargetCatalog catalog = CatalogWithFarFiller();
            catalog.Set(A, new Vector3(3f, 1f, 3f), 0.5f);
            catalog.Remove(A);

            Assert.IsFalse(SweepDown(catalog, new Vector3(3f, 5f, 3f), out _));
            Assert.IsFalse(catalog.Contains(A));
        }

        [Test]
        public void RemoveThenSetSameId_HitsOnlyTheNewPlace()
        {
            HitTargetCatalog catalog = CatalogWithFarFiller();
            catalog.Set(A, new Vector3(3f, 1f, 3f), 0.5f);
            catalog.Remove(A);
            catalog.Set(A, new Vector3(-5f, 1f, 7f), 0.5f);

            Assert.IsFalse(SweepDown(catalog, new Vector3(3f, 5f, 3f), out _));
            Assert.IsTrue(SweepDown(catalog, new Vector3(-5f, 5f, 7f), out TargetId hit));
            Assert.AreEqual(A, hit);

            var results = new List<HitInfo>();
            catalog.AppendSphere(Shot, new Vector3(-5f, 1f, 7f), 1f, results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(A, results[0].TargetId);
        }

        [Test]
        public void UnusableRadius_RemovesFromTheGrid()
        {
            HitTargetCatalog catalog = CatalogWithFarFiller();
            catalog.Set(A, new Vector3(3f, 1f, 3f), 0.5f);
            catalog.Set(A, new Vector3(3f, 1f, 3f), float.NaN);

            Assert.IsFalse(catalog.Contains(A));
            Assert.IsFalse(SweepDown(catalog, new Vector3(3f, 5f, 3f), out _));

            catalog.SetCapsule(A, new Vector3(3f, 0f, 3f), 0.5f, 2f, 1f);
            catalog.SetCapsule(A, new Vector3(3f, 0f, 3f), 0f, 2f, 1f);
            Assert.IsFalse(SweepDown(catalog, new Vector3(3f, 5f, 3f), out _));
        }

        [Test]
        public void CompactAfterRemovals_KeepsHitsRight()
        {
            var catalog = new HitTargetCatalog();
            var reference = new HitTargetCatalog(broadPhase: false);
            for (int i = 0; i < 60; i++)
            {
                var position = new Vector3(i * 1.5f - 40f, 1f, (i % 7) * 1.3f);
                catalog.Set(new TargetId(i), position, 0.5f);
                reference.Set(new TargetId(i), position, 0.5f);
            }

            for (int i = 0; i < 60; i += 3)
            {
                catalog.Remove(new TargetId(i));
                reference.Remove(new TargetId(i));
            }

            Assert.IsTrue(catalog.TryGet(0, out _, out _));
            Assert.AreEqual(40, catalog.SlotCount);

            for (int i = 0; i < 60; i++)
            {
                var top = new Vector3(i * 1.5f - 40f, 5f, (i % 7) * 1.3f);
                bool found = SweepDown(catalog, top, out TargetId hit);
                Assert.AreEqual(i % 3 != 0, found, $"target {i}");
                if (found)
                    Assert.AreEqual(new TargetId(i), hit);

                Assert.AreEqual(SweepDown(reference, top, out TargetId expected), found);
                Assert.AreEqual(expected, hit);
            }
        }

        [Test]
        public void RemoveAndSetWithoutQueries_KeepsTheHitCatalogBounded()
        {
            var catalog = new HitTargetCatalog();
            for (int i = 0; i < 100; i++)
                catalog.Set(new TargetId(i), new Vector3(i, 0f, 0f), 0.5f);

            for (int cycle = 0; cycle < 10000; cycle++)
            {
                var id = new TargetId(cycle % 100);
                catalog.Remove(id);
                catalog.SetCapsule(id, new Vector3(cycle % 100, 0f, cycle % 13), 0.5f, 2f, 1f);
                catalog.Remove(new TargetId(1000 + cycle));
                catalog.Set(new TargetId(1000 + cycle + 1), new Vector3(0f, 0f, cycle % 50), 0.5f);
            }

            Assert.AreEqual(101, catalog.Count);
            Assert.LessOrEqual(catalog.SlotCount, 2 * catalog.Count + 1);
            Assert.IsTrue(SweepDown(catalog, new Vector3(37f, 5f, 9937 % 13), out TargetId hit));
            Assert.AreEqual(new TargetId(37), hit);
        }

        [Test]
        public void RemoveAndSetWithoutQueries_KeepsTheHomingCatalogBounded()
        {
            var catalog = new HomingCatalog();
            for (int i = 0; i < 100; i++)
                catalog.Set(new TargetId(i), new Vector3(i, 0f, 0f));

            for (int cycle = 0; cycle < 10000; cycle++)
            {
                var id = new TargetId(cycle % 100);
                catalog.Remove(id);
                catalog.Set(id, new Vector3(cycle, 0f, 0f));
            }

            Assert.AreEqual(100, catalog.Count);
            Assert.LessOrEqual(catalog.SlotCount, 2 * catalog.Count + 1);
            Assert.IsTrue(catalog.TryGetPosition(HomingTarget.Assigned(new TargetId(42)), out Vector3 position));
            Assert.AreEqual(new Vector3(9942f, 0f, 0f), position);
        }

        static int MissingSweepTests(int targets, out int hits)
        {
            var catalog = new HitTargetCatalog();
            int published = 0;
            for (int i = 0; i < 10 && published < targets; i++)
            {
                for (int j = 0; j < 10 && published < targets; j++)
                {
                    catalog.SetCapsule(new TargetId(published), new Vector3(i * 4f, 0f, j * 4f), 0.5f, 1.8f, 1.2f);
                    published++;
                }
            }

            for (int i = 0; i < 50 && published < targets; i++)
            {
                for (int j = 0; j < 40 && published < targets; j++)
                {
                    if (i >= 25 && i < 35 && j >= 20 && j < 30)
                        continue;

                    var bottom = new Vector3((i - 25) * 4f + 0.3f, 0f, (j - 20) * 4f + 0.3f);
                    catalog.SetCapsule(new TargetId(published), bottom, 0.5f, 1.8f, 1.2f);
                    published++;
                }
            }

            Assert.AreEqual(targets, catalog.Count);
            hits = 0;
            int before = catalog.NarrowPhaseTests;
            for (int k = 0; k < 100; k++)
            {
                float z = (1 + k % 7) * 4f + 2f;
                float x = 6f + (k * 7) % 22 + 0.37f;
                Vector3 direction = k % 3 == 0 ? new Vector3(1f, 0f, 0.2f) : k % 3 == 1 ? Vector3.right : new Vector3(-1f, -0.1f, -0.15f);
                if (catalog.TrySweep(new ProjectileId(k + 1), new Vector3(x, 1f, z), direction, 1f, 0.1f, out _, out _, out _))
                    hits++;
            }

            return catalog.NarrowPhaseTests - before;
        }

        // 50 x 40 capsules on a 2 m lattice (the cell size) with up to 5 cm jitter, so centres sit on both sides of
        // cell boundaries. Lines halfway between rows or columns, and diagonals x - z = odd, clear every capsule.
        static HitTargetCatalog DenseCrowd(bool broadPhase)
        {
            var catalog = new HitTargetCatalog(broadPhase);
            var rng = new System.Random(839);
            int id = 0;
            for (int i = -25; i < 25; i++)
            {
                for (int j = -20; j < 20; j++)
                {
                    float jx = (float)rng.NextDouble() * 0.1f - 0.05f;
                    float jz = (float)rng.NextDouble() * 0.1f - 0.05f;
                    catalog.SetCapsule(new TargetId(id++), new Vector3(i * 2f + jx, 0f, j * 2f + jz), 0.5f, 1.8f, 1.2f);
                }
            }

            return catalog;
        }

        static void CrowdSweep(int k, out Vector3 origin, out Vector3 direction, out float distance)
        {
            int a = k % 31 - 15;
            int b = (k * 7) % 25 - 12;
            distance = 0.5f + (k % 6) * 0.1f;
            switch (k % 4)
            {
                case 0:
                    origin = new Vector3(a * 2f + 0.3f, 1f, b * 2f + 1f);
                    direction = Vector3.right;
                    break;
                case 1:
                    origin = new Vector3(a * 2f + 1f, 1.1f, b * 2f + 0.4f);
                    direction = Vector3.back;
                    break;
                case 2:
                    origin = new Vector3(b * 2f + 1f + 0.35f, 0.9f, b * 2f + 0.35f);
                    direction = new Vector3(1f, 0f, 1f);
                    break;
                default:
                    origin = new Vector3(a * 2f + 1f - 0.2f, 1f, a * 2f - 0.2f);
                    direction = new Vector3(-1f, -0.05f, -1f);
                    break;
            }
        }

        static HitTargetCatalog CatalogWithFarFiller()
        {
            var catalog = new HitTargetCatalog();
            for (int i = 0; i < 50; i++)
                catalog.Set(new TargetId(100 + i), new Vector3(i * 3f, 1f, 500f), 0.5f);
            return catalog;
        }

        static bool SweepDown(HitTargetCatalog catalog, Vector3 top, out TargetId id)
        {
            bool found = catalog.TrySweep(Shot, top, Vector3.down, 10f, 0.05f, out HitInfo hit, out _, out _);
            id = hit.TargetId;
            return found;
        }

        static void Mutate(System.Random rng, HitTargetCatalog fast, HitTargetCatalog slow, int ids)
        {
            var id = new TargetId(rng.Next(ids));
            int op = rng.Next(12);
            if (op < 8)
            {
                Publish(rng, fast, slow, id);
                return;
            }

            if (op < 10)
            {
                fast.Remove(id);
                slow.Remove(id);
                return;
            }

            if (op == 10)
            {
                Vector3 nudge = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 3f;
                if (fast.TryGetPosition(id, out Vector3 position))
                {
                    fast.Set(id, position + nudge, 0.4f);
                    slow.Set(id, position + nudge, 0.4f);
                }

                return;
            }

            float bad = Pick(rng, 0f, -1f, float.NaN, float.PositiveInfinity);
            Vector3 at = RandomPoint(rng);
            fast.Set(id, at, bad);
            slow.Set(id, at, bad);
        }

        static void Publish(System.Random rng, HitTargetCatalog fast, HitTargetCatalog slow, TargetId id)
        {
            Vector3 position = RandomPoint(rng);
            float radius = RandomVolumeRadius(rng);
            int protectionRoll = rng.Next(10);
            ProtectionLevel protection = protectionRoll == 0 ? ProtectionLevel.Strong : protectionRoll == 1 ? ProtectionLevel.Basic : ProtectionLevel.None;
            if (rng.Next(2) == 0)
            {
                fast.Set(id, position, radius, protection);
                slow.Set(id, position, radius, protection);
                return;
            }

            float height = Pick(rng, 0f, 0.5f, 1.8f, 3.2f, 12f);
            float homing = (float)rng.NextDouble() * height;
            fast.SetCapsule(id, position, radius, height, homing, protection);
            slow.SetCapsule(id, position, radius, height, homing, protection);
        }

        static void RandomSweep(System.Random rng, HitTargetCatalog fast, out Vector3 origin, out Vector3 direction, out float distance, out float radius)
        {
            origin = RandomPoint(rng);
            if (rng.Next(5) == 0 && fast.TryGet(rng.Next(Math.Max(1, fast.Count)), out _, out Vector3 aim))
                origin = aim + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.4f;

            int shape = rng.Next(10);
            if (shape == 0)
                direction = Vector3.zero;
            else if (shape == 1)
                direction = rng.Next(2) == 0 ? Vector3.down : Vector3.up;
            else if (shape == 2)
                direction = new Vector3(1e-6f, 0f, 2e-6f);
            else
                direction = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.6f - 0.3f, (float)rng.NextDouble() * 2f - 1f) * Pick(rng, 1f, 1f, 3f, 0.2f);

            distance = Pick(rng, 0f, 0.3f, 0.8f, 2.5f, 6f, 40f, 300f, -1f);
            radius = Pick(rng, 0f, 0.05f, 0.1f, 0.4f, 1.2f, 3f, 25f, -0.3f);
            if (rng.Next(60) == 0)
                distance = Pick(rng, float.NaN, float.PositiveInfinity);
            if (rng.Next(60) == 0)
                origin.x = float.NaN;
        }

        static GroundDisk RandomDisk(System.Random rng)
        {
            Vector3 center = RandomPoint(rng);
            Vector3 normal = rng.Next(3) == 0
                ? Vector3.up
                : GroundDisk.SanitizeNormal(new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() + 0.15f, (float)rng.NextDouble() * 2f - 1f));
            float margin = Pick(rng, 0f, 0f, 0.5f, 3f);
            Vector3 planar = GroundDisk.PlanarDirection(new Vector3((float)rng.NextDouble() * 2f - 1f, 0f, (float)rng.NextDouble() * 2f - 1f));
            int shape = rng.Next(3);
            if (shape == 0)
                return new GroundDisk(center, normal, Pick(rng, 0.5f, 2f, 4f, 9f, 60f), margin, planar);

            float length = Pick(rng, 1f, 3f, 7.5f, 14f);
            if (shape == 1)
                return GroundDisk.Shaped(center, normal, planar, new GroundFieldSize(GroundFieldShape.Rectangle, 0f, length, Pick(rng, 0.5f, 2f, 5f), 0f), margin);

            return GroundDisk.Shaped(center, normal, planar, new GroundFieldSize(GroundFieldShape.Sector, 0f, length, 0f, Pick(rng, 20f, 90f, 200f, 360f)), margin);
        }

        static Vector3 RandomPoint(System.Random rng)
        {
            float y = (float)rng.NextDouble() * 6f - 2f;
            int kind = rng.Next(20);
            if (kind < 11)
                return new Vector3((float)rng.NextDouble() * 60f - 30f, y, (float)rng.NextDouble() * 60f - 30f);
            if (kind < 14)
                return new Vector3(rng.Next(-15, 15) * 2f, y, rng.Next(-15, 15) * 2f);
            if (kind < 16)
                return new Vector3((float)rng.NextDouble() * 8f - 4f, y, (float)rng.NextDouble() * 8f - 4f);
            if (kind < 18)
                return new Vector3(rng.Next(-15, 15) * 2f - 1e-4f, y, rng.Next(-15, 15) * 2f + 1e-4f);
            if (kind < 19)
                return new Vector3(Pick(rng, -1f, 1f) * (float)(1e4 + rng.NextDouble() * 4e5), y, Pick(rng, -1f, 1f) * (float)(rng.NextDouble() * 4e5));
            return new Vector3(Pick(rng, -3e6f, 2e6f, 5e5f), y, Pick(rng, 0f, -3e6f));
        }

        static float RandomVolumeRadius(System.Random rng)
        {
            int kind = rng.Next(20);
            if (kind == 0)
                return Pick(rng, 2f, 2.0001f, 6f, 400f);
            return 0.2f + (float)rng.NextDouble() * 1.3f;
        }

        static float Pick(System.Random rng, params float[] values)
        {
            return values[rng.Next(values.Length)];
        }

        static void AssertSame(List<HitInfo> expected, List<HitInfo> actual, string what)
        {
            Assert.AreEqual(expected.Count, actual.Count, what);
            for (int i = 0; i < expected.Count; i++)
                AssertSame(expected[i], actual[i], $"{what} result {i}");
        }

        static void AssertSame(HitInfo expected, HitInfo actual, string what)
        {
            Assert.AreEqual(expected.InstanceId, actual.InstanceId, what);
            Assert.AreEqual(expected.Kind, actual.Kind, what);
            Assert.AreEqual(expected.Protection, actual.Protection, what);
            Assert.AreEqual(expected.TargetId, actual.TargetId, what);
            AssertSame(expected.Point, actual.Point, what);
            AssertSame(expected.Normal, actual.Normal, what);
        }

        static void AssertSame(Vector3 expected, Vector3 actual, string what)
        {
            AssertSame(expected.x, actual.x, what);
            AssertSame(expected.y, actual.y, what);
            AssertSame(expected.z, actual.z, what);
        }

        static void AssertSame(float expected, float actual, string what)
        {
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual), $"{what}: {expected:R} vs {actual:R}");
        }
    }
}
