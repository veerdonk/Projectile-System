using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    public sealed class MultiplyTests
    {
        [Test]
        public void Multiply2_OnInterval_SpawnsTwoChildrenAlongTravel_AfterHalfSecond()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("interval-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("interval-parent", speed: 10f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child) };

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

                world.Tick(0.25f);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.25f);
                Assert.AreEqual(3, world.ActiveCount);

                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                int childCount = 0;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].recipe.Id != child.Id)
                        continue;

                    childCount++;
                    Assert.AreEqual(2.5f, active[i].position.z, 0.0001f);
                    Vector3 dir = active[i].velocity.normalized;
                    Assert.Greater(Vector3.Dot(dir, Vector3.forward), 0.9f);
                    Assert.AreEqual(child.Speed, active[i].velocity.magnitude, 0.0001f);
                }

                Assert.AreEqual(2, childCount);
                Assert.IsTrue(ChildDirectionsDiffer(active, child.Id));
            }
        }

        [Test]
        public void Multiply2_OnInterval_LargeDt_FiresEachCrossing()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("dt-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("dt-parent", speed: 10f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 16);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(1f);

                Assert.AreEqual(5, world.ActiveCount);
            }
        }

        [Test]
        public void Multiply_OnInterval_DoesNotFireOnExpireTick()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("expire-skip-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("expire-skip-parent", speed: 10f, lifetime: 0.5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);

                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Multiply1_OnExpire_SpawnsChildFromLifetime()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("expire-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("expire-parent", speed: 10f, lifetime: 0.5f);
            parent.OnExpire = new IModifier[] { new Multiply(1, child) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out _, out Vector3 position, out Vector3 velocity, out Recipe recipe));
                Assert.AreEqual(child.Id, recipe.Id);
                Assert.AreEqual(0f, position.x, 0.0001f);
                Assert.AreEqual(0f, position.y, 0.0001f);
                Assert.AreEqual(0f, position.z, 0.0001f);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(0f, velocity.y, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void Multiply1_OnExpire_ChildDoesNotInheritMultiply_ByDefault()
        {
            (ProjectileWorld world, Recipe parent) = CreateExpireChain(generations: 0);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.5f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Multiply1_OnExpire_ChildInheritsMultiply_ForOneGeneration()
        {
            (ProjectileWorld world, Recipe parent) = CreateExpireChain(generations: 1);
            using (world)
            {
                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                world.Tick(0.5f);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.5f);
                Assert.AreEqual(1, world.ActiveCount);

                world.Tick(0.5f);
                Assert.AreEqual(0, world.ActiveCount);
            }
        }

        [Test]
        public void Prewarm_CoversOnExpireChildren()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("prewarm-expire-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("prewarm-expire-parent", speed: 10f, lifetime: 1f);
            parent.OnExpire = new IModifier[] { new Multiply(1, child) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 2);

                Assert.AreEqual(4, world.AvailableCount);
            }
        }

        [Test]
        public void Prewarm_CoversIntervalChildrenAliveAtOnce()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("prewarm-interval-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("prewarm-interval-parent", speed: 10f, lifetime: 5f);
            parent.OnSpawn = new IModifier[] { new Interval(0.5f) };
            parent.OnInterval = new IModifier[] { new Multiply(2, child) };

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            using (world)
            {
                world.Prewarm(parent, 1);

                Assert.AreEqual(9, world.AvailableCount);
            }
        }

        static (ProjectileWorld world, Recipe parent) CreateExpireChain(int generations)
        {
            Recipe child = SimulationTestHelper.CreateRecipe("chain-child", speed: 8f, lifetime: 0.5f);
            Recipe parent = SimulationTestHelper.CreateRecipe("chain-parent", speed: 10f, lifetime: 0.5f);
            child.OnExpire = new IModifier[] { new Multiply(1, child) };
            parent.OnExpire = new IModifier[] { new Multiply(1, child) };
            parent.MultiplyGenerations = generations;

            var world = new ProjectileWorld(new ScriptedHitQuery(), createViews: false);
            world.Prewarm(parent, 4);
            return (world, parent);
        }

        [Test]
        public void Multiply1_OnExpire_KillingHit_UsesHitNormal()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("kill-expire-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("kill-expire-parent", speed: 20f, lifetime: 5f);
            parent.OnExpire = new IModifier[] { new Multiply(1, child) };

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Vector3 point = new Vector3(1f, 2f, 3f);
                hits.Queue(HitKind.Target, point, Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(1, world.ActiveCount);
                Assert.IsTrue(world.TryGetActiveAt(0, out _, out Vector3 position, out Vector3 velocity, out Recipe recipe));
                Assert.AreEqual(child.Id, recipe.Id);
                Assert.AreEqual(SimulationTestHelper.OffSurface(point, Vector3.up, child.Radius), position);
                Assert.AreEqual(0f, velocity.x, 0.0001f);
                Assert.AreEqual(child.Speed, velocity.y, 0.0001f);
                Assert.AreEqual(0f, velocity.z, 0.0001f);
            }
        }

        [Test]
        public void KillingHit_RunsOnHitThenOnExpire()
        {
            Recipe hitChild = SimulationTestHelper.CreateRecipe("hit-child", speed: 8f, lifetime: 2f);
            Recipe expireChild = SimulationTestHelper.CreateRecipe("expire-child-2", speed: 6f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("hit-expire-parent", speed: 20f, lifetime: 5f);
            parent.OnHit = new IModifier[] { new Multiply(1, hitChild) };
            parent.OnExpire = new IModifier[] { new Multiply(1, expireChild) };

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            using (world)
            {
                world.Prewarm(parent, 4);
                world.Prewarm(hitChild, 4);
                world.Prewarm(expireChild, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.up, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(2, world.ActiveCount);
                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                Assert.IsTrue(HasRecipe(active, hitChild.Id));
                Assert.IsTrue(HasRecipe(active, expireChild.Id));
            }
        }

        [Test]
        public void Multiply3_OnHit_SpreadsAroundHitNormal()
        {
            Recipe child = SimulationTestHelper.CreateRecipe("spread-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("spread-parent", speed: 20f, lifetime: 5f);
            parent.OnHit = new IModifier[] { new Multiply(3, child) };

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false, seed: 17u);
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

                Assert.AreEqual(3, world.ActiveCount);
                List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active =
                    SimulationTestHelper.CollectActive(world);
                for (int i = 0; i < active.Count; i++)
                {
                    Assert.AreEqual(child.Id, active[i].recipe.Id);
                    Vector3 fromPoint = active[i].position - point;
                    Assert.AreEqual(child.Radius + 0.01f, fromPoint.magnitude, 0.0001f);
                    Vector3 dir = active[i].velocity.normalized;
                    Assert.Greater(Vector3.Dot(fromPoint.normalized, dir), 0.999f);
                    Assert.Greater(Vector3.Dot(dir, normal), 0.9f);
                    Assert.AreEqual(child.Speed, active[i].velocity.magnitude, 0.0001f);
                }

                Assert.IsTrue(ChildDirectionsDiffer(active, child.Id));
            }
        }

        [Test]
        public void Multiply3_OnHit_SameSeed_IsDeterministic()
        {
            Vector3[] first = SpawnSpreadChildren(seed: 17u);
            Vector3[] second = SpawnSpreadChildren(seed: 17u);
            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].x, second[i].x, 0.0001f);
                Assert.AreEqual(first[i].y, second[i].y, 0.0001f);
                Assert.AreEqual(first[i].z, second[i].z, 0.0001f);
            }
        }

        static Vector3[] SpawnSpreadChildren(uint seed)
        {
            Recipe child = SimulationTestHelper.CreateRecipe("det-child", speed: 8f, lifetime: 2f);
            Recipe parent = SimulationTestHelper.CreateRecipe("det-parent", speed: 20f, lifetime: 5f);
            parent.OnHit = new IModifier[] { new Multiply(3, child) };

            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false, seed: seed);
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
                hits.Queue(HitKind.Target, Vector3.zero, Vector3.up, new TargetId(1));
                world.Tick(0.1f);

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

        static bool HasRecipe(
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active,
            string recipeId)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id == recipeId)
                    return true;
            }

            return false;
        }

        static bool ChildDirectionsDiffer(
            List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> active,
            string childId)
        {
            Vector3? first = null;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].recipe.Id != childId)
                    continue;

                Vector3 dir = active[i].velocity.normalized;
                if (!first.HasValue)
                {
                    first = dir;
                    continue;
                }

                if (Vector3.Dot(first.Value, dir) < 0.9999f)
                    return true;
            }

            return false;
        }
    }
}
