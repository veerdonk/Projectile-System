using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
{
    public sealed class QueryBufferGrowthPlayModeTests
    {
        const int Clutter = 100;

        readonly List<Object> spawned = new List<Object>();
        readonly List<HitInfo> hits = new List<HitInfo>();
        ProjectileWorld world;

        [SetUp]
        public void SetUp()
        {
            hits.Clear();
            GameObject poolRoot = Track(new GameObject("TestPool"));
            world = new ProjectileWorld(poolRoot.transform);
            world.Hit += info => hits.Add(info);
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            world = null;
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                    Object.Destroy(spawned[i]);
            }

            spawned.Clear();
        }

        [UnityTest]
        public IEnumerator Straight_IgnoredClutterAlongPath_StillHitsDummy()
        {
            Small(new Vector3(0f, 0f, 3.2f), true, 7);
            var ignore = new List<Collider>();
            for (int i = 0; i < 40; i++)
                ignore.Add(Small(new Vector3(0f, 0f, 1f + i * 0.05f), false, 0));
            Recipe recipe = Prewarmed("straight");
            yield return Settle();

            Spawn(recipe, ignore);
            world.Tick(0.2f);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitKind.Target, hits[0].Kind);
            Assert.AreEqual(new TargetId(7), hits[0].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Straight_IgnoredClutterAtOrigin_StillHitsOverlappingDummy()
        {
            Small(new Vector3(0f, 0f, 0.05f), true, 7);
            var ignore = new List<Collider>();
            for (int i = 0; i < Clutter; i++)
                ignore.Add(Small(new Vector3(0f, 0f, i * 0.001f), false, 0));
            Recipe recipe = Prewarmed("overlap");
            yield return Settle();

            Spawn(recipe, ignore);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(new TargetId(7), hits[0].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AreaEffect_IgnoredClutter_StillHitsEveryTarget()
        {
            var ignore = new List<Collider>();
            for (int i = 0; i < Clutter; i++)
                ignore.Add(Small(new Vector3(0.5f, 0f, i * 0.001f), true, 100 + i));
            for (int i = 0; i < 20; i++)
                Small(new Vector3(-1f - i * 0.05f, 0f, 0f), true, 1 + i);
            Recipe recipe = Prewarmed("area");
            recipe.EffectRadius = 4f;
            recipe.OnTravel = new IModifier[] { new AreaEffect(4f) };
            yield return Settle();

            Spawn(recipe, ignore);

            Assert.AreEqual(20, hits.Count);
            var seen = new HashSet<TargetId>();
            for (int i = 0; i < hits.Count; i++)
                seen.Add(hits[i].TargetId);
            for (int i = 1; i <= 20; i++)
                Assert.IsTrue(seen.Contains(new TargetId(i)), "target " + i);
            LogAssert.NoUnexpectedReceived();
        }

        Recipe Prewarmed(string id)
        {
            var recipe = new Recipe
            {
                Id = id,
                Speed = 20f,
                Radius = 0.25f,
                LifetimeSeconds = 5f,
                PresentationId = "sphere"
            };
            world.Prewarm(recipe, 4);
            return recipe;
        }

        static IEnumerator Settle()
        {
            Physics.SyncTransforms();
            yield return null;
            Physics.SyncTransforms();
        }

        void TickUntil(System.Predicate<ProjectileWorld> done)
        {
            for (int i = 0; i < 60 && !done(world); i++)
                world.Tick(0.05f);
        }

        ProjectileId Spawn(Recipe recipe, IReadOnlyList<Collider> ignore)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreColliders = ignore
            });
        }

        Collider Small(Vector3 position, bool damageable, int targetId)
        {
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * 0.1f;
            if (damageable)
                cube.AddComponent<TestDamageable>().TargetId = new TargetId(targetId);
            return cube.GetComponent<Collider>();
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public TargetId TargetId { get; set; }
        }
    }
}
