using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HitTargetCatalogTests
    {
        static readonly TargetId A = new TargetId(1);
        static readonly TargetId B = new TargetId(2);
        static readonly TargetId C = new TargetId(3);

        [Test]
        public void UpdateExisting_KeepsOrder_AndMovesPose()
        {
            var catalog = new HitTargetCatalog();
            catalog.Set(A, new Vector3(1f, 0f, 0f), 0.5f);
            catalog.Set(B, new Vector3(2f, 0f, 0f), 0.5f);
            catalog.Set(C, new Vector3(3f, 0f, 0f), 0.5f);

            catalog.Set(B, new Vector3(0f, 0f, 9f), 1f);

            Assert.AreEqual(3, catalog.Count);
            AssertAt(catalog, 0, A, new Vector3(1f, 0f, 0f));
            AssertAt(catalog, 1, B, new Vector3(0f, 0f, 9f));
            AssertAt(catalog, 2, C, new Vector3(3f, 0f, 0f));
        }

        [Test]
        public void Remove_CompactsLeft_AndReindexesTail()
        {
            var catalog = new HitTargetCatalog();
            catalog.Set(A, new Vector3(1f, 0f, 0f), 0.5f);
            catalog.Set(B, new Vector3(2f, 0f, 0f), 0.5f);
            catalog.Set(C, new Vector3(3f, 0f, 0f), 0.5f);

            catalog.Remove(B);

            Assert.AreEqual(2, catalog.Count);
            AssertAt(catalog, 0, A, new Vector3(1f, 0f, 0f));
            AssertAt(catalog, 1, C, new Vector3(3f, 0f, 0f));
            Assert.IsFalse(catalog.Contains(B));
            Assert.IsTrue(catalog.TryGetPosition(C, out Vector3 c));
            Assert.AreEqual(new Vector3(3f, 0f, 0f), c);

            catalog.Set(C, new Vector3(4f, 0f, 0f), 0.5f);
            catalog.Set(B, new Vector3(5f, 0f, 0f), 0.5f);

            Assert.AreEqual(3, catalog.Count);
            AssertAt(catalog, 1, C, new Vector3(4f, 0f, 0f));
            AssertAt(catalog, 2, B, new Vector3(5f, 0f, 0f));
        }

        [Test]
        public void RemovesAndSetsBeforeRead_MatchImmediateRemoveOrder()
        {
            var D = new TargetId(4);
            var catalog = new HitTargetCatalog();
            catalog.Set(A, new Vector3(1f, 0f, 0f), 0.5f);
            catalog.Set(B, new Vector3(2f, 0f, 0f), 0.5f);
            catalog.Set(C, new Vector3(3f, 0f, 0f), 0.5f);

            catalog.Remove(A);
            catalog.Set(D, new Vector3(4f, 0f, 0f), 0.5f);
            catalog.Remove(C);
            catalog.Set(A, new Vector3(5f, 0f, 0f), 0.5f);
            catalog.Set(B, new Vector3(6f, 0f, 0f), 0.5f);
            catalog.Remove(C);

            Assert.AreEqual(3, catalog.Count);
            Assert.IsFalse(catalog.Contains(C));
            Assert.IsTrue(catalog.TryGetPosition(A, out Vector3 a));
            Assert.AreEqual(new Vector3(5f, 0f, 0f), a);
            AssertAt(catalog, 0, B, new Vector3(6f, 0f, 0f));
            AssertAt(catalog, 1, D, new Vector3(4f, 0f, 0f));
            AssertAt(catalog, 2, A, new Vector3(5f, 0f, 0f));
            Assert.IsFalse(catalog.TryGet(3, out _, out _));
        }

        [Test]
        public void MassRemove_KeepsSurvivorOrder()
        {
            var catalog = new HitTargetCatalog();
            for (int i = 0; i < 32; i++)
                catalog.Set(new TargetId(i), new Vector3(i, 0f, 0f), 0.5f);

            for (int i = 0; i < 32; i += 2)
                catalog.Remove(new TargetId(i));

            Assert.AreEqual(16, catalog.Count);
            for (int i = 0; i < 16; i++)
                AssertAt(catalog, i, new TargetId(i * 2 + 1), new Vector3(i * 2 + 1, 0f, 0f));
        }

        [Test]
        public void ContainsAndTryGetPosition_AfterSet()
        {
            var catalog = new HitTargetCatalog();
            Assert.IsFalse(catalog.Contains(A));
            Assert.IsFalse(catalog.TryGetPosition(A, out _));

            catalog.Set(A, new Vector3(1f, 2f, 3f), 0.5f);

            Assert.IsTrue(catalog.Contains(A));
            Assert.IsTrue(catalog.TryGetPosition(A, out Vector3 position));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
        }

        [Test]
        public void NonPositiveRadius_Removes()
        {
            var catalog = new HitTargetCatalog();
            catalog.Set(A, Vector3.zero, 0.5f);
            catalog.Set(B, Vector3.one, 0.5f);

            catalog.Set(A, Vector3.zero, 0f);

            Assert.AreEqual(1, catalog.Count);
            Assert.IsFalse(catalog.Contains(A));
            AssertAt(catalog, 0, B, Vector3.one);

            catalog.Set(C, Vector3.zero, -1f);
            Assert.AreEqual(1, catalog.Count);
            Assert.IsFalse(catalog.Contains(C));
        }

        [Test]
        public void ReSetExistingIds_DoesNotGrowCount()
        {
            var catalog = new HitTargetCatalog();
            for (int i = 0; i < 32; i++)
                catalog.Set(new TargetId(i), new Vector3(i, 0f, 0f), 0.5f);

            for (int i = 0; i < 32; i++)
                catalog.Set(new TargetId(i), new Vector3(i, 0f, 1f), 0.5f);

            Assert.AreEqual(32, catalog.Count);
            for (int i = 0; i < 32; i++)
                AssertAt(catalog, i, new TargetId(i), new Vector3(i, 0f, 1f));
        }

        [Test]
        public void HomingCatalog_UpdateAndRemove_KeepOrder()
        {
            var catalog = new HomingCatalog();
            catalog.Set(A, new Vector3(1f, 0f, 0f));
            catalog.Set(B, new Vector3(2f, 0f, 0f));
            catalog.Set(C, new Vector3(3f, 0f, 0f));

            catalog.Set(B, new Vector3(0f, 0f, 9f));
            Assert.AreEqual(3, catalog.Count);
            AssertAt(catalog, 1, B, new Vector3(0f, 0f, 9f));

            catalog.Remove(B);

            Assert.AreEqual(2, catalog.Count);
            AssertAt(catalog, 0, A, new Vector3(1f, 0f, 0f));
            AssertAt(catalog, 1, C, new Vector3(3f, 0f, 0f));
            Assert.IsFalse(catalog.TryGetPosition(HomingTarget.Assigned(B), out _));
            Assert.IsTrue(catalog.TryGetPosition(HomingTarget.Assigned(C), out Vector3 c));
            Assert.AreEqual(new Vector3(3f, 0f, 0f), c);
        }

        [Test]
        public void HomingCatalog_RemovesAndSetsBeforeRead_MatchImmediateRemoveOrder()
        {
            var catalog = new HomingCatalog();
            catalog.Set(A, new Vector3(1f, 0f, 0f));
            catalog.Set(B, new Vector3(2f, 0f, 0f));
            catalog.Set(C, new Vector3(3f, 0f, 0f));

            catalog.Remove(A);
            catalog.Remove(C);
            catalog.Set(A, new Vector3(5f, 0f, 0f));

            Assert.AreEqual(2, catalog.Count);
            AssertAt(catalog, 0, B, new Vector3(2f, 0f, 0f));
            AssertAt(catalog, 1, A, new Vector3(5f, 0f, 0f));
            Assert.IsFalse(catalog.TryGetPosition(HomingTarget.Assigned(C), out _));
        }

        static void AssertAt(HitTargetCatalog catalog, int index, TargetId expectedId, Vector3 expectedPosition)
        {
            Assert.IsTrue(catalog.TryGet(index, out TargetId id, out Vector3 position));
            Assert.AreEqual(expectedId, id);
            Assert.AreEqual(expectedPosition, position);
        }

        static void AssertAt(HomingCatalog catalog, int index, TargetId expectedId, Vector3 expectedPosition)
        {
            Assert.IsTrue(catalog.TryGet(index, out TargetId id, out Vector3 position));
            Assert.AreEqual(expectedId, id);
            Assert.AreEqual(expectedPosition, position);
        }
    }
}
