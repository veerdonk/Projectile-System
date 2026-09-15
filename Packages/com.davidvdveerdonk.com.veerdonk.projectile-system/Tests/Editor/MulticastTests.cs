using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class MulticastTests
    {
        [Test]
        public void Multicast3_OnSpawn_CreatesThreeInstancesInARandomCone()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("multicast-extra", speed: 12f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("multicast-parent", speed: 20f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Multicast(3, extra) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: 9u);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(extra, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, world.ActiveCount);
                Assert.AreEqual(5, world.AvailableCount);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                int extraCount = 0;
                for (int i = 0; i < active.Count; i++)
                {
                    Recipe recipe = active[i].recipe;
                    if (recipe.Id == extra.Id)
                        extraCount++;

                    Assert.AreEqual(Vector3.zero, active[i].position);
                    Assert.AreEqual(recipe.Speed, active[i].velocity.magnitude, 0.0001f);
                    float cone = Vector3.Angle(active[i].velocity, Vector3.forward);
                    Assert.LessOrEqual(cone, RandomConePattern.DefaultHalfAngleDegrees + 0.001f);
                }

                Assert.AreEqual(2, extraCount);
                Assert.IsTrue(DirectionsDiffer(active));
            }
        }

        [Test]
        public void Multicast3_ExtraRecipeWithItsOwnMulticast_DoesNotRecurse()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("recursive-extra", speed: 12f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("recursive-parent", speed: 20f, lifetime: 5f);
            extra.OnSpawn = new IModifier[] { new Multicast(3, extra) };
            parent.OnSpawn = new IModifier[] { new Multicast(3, extra) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(extra, 16);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, world.ActiveCount);

                world.Tick(0.1f);

                Assert.AreEqual(3, world.ActiveCount);
            }
        }

        [Test]
        public void Multicast3_SpawnedDirectly_StillMulticasts()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("direct-extra", speed: 12f, lifetime: 2f);
            extra.OnSpawn = new IModifier[] { new Multicast(3, extra) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(extra, 8);

                world.Spawn(new SpawnRequest
                {
                    Recipe = extra,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(3, world.ActiveCount);
            }
        }

        [Test]
        public void Multicast3_CustomPattern_AimsEveryInstanceIncludingTheOriginal()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("fan-extra", speed: 12f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("fan-parent", speed: 20f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Multicast(3, extra, new FanPattern(20f)) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(extra, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                Assert.AreEqual(3, active.Count);

                var yaws = new List<float>();
                for (int i = 0; i < active.Count; i++)
                {
                    float yaw = Yaw(active[i].velocity);
                    yaws.Add(yaw);
                    if (active[i].recipe.Id == parent.Id)
                        Assert.AreEqual(-20f, yaw, 0.001f);
                }

                yaws.Sort();
                Assert.AreEqual(-20f, yaws[0], 0.001f);
                Assert.AreEqual(0f, yaws[1], 0.001f);
                Assert.AreEqual(20f, yaws[2], 0.001f);
            }
        }

        [Test]
        public void Multicast3_SameSeed_IsDeterministic()
        {
            Vector3[] first = SpawnMulticast(seed: 23u);
            Vector3[] second = SpawnMulticast(seed: 23u);
            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].x, second[i].x, 0.0001f);
                Assert.AreEqual(first[i].y, second[i].y, 0.0001f);
                Assert.AreEqual(first[i].z, second[i].z, 0.0001f);
            }
        }

        [Test]
        public void Multicast1_LeavesTheOriginalAlone()
        {
            AssertSingleStraightInstance(count: 1);
        }

        [Test]
        public void Multicast0_LeavesTheOriginalAlone()
        {
            AssertSingleStraightInstance(count: 0);
        }

        static void AssertSingleStraightInstance(int count)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("noop-extra", speed: 12f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("noop-parent", speed: 20f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Multicast(count, extra) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out _, out _, out Vector3 velocity, out _));
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(parent.Speed, velocity.z, 0.0001f);
            }
        }

        static Vector3[] SpawnMulticast(uint seed)
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("det-multicast-extra", speed: 12f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("det-multicast-parent", speed: 20f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Multicast(3, extra) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false, seed: seed);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(extra, 4);
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                var velocities = new Vector3[active.Count];
                for (int i = 0; i < active.Count; i++)
                    velocities[i] = active[i].velocity;
                System.Array.Sort(velocities, CompareVelocity);
                return velocities;
            }
        }

        static int CompareVelocity(Vector3 a, Vector3 b)
        {
            int x = a.x.CompareTo(b.x);
            if (x != 0)
                return x;
            int y = a.y.CompareTo(b.y);
            if (y != 0)
                return y;
            return a.z.CompareTo(b.z);
        }

        static float Yaw(Vector3 velocity)
        {
            return Vector3.SignedAngle(Vector3.forward, velocity, Vector3.up);
        }

        static bool DirectionsDiffer(
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active)
        {
            for (int i = 1; i < active.Count; i++)
            {
                if (Vector3.Dot(active[0].velocity.normalized, active[i].velocity.normalized) < 0.9999f)
                    return true;
            }

            return false;
        }

        sealed class FanPattern : IMulticastPattern
        {
            readonly float stepDegrees;

            public FanPattern(float stepDegrees)
            {
                this.stepDegrees = stepDegrees;
            }

            public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
            {
                float offset = (index - (count - 1) * 0.5f) * stepDegrees;
                return Quaternion.AngleAxis(offset, Vector3.up) * forward;
            }
        }
    }
}
