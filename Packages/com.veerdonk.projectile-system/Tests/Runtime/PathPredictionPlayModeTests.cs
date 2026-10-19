using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
{
    public sealed class PathPredictionPlayModeTests
    {
        const float Step = 1f / 60f;
        static readonly PathPrediction Settings = new PathPrediction { StepSeconds = Step };
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
        public IEnumerator LevelArcOverFlatGround_LandsWhereTheTwinHits()
        {
            Ground();
            Recipe recipe = ArcShot("predict-ground");
            yield return Settle();
            var request = new SpawnRequest
            {
                Recipe = recipe,
                Position = new Vector3(0f, 1.6f, 0f),
                Direction = new Vector3(0f, 0.15f, 1f)
            };
            var points = new List<Vector3>();

            Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit hit));
            Assert.AreEqual(HitKind.World, hit.Kind);
            Assert.AreEqual(0f, hit.Point.y, 0.01f);
            Assert.Greater(Vector3.Dot(hit.Normal, Vector3.up), 0.99f);
            Assert.AreEqual(0, hits.Count);

            world.Spawn(request);
            for (int i = 0; i < 600 && hits.Count == 0; i++)
                world.Tick(Step);

            Assert.AreEqual(1, hits.Count);
            Assert.Less(Vector3.Distance(hits[0].Point, hit.Point), 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StartInsideTheIgnoreCollider_DoesNotHitIt()
        {
            Ground();
            Collider shooter = Cube(new Vector3(0f, 1.6f, 0f));
            Recipe recipe = ArcShot("predict-ignore-collider");
            yield return Settle();
            var request = new SpawnRequest
            {
                Recipe = recipe,
                Position = new Vector3(0f, 1.6f, 0f),
                Direction = new Vector3(0f, 0.15f, 1f),
                IgnoreCollider = shooter
            };
            var points = new List<Vector3>();

            Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit skipped));
            Assert.AreEqual(0f, skipped.Point.y, 0.01f);
            Assert.Greater(skipped.Point.z, 5f);

            request.IgnoreCollider = null;
            Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit control));
            Assert.AreEqual(2, points.Count);
            Assert.Less(control.Point.z, 1f);

            request.IgnoreCollider = shooter;
            Assert.IsTrue(world.PredictPath(request, Settings, points, out PathHit again));
            Assert.Less(Vector3.Distance(skipped.Point, again.Point), 1e-4f);
            LogAssert.NoUnexpectedReceived();
        }

        Recipe ArcShot(string id)
        {
            var recipe = new Recipe
            {
                Id = id,
                Delivery = Delivery.Arc,
                Speed = 15f,
                Gravity = 9.81f,
                Radius = 0.2f,
                LifetimeSeconds = 5f,
                PresentationId = "sphere"
            };
            world.Prewarm(recipe, 2);
            return recipe;
        }

        static IEnumerator Settle()
        {
            Physics.SyncTransforms();
            yield return null;
            Physics.SyncTransforms();
        }

        void Ground()
        {
            GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
        }

        Collider Cube(Vector3 position)
        {
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            return cube.GetComponent<Collider>();
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }
    }
}
