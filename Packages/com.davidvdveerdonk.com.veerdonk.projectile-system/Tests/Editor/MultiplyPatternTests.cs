using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class MultiplyPatternTests
    {
        const float S = 2f;
        static readonly Vector3 Forward = Vector3.forward;

        [Test]
        public void SquarePattern_NamedCounts_MatchLattice()
        {
            AssertLocals(new SquarePattern(), 1, Vector2.zero);
            AssertLocals(new SquarePattern(), 2, new Vector2(-S * 0.5f, 0f), new Vector2(S * 0.5f, 0f));
            AssertLocals(new SquarePattern(), 3, new Vector2(-S, 0f), Vector2.zero, new Vector2(S, 0f));
            AssertLocals(
                new SquarePattern(),
                4,
                new Vector2(-S, S),
                new Vector2(S, S),
                new Vector2(-S, -S),
                new Vector2(S, -S));
            AssertLocals(
                new SquarePattern(),
                5,
                new Vector2(-S, S),
                new Vector2(S, S),
                new Vector2(-S, -S),
                new Vector2(S, -S),
                Vector2.zero);
            AssertLocals(
                new SquarePattern(),
                6,
                new Vector2(-S, S),
                new Vector2(-S, 0f),
                new Vector2(-S, -S),
                new Vector2(S, S),
                new Vector2(S, 0f),
                new Vector2(S, -S));
            AssertLocals(
                new SquarePattern(),
                7,
                new Vector2(-S, S),
                new Vector2(-S, 0f),
                new Vector2(-S, -S),
                new Vector2(S, S),
                new Vector2(S, 0f),
                new Vector2(S, -S),
                Vector2.zero);
            AssertLocals(
                new SquarePattern(),
                8,
                new Vector2(-S, S),
                new Vector2(-S, 0f),
                new Vector2(-S, -S),
                new Vector2(S, S),
                new Vector2(S, 0f),
                new Vector2(S, -S),
                Vector2.zero,
                new Vector2(0f, S));
            AssertLocals(
                new SquarePattern(),
                9,
                new Vector2(-S, S),
                new Vector2(-S, 0f),
                new Vector2(-S, -S),
                new Vector2(S, S),
                new Vector2(S, 0f),
                new Vector2(S, -S),
                Vector2.zero,
                new Vector2(0f, S),
                new Vector2(0f, -S));
        }

        [Test]
        public void SquarePattern_10_AddsTopMiddleOf5x5()
        {
            var pattern = new SquarePattern();
            AssertLocal(pattern, 9, 10, new Vector2(0f, 2f * S));
        }

        [Test]
        public void CirclePattern_3_IsEquilateralTriangle_4_AddsCenter()
        {
            float r = S / Mathf.Sqrt(3f);
            AssertLocals(
                new CirclePattern(),
                3,
                new Vector2(0f, r),
                new Vector2(-0.5f * S, -0.5f * r),
                new Vector2(0.5f * S, -0.5f * r));
            AssertLocals(
                new CirclePattern(),
                4,
                new Vector2(0f, r),
                new Vector2(-0.5f * S, -0.5f * r),
                new Vector2(0.5f * S, -0.5f * r),
                Vector2.zero);

            Vector3[] triangle = Offsets(new CirclePattern(), 3);
            Assert.AreEqual(S, Vector3.Distance(triangle[0], triangle[1]), 1e-4f);
            Assert.AreEqual(S, Vector3.Distance(triangle[1], triangle[2]), 1e-4f);
            Assert.AreEqual(S, Vector3.Distance(triangle[2], triangle[0]), 1e-4f);
        }

        [Test]
        public void CirclePattern_6_IsCenterPlusPentagon()
        {
            var pattern = new CirclePattern();
            AssertLocal(pattern, 0, 6, Vector2.zero);
            Vector3[] offsets = Offsets(pattern, 6);
            float radius = S / (2f * Mathf.Sin(Mathf.PI / 5f));
            AssertLocal(pattern, 1, 6, new Vector2(0f, radius));
            for (int i = 1; i < 6; i++)
            {
                Assert.AreEqual(radius, offsets[i].magnitude, 1e-4f);
                int next = i == 5 ? 1 : i + 1;
                Assert.AreEqual(S, Vector3.Distance(offsets[i], offsets[next]), 1e-4f);
            }
        }

        [Test]
        public void CirclePattern_1_And_2_MatchSquare()
        {
            AssertLocals(new CirclePattern(), 1, Vector2.zero);
            AssertLocals(new CirclePattern(), 2, new Vector2(-S * 0.5f, 0f), new Vector2(S * 0.5f, 0f));
        }

        [Test]
        public void RandomCirclePattern_StaysInsideDisk_AndIsDeterministic()
        {
            Vector3[] first = Offsets(new RandomCirclePattern(), 8, seed: 17u);
            Vector3[] second = Offsets(new RandomCirclePattern(), 8, seed: 17u);
            Assert.AreEqual(8, first.Length);
            bool anyAwayFromCenter = false;
            for (int i = 0; i < first.Length; i++)
            {
                Assert.LessOrEqual(first[i].magnitude, S + 1e-4f);
                Assert.AreEqual(0f, first[i].z, 1e-4f);
                Assert.AreEqual(first[i].x, second[i].x, 1e-4f);
                Assert.AreEqual(first[i].y, second[i].y, 1e-4f);
                Assert.AreEqual(first[i].z, second[i].z, 1e-4f);
                if (first[i].sqrMagnitude > 1e-6f)
                    anyAwayFromCenter = true;
            }

            Assert.IsTrue(anyAwayFromCenter);
            Assert.AreEqual(Vector3.zero, new RandomCirclePattern().GetOffset(Context(), Forward, S, 0, 1));
        }

        [Test]
        public void Spread0_StacksForEveryPattern()
        {
            IMultiplyPattern[] patterns =
            {
                new SquarePattern(),
                new CirclePattern(),
                new RandomCirclePattern()
            };
            for (int p = 0; p < patterns.Length; p++)
            {
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(Vector3.zero, patterns[p].GetOffset(Context(), Forward, 0f, i, 4));
            }
        }

        [Test]
        public void Multiply2_Square_OnInterval_PlacesPairInTravelPlane()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("interval-pattern-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("interval-pattern-parent", speed: 10f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child, S, new SquarePattern()) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 8);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.25f);
                world.Tick(0.25f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children =
                    Children(world, child.Id);
                Assert.AreEqual(2, children.Count);
                Vector3 origin = new Vector3(0f, 0f, 2.5f);
                Vector3 left = origin + ExpectedWorld(Vector3.forward, new Vector2(-S * 0.5f, 0f));
                Vector3 right = origin + ExpectedWorld(Vector3.forward, new Vector2(S * 0.5f, 0f));
                AssertContainsPosition(children, left);
                AssertContainsPosition(children, right);
                Assert.AreEqual(S, Vector3.Distance(children[0].position, children[1].position), 1e-4f);
                for (int i = 0; i < children.Count; i++)
                {
                    Assert.AreEqual(0f, children[i].velocity.x, 1e-4f);
                    Assert.AreEqual(0f, children[i].velocity.y, 1e-4f);
                    Assert.AreEqual(child.Speed, children[i].velocity.z, 1e-4f);
                }
            }
        }

        [Test]
        public void Multiply5_Square_OnHit_PlacesDie5AroundHitPoint()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("hit-pattern-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("hit-pattern-parent", speed: 20f, lifetime: 5f);
            parent.OnHit = new IModifier[] { new Multiply(5, child, S, new SquarePattern()) };

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 8);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                Vector3 normal = Vector3.up;
                hits.Queue(HitKind.Target, point, normal, new TargetId(1));
                world.Tick(0.1f);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children =
                    Children(world, child.Id);
                Assert.AreEqual(5, children.Count);
                Vector2[] locals =
                {
                    new Vector2(-S, S),
                    new Vector2(S, S),
                    new Vector2(-S, -S),
                    new Vector2(S, -S),
                    Vector2.zero
                };
                for (int i = 0; i < locals.Length; i++)
                {
                    Vector3 expected = SimulationTestHelper.OffSurface(
                        point + ExpectedWorld(normal, locals[i]),
                        normal,
                        child.Radius);
                    AssertContainsPosition(children, expected);
                }

                for (int i = 0; i < children.Count; i++)
                {
                    Assert.AreEqual(0f, children[i].velocity.x, 1e-4f);
                    Assert.AreEqual(child.Speed, children[i].velocity.y, 1e-4f);
                    Assert.AreEqual(0f, children[i].velocity.z, 1e-4f);
                }
            }
        }

        static void AssertLocals(IMultiplyPattern pattern, int count, params Vector2[] locals)
        {
            Assert.AreEqual(count, locals.Length);
            for (int i = 0; i < count; i++)
                AssertLocal(pattern, i, count, locals[i]);
        }

        static void AssertLocal(IMultiplyPattern pattern, int index, int count, Vector2 local)
        {
            AssertOffset(pattern.GetOffset(Context(), Forward, S, index, count), local, Forward);
        }

        static void AssertOffset(Vector3 world, Vector2 local, Vector3 forward)
        {
            Vector3 expected = ExpectedWorld(forward, local);
            Assert.AreEqual(expected.x, world.x, 1e-4f);
            Assert.AreEqual(expected.y, world.y, 1e-4f);
            Assert.AreEqual(expected.z, world.z, 1e-4f);
        }

        static Vector3[] Offsets(IMultiplyPattern pattern, int count, uint seed = 1u)
        {
            ModifierContext context = Context(seed);
            var offsets = new Vector3[count];
            for (int i = 0; i < count; i++)
                offsets[i] = pattern.GetOffset(context, Forward, S, i, count);
            return offsets;
        }

        static Vector3 ExpectedWorld(Vector3 forward, Vector2 local)
        {
            if (forward.sqrMagnitude < 1e-12f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < 1e-8f)
                right = Vector3.right;
            else
                right.Normalize();

            Vector3 up = Vector3.Cross(right, forward);
            return right * local.x + up * local.y;
        }

        static ModifierContext Context(uint seed = 1u)
        {
            return new ModifierContext(new List<SpawnRequest>(), new HomingCatalog(), seed);
        }

        static List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> Children(
            ProjectileWorld world,
            string childId)
        {
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                SimulationTestHelper.CollectActive(world);
            var children = new List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)>();
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id == childId)
                    children.Add(active[i]);
            }

            return children;
        }

        static void AssertContainsPosition(
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> children,
            Vector3 expected)
        {
            for (int i = 0; i < children.Count; i++)
            {
                Vector3 position = children[i].position;
                if (Mathf.Abs(position.x - expected.x) > 1e-4f)
                    continue;
                if (Mathf.Abs(position.y - expected.y) > 1e-4f)
                    continue;
                if (Mathf.Abs(position.z - expected.z) > 1e-4f)
                    continue;
                return;
            }

            Assert.Fail($"Missing child at {expected}");
        }
    }
}
