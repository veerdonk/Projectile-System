using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Tests
{
    public sealed class PhysicsHitPlayModeTests
    {
        [UnityTest]
        public IEnumerator StraightShot_HitsTarget_ReturnsToPool()
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(7));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                Recipe recipe = CreateRecipe("straight", speed: 20f, lifetime: 5f);
                world.Prewarm(recipe, 4);
                int availableBefore = world.AvailableCount;

                HitInfo? seen = null;
                world.Hit += info => seen = info;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Physics.SyncTransforms();
                yield return null;

                TickUntilIdle(world, 1f);

                Assert.IsTrue(seen.HasValue);
                Assert.AreEqual(HitKind.Target, seen.Value.Kind);
                Assert.AreEqual(new TargetId(7), seen.Value.TargetId);
                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
            }
            finally
            {
                if (dummy != null)
                    Object.Destroy(dummy);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator OnHitSpawn_CreatesChildAfterTargetHit()
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(1));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                Recipe child = CreateRecipe("child", speed: 8f, lifetime: 2f);
                Recipe parent = CreateRecipe("parent", speed: 20f, lifetime: 5f);
                parent.OnHit = new IModifier[] { new Multiply(1, child) };
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Physics.SyncTransforms();
                yield return null;

                TickUntil(world, 1f, w => w.ActiveCount == 1);

                Assert.AreEqual(1, world.ActiveCount);
            }
            finally
            {
                if (dummy != null)
                    Object.Destroy(dummy);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator LifetimeOnly_ExpiresWithoutChild()
        {
            GameObject poolRoot = null;
            try
            {
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                Recipe recipe = CreateRecipe("lifetime", speed: 4f, lifetime: 3f);
                world.Prewarm(recipe, 4);
                int availableBefore = world.AvailableCount;
                int hitCount = 0;
                world.Hit += _ => hitCount++;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                yield return null;
                world.Tick(3f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(availableBefore, world.AvailableCount);
                Assert.AreEqual(0, hitCount);
            }
            finally
            {
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        static Recipe CreateRecipe(string id, float speed, float lifetime)
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = 0.25f,
                LifetimeSeconds = lifetime,
                PresentationId = "sphere"
            };
        }

        static GameObject CreateDummy(Vector3 position, TargetId targetId)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "TestDummy";
            dummy.transform.position = position;
            var marker = dummy.AddComponent<TestDamageable>();
            marker.TargetId = targetId;
            return dummy;
        }

        static void TickUntilIdle(ProjectileWorld world, float maxSeconds)
        {
            TickUntil(world, maxSeconds, w => w.ActiveCount == 0);
        }

        static void TickUntil(ProjectileWorld world, float maxSeconds, System.Predicate<ProjectileWorld> done)
        {
            const float dt = 0.05f;
            float elapsed = 0f;
            while (elapsed < maxSeconds && !done(world))
            {
                world.Tick(dt);
                elapsed += dt;
            }
        }

        sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public TargetId TargetId { get; set; }
        }
    }
}
