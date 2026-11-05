using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class MultiplyDirectionLockTests
    {
        static readonly Vector3 HitPoint = new Vector3(0f, 0f, 5f);
        static readonly TargetId HitTarget = new TargetId(100);

        RecipeAsset asset;

        [TearDown]
        public void TearDown()
        {
            if (asset != null)
                Object.DestroyImmediate(asset);
        }

        [Test]
        public void Up_OnAHit_EveryChildFliesWorldUp_OffsetAlongTheHitNormal()
        {
            Recipe child = MakeChild("up-hit-child");
            var multiply = new Multiply(3, child, 0f, null, MultiplyDirection.Up);
            List<Child> children = SplitOnHitAndDispose(child, new IModifier[] { multiply }, Vector3.right);

            Assert.AreEqual(3, children.Count);
            Vector3 expected = SimulationTestHelper.OffSurface(HitPoint, Vector3.right, child.Radius);
            foreach (Child c in children)
            {
                AssertDirection(Vector3.up, c.Velocity);
                AssertVector(expected, c.Position);
            }
        }

        [Test]
        public void Up_PastAPiercedTarget_EveryChildFliesWorldUp()
        {
            Recipe child = MakeChild("up-pierce-child");
            var multiply = new Multiply(2, child, 0f, null, MultiplyDirection.Up);
            List<Child> children = SplitOnHitAndDispose(child, new IModifier[] { new Pierce(1), multiply }, Vector3.back);

            Assert.AreEqual(2, children.Count);
            Vector3 expected = SimulationTestHelper.OffSurface(HitPoint, Vector3.forward, child.Radius);
            foreach (Child c in children)
            {
                AssertDirection(Vector3.up, c.Velocity);
                AssertVector(expected, c.Position);
            }
        }

        [Test]
        public void Up_OnExpire_EveryChildFliesWorldUp()
        {
            Recipe child = MakeChild("up-expire-child");
            List<Child> children = SplitOnExpire(
                child,
                new Multiply(2, child, 0f, null, MultiplyDirection.Up),
                Vector3.forward,
                out Vector3 expirePosition);

            Assert.AreEqual(2, children.Count);
            foreach (Child c in children)
            {
                AssertDirection(Vector3.up, c.Velocity);
                AssertVector(expirePosition, c.Position);
            }
        }

        [Test]
        public void Surface_KeepsTodaysDirections()
        {
            Recipe child = MakeChild("surface-child");
            Assert.AreEqual(MultiplyDirection.Surface, new Multiply(1, child).Direction);
            Assert.AreEqual(0f, new Multiply(1, child).LockRange);

            List<Child> onHit = SplitOnHitAndDispose(child, new IModifier[] { new Multiply(1, child) }, Vector3.right);
            Assert.AreEqual(1, onHit.Count);
            AssertDirection(Vector3.right, onHit[0].Velocity);

            List<Child> pierced = SplitOnHitAndDispose(
                child,
                new IModifier[] { new Pierce(1), new Multiply(1, child, 0f, null, MultiplyDirection.Surface) },
                Vector3.back);
            Assert.AreEqual(1, pierced.Count);
            AssertDirection(Vector3.forward, pierced[0].Velocity);

            Vector3 travel = new Vector3(1f, 1f, 0f).normalized;
            List<Child> onExpire = SplitOnExpire(child, new Multiply(1, child), travel, out _);
            Assert.AreEqual(1, onExpire.Count);
            AssertDirection(travel, onExpire[0].Velocity);
        }

        [Test]
        public void Up_WithAGrid_TheTopRowFollowsTheParentsHorizontalTravel()
        {
            Recipe child = MakeChild("up-grid-child");
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 1, 0, 0, 0, 2, 0, 0, 0 });

            List<Child> children = SplitOnExpire(
                child,
                new Multiply(2, child, 30f, grid, MultiplyDirection.Up),
                new Vector3(1f, 1f, 0f).normalized,
                out Vector3 origin);

            Assert.AreEqual(2, children.Count);
            Child top = Nearest(children, origin + new Vector3(2f, 0f, 0f));
            Child right = Nearest(children, origin + new Vector3(0f, 0f, -2f));
            AssertVector(origin + new Vector3(2f, 0f, 0f), top.Position);
            AssertVector(origin + new Vector3(0f, 0f, -2f), right.Position);
            AssertDirection(new Vector3(Mathf.Sin(30f * Mathf.Deg2Rad), Mathf.Cos(30f * Mathf.Deg2Rad), 0f), top.Velocity);
            AssertDirection(new Vector3(0f, Mathf.Cos(30f * Mathf.Deg2Rad), -Mathf.Sin(30f * Mathf.Deg2Rad)), right.Velocity);
        }

        [Test]
        public void Up_WithAGrid_AndNoHorizontalTravel_KeepsTheWorldFixedBasis()
        {
            Recipe child = MakeChild("up-grid-vertical-child");
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 1, 0, 0, 0, 2, 0, 0, 0 });

            List<Child> children = SplitOnExpire(
                child,
                new Multiply(2, child, 0f, grid, MultiplyDirection.Up),
                Vector3.up,
                out Vector3 origin);

            Assert.AreEqual(2, children.Count);
            AssertVector(origin + new Vector3(0f, 0f, 2f), Nearest(children, origin + new Vector3(0f, 0f, 2f)).Position);
            AssertVector(origin + new Vector3(2f, 0f, 0f), Nearest(children, origin + new Vector3(2f, 0f, 0f)).Position);
            foreach (Child c in children)
                AssertDirection(Vector3.up, c.Velocity);
        }

        [Test]
        public void LockRange_ThreeTargetsAndTheHitTarget_ThreeChildrenLockTheOthersOneEach()
        {
            var near = new TargetId(1);
            var middle = new TargetId(2);
            var far = new TargetId(3);
            Recipe child = MakeChild("lock-three-child");
            var locks = new List<HomingLockInfo>();
            List<Child> children = SplitOnHit(
                child,
                new IModifier[] { new Multiply(3, child, 0f, null, MultiplyDirection.Surface, 10f) },
                Vector3.back,
                out ProjectileWorld world,
                w =>
                {
                    w.SetHomingCandidate(HitTarget, HitPoint + new Vector3(0f, 0f, 0.5f));
                    w.SetHomingCandidate(near, HitPoint + new Vector3(2f, 0f, 0f));
                    w.SetHitTarget(middle, HitPoint + new Vector3(0f, 4f, 0f), 0.5f);
                    w.SetHomingCandidate(far, HitPoint + new Vector3(-6f, 0f, 0f));
                    w.HomingLocked += l => locks.Add(l);
                });
            using (world)
            {
                Assert.AreEqual(3, children.Count);
                var seen = new HashSet<TargetId>();
                foreach (Child c in children)
                {
                    Assert.IsTrue(world.TryGetHomingTarget(c.Id, out HomingTarget locked));
                    Assert.IsTrue(locked.IsAssigned);
                    seen.Add(locked.Id);
                }

                CollectionAssert.AreEquivalent(new[] { near, middle, far }, seen);
                Assert.AreEqual(3, locks.Count);
                foreach (HomingLockInfo l in locks)
                    Assert.AreEqual(HomingLockKind.Assigned, l.Kind);
            }
        }

        [Test]
        public void LockRange_OneOtherTargetAndTheHitTarget_BothChildrenLockTheOther()
        {
            var other = new TargetId(1);
            AssertEveryChildLocks(other, w =>
            {
                w.SetHomingCandidate(HitTarget, HitPoint);
                w.SetHomingCandidate(other, HitPoint + new Vector3(3f, 0f, 0f));
            });
        }

        [Test]
        public void LockRange_OnlyTheHitTargetInRange_TheChildrenLockIt()
        {
            AssertEveryChildLocks(HitTarget, w =>
            {
                w.SetHomingCandidate(HitTarget, HitPoint);
                w.SetHomingCandidate(new TargetId(9), HitPoint + new Vector3(12f, 0f, 0f));
            });
        }

        [Test]
        public void LockRange_ATargetBeyondTheRangeIsNotAssigned()
        {
            var inside = new TargetId(1);
            AssertEveryChildLocks(inside, w =>
            {
                w.SetHomingCandidate(new TargetId(9), HitPoint + new Vector3(10.5f, 0f, 0f));
                w.SetHomingCandidate(inside, HitPoint + new Vector3(0f, 0f, -3f));
            });

            Recipe child = MakeChild("lock-beyond-only-child");
            List<Child> children = SplitOnHit(
                child,
                new IModifier[] { new Multiply(2, child, 0f, null, MultiplyDirection.Surface, 10f) },
                Vector3.back,
                out ProjectileWorld world,
                w => w.SetHomingCandidate(new TargetId(9), HitPoint + new Vector3(10.5f, 0f, 0f)));
            using (world)
                AssertUnlocked(world, children);
        }

        [Test]
        public void LockRangeZero_AssignsNothing()
        {
            Recipe child = MakeChild("lock-off-child");
            var locks = new List<HomingLockInfo>();
            List<Child> children = SplitOnHit(
                child,
                new IModifier[] { new Multiply(3, child) },
                Vector3.back,
                out ProjectileWorld world,
                w =>
                {
                    w.SetHomingCandidate(HitTarget, HitPoint);
                    w.SetHomingCandidate(new TargetId(1), HitPoint + new Vector3(2f, 0f, 0f));
                    w.HomingLocked += l => locks.Add(l);
                });
            using (world)
            {
                Assert.AreEqual(3, children.Count);
                AssertUnlocked(world, children);
                Assert.AreEqual(0, locks.Count);
            }
        }

        [Test]
        public void LockRange_SkipsTheShooter()
        {
            var shooter = new TargetId(7);
            var other = new TargetId(1);
            Recipe child = MakeChild("lock-shooter-child");
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "lock-shooter-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(2, child, 0f, null, MultiplyDirection.Surface, 10f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 4);
                world.SetHomingCandidate(shooter, HitPoint + new Vector3(1f, 0f, 0f));
                world.SetHomingCandidate(other, HitPoint + new Vector3(4f, 0f, 0f));
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    IgnoreTargetSet = true,
                    IgnoreTarget = shooter
                });
                hits.Queue(HitKind.World, HitPoint, Vector3.back);
                world.Tick(0.1f);

                List<Child> children = Children(world, child.Id);
                Assert.AreEqual(2, children.Count);
                foreach (Child c in children)
                {
                    Assert.IsTrue(world.TryGetHomingTarget(c.Id, out HomingTarget locked));
                    Assert.AreEqual(other, locked.Id);
                }
            }
        }

        [Test]
        public void ToRecipe_CopiesDirectionAndLockRange()
        {
            var childAsset = ScriptableObject.CreateInstance<RecipeAsset>();
            try
            {
                childAsset.Id = "up-asset-child";
                asset = ScriptableObject.CreateInstance<RecipeAsset>();
                asset.Id = "up-asset";
                asset.OnHit = new IModifier[] { new Multiply(3, childAsset, 1f, null, MultiplyDirection.Up, 12f) };

                Recipe recipe = asset.ToRecipe();

                var copy = (Multiply)recipe.OnHit[0];
                Assert.AreNotSame(asset.OnHit[0], copy);
                Assert.AreEqual(MultiplyDirection.Up, copy.Direction);
                Assert.AreEqual(12f, copy.LockRange);
                Assert.AreEqual("up-asset-child", copy.ChildRecipe.Id);
            }
            finally
            {
                Object.DestroyImmediate(childAsset);
            }
        }

        static void AssertEveryChildLocks(TargetId expected, System.Action<ProjectileWorld> setup)
        {
            Recipe child = MakeChild("lock-each-child");
            List<Child> children = SplitOnHit(
                child,
                new IModifier[] { new Multiply(2, child, 0f, null, MultiplyDirection.Surface, 10f) },
                Vector3.back,
                out ProjectileWorld world,
                setup);
            using (world)
            {
                Assert.AreEqual(2, children.Count);
                foreach (Child c in children)
                {
                    Assert.IsTrue(world.TryGetHomingTarget(c.Id, out HomingTarget locked));
                    Assert.AreEqual(expected, locked.Id);
                }
            }
        }

        static void AssertUnlocked(ProjectileWorld world, List<Child> children)
        {
            foreach (Child c in children)
            {
                Assert.IsTrue(world.TryGetHomingTarget(c.Id, out HomingTarget locked));
                Assert.IsFalse(locked.IsAssigned);
            }
        }

        static List<Child> SplitOnHit(Recipe child, IModifier[] onHit, Vector3 normal, out ProjectileWorld world, System.Action<ProjectileWorld> setup = null)
        {
            Recipe parent = SimulationTestHelper.CreateRecipe("split-parent-" + child.Id, speed: 20f, lifetime: 5f, onHit: onHit);
            ScriptedHitQuery hits;
            (world, hits) = SimulationTestHelper.CreateWorld(parent);
            world.Prewarm(child, 4);
            setup?.Invoke(world);
            world.Spawn(new SpawnRequest
            {
                Recipe = parent,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });
            hits.Queue(HitKind.Target, HitPoint, normal, HitTarget);
            world.Tick(0.1f);
            return Children(world, child.Id);
        }

        static List<Child> SplitOnHitAndDispose(Recipe child, IModifier[] onHit, Vector3 normal)
        {
            List<Child> children = SplitOnHit(child, onHit, normal, out ProjectileWorld world);
            world.Dispose();
            return children;
        }

        static List<Child> SplitOnExpire(Recipe child, Multiply multiply, Vector3 direction, out Vector3 expirePosition)
        {
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "expire-parent-" + child.Id,
                speed: 10f,
                lifetime: 0.05f,
                onExpire: new IModifier[] { multiply });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 4);
                Vector3 position = default;
                bool expired = false;
                world.Expired += e =>
                {
                    if (e.Reason != ExpireReason.Lifetime || expired)
                        return;
                    expired = true;
                    position = e.Position;
                };
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = new Vector3(1f, 2f, 3f),
                    Direction = direction
                });
                world.Tick(0.1f);
                Assert.IsTrue(expired);
                expirePosition = position;
                return Children(world, child.Id);
            }
        }

        static List<Child> Children(ProjectileWorld world, string childId)
        {
            var result = new List<Child>();
            foreach (var active in SimulationTestHelper.CollectActive(world))
            {
                if (active.recipe.Id == childId)
                    result.Add(new Child { Id = active.id, Position = active.position, Velocity = active.velocity });
            }

            return result;
        }

        static Child Nearest(List<Child> children, Vector3 point)
        {
            Child best = children[0];
            for (int i = 1; i < children.Count; i++)
            {
                if ((children[i].Position - point).sqrMagnitude < (best.Position - point).sqrMagnitude)
                    best = children[i];
            }

            return best;
        }

        static Recipe MakeChild(string id)
        {
            return SimulationTestHelper.CreateRecipe(id, speed: 8f, lifetime: 2f, radius: 0.2f);
        }

        static void AssertDirection(Vector3 expected, Vector3 velocity)
        {
            Assert.Less(Vector3.Angle(expected, velocity), 0.05f, $"expected {expected}, got {velocity.normalized}");
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }

        struct Child
        {
            public ProjectileId Id;
            public Vector3 Position;
            public Vector3 Velocity;
        }
    }
}
