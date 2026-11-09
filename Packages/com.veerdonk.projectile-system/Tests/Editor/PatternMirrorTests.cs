using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class PatternMirrorTests
    {
        const float ChildRadius = 0.2f;
        static readonly Vector3 Start = new Vector3(1f, 2f, 3f);
        static readonly Vector3 WallPoint = new Vector3(0f, 0f, 5f);

        [Test]
        public void Multiply_OnExpire_TravelPlusZ_RightColumnLandsOnPlusX_TopRowAbove()
        {
            List<(Vector3 position, Vector3 velocity)> children = SplitOnExpire(
                new Multiply(3, Child("mirror-expire-child"), 0f, Layout()),
                out Vector3 origin);

            Assert.AreEqual(3, children.Count);
            AssertHas(children, origin);
            AssertHas(children, origin + new Vector3(0f, 2f, 0f));
            AssertHas(children, origin + new Vector3(2f, 0f, 0f));
        }

        [Test]
        public void Multiply_OnExpire_TravelPlusZ_GridSpreadAimsTheRightColumnToPlusX()
        {
            List<(Vector3 position, Vector3 velocity)> children = SplitOnExpire(
                new Multiply(3, Child("mirror-expire-aim-child"), 20f, Layout()),
                out Vector3 origin);

            (Vector3 position, Vector3 velocity) right = Nearest(children, origin + new Vector3(2f, 0f, 0f));
            Assert.Greater(right.velocity.x, 0f);
            Assert.AreEqual(0f, right.velocity.y, 1e-3f);
            (Vector3 position, Vector3 velocity) top = Nearest(children, origin + new Vector3(0f, 2f, 0f));
            Assert.Greater(top.velocity.y, 0f);
            Assert.AreEqual(0f, top.velocity.x, 1e-3f);
        }

        [Test]
        public void Multiply_Surface_WallFacingTheShooter_RightColumnLandsOnPlusX()
        {
            Recipe child = Child("mirror-wall-child");
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "mirror-wall-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Multiply(3, child, 0f, Layout()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 4);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, WallPoint, Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                List<(Vector3 position, Vector3 velocity)> children = Children(world, child.Id);
                Assert.AreEqual(3, children.Count);
                float z = WallPoint.z - (ChildRadius + 0.01f);
                AssertHas(children, new Vector3(0f, 0f, z));
                AssertHas(children, new Vector3(0f, 2f, z));
                AssertHas(children, new Vector3(2f, 0f, z));
                for (int i = 0; i < children.Count; i++)
                    Assert.Less(Vector3.Angle(Vector3.back, children[i].velocity), 0.05f);
            }
        }

        [Test]
        public void Multiply_PastAPiercedTarget_TravelPlusZ_RightColumnLandsOnPlusX()
        {
            Recipe child = Child("mirror-pierce-child");
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "mirror-pierce-parent",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Pierce(1), new Multiply(3, child, 0f, Layout()) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                world.Prewarm(child, 4);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, WallPoint, Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                List<(Vector3 position, Vector3 velocity)> children = Children(world, child.Id);
                Assert.AreEqual(3, children.Count);
                float z = WallPoint.z + ChildRadius + 0.01f;
                AssertHas(children, new Vector3(0f, 0f, z));
                AssertHas(children, new Vector3(0f, 2f, z));
                AssertHas(children, new Vector3(2f, 0f, z));
                for (int i = 0; i < children.Count; i++)
                    Assert.Less(Vector3.Angle(Vector3.forward, children[i].velocity), 0.05f);
            }
        }

        [Test]
        public void Multiply_Up_TravelPlusZ_RightColumnLandsOnPlusX_TopRowAlongTravel()
        {
            List<(Vector3 position, Vector3 velocity)> children = SplitOnExpire(
                new Multiply(3, Child("mirror-up-child"), 0f, Layout(), MultiplyDirection.Up),
                out Vector3 origin);

            Assert.AreEqual(3, children.Count);
            AssertHas(children, origin);
            AssertHas(children, origin + new Vector3(0f, 0f, 2f));
            AssertHas(children, origin + new Vector3(2f, 0f, 0f));
            for (int i = 0; i < children.Count; i++)
                Assert.Less(Vector3.Angle(Vector3.up, children[i].velocity), 0.05f);
        }

        static GridMultiplyPattern Layout()
        {
            var grid = new GridMultiplyPattern();
            grid.AssignLayout(3, 2f, new[] { 0, 2, 0, 0, 1, 3, 0, 0, 0 });
            return grid;
        }

        static Recipe Child(string id)
        {
            return SimulationTestHelper.CreateRecipe(id, speed: 8f, lifetime: 2f, radius: ChildRadius);
        }

        static List<(Vector3 position, Vector3 velocity)> SplitOnExpire(Multiply multiply, out Vector3 expirePosition)
        {
            Recipe child = multiply.ChildRecipe;
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "mirror-expire-parent-" + child.Id,
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
                    Position = Start,
                    Direction = Vector3.forward
                });
                world.Tick(0.1f);
                Assert.IsTrue(expired);
                expirePosition = position;
                return Children(world, child.Id);
            }
        }

        static List<(Vector3 position, Vector3 velocity)> Children(ProjectileWorld world, string childId)
        {
            var result = new List<(Vector3 position, Vector3 velocity)>();
            foreach (var active in SimulationTestHelper.CollectActive(world))
            {
                if (active.recipe.Id == childId)
                    result.Add((active.position, active.velocity));
            }

            return result;
        }

        static (Vector3 position, Vector3 velocity) Nearest(List<(Vector3 position, Vector3 velocity)> children, Vector3 point)
        {
            Assert.Greater(children.Count, 0);
            int best = 0;
            for (int i = 1; i < children.Count; i++)
            {
                if ((children[i].position - point).sqrMagnitude < (children[best].position - point).sqrMagnitude)
                    best = i;
            }

            return children[best];
        }

        static void AssertHas(List<(Vector3 position, Vector3 velocity)> children, Vector3 expected)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if ((children[i].position - expected).sqrMagnitude < 1e-6f)
                    return;
            }

            Assert.Fail($"No child at {expected}");
        }
    }
}
