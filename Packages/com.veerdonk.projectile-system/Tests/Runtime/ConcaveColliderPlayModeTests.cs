using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
{
    public sealed class ConcaveColliderPlayModeTests
    {
        const float Radius = 0.25f;

        readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                    Object.Destroy(spawned[i]);
            }

            spawned.Clear();
        }

        [UnityTest]
        public IEnumerator StraightShot_OntoConcavePlane_OneWorldHit_NoWarning()
        {
            CreateConcavePlane();
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-straight", speed: 20f, lifetime: 5f);
                world.Prewarm(recipe, 2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 2f, 0f),
                    Direction = Vector3.down
                });
                TickUntil(world, 1f, w => w.ActiveCount == 0);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.LessOrEqual(Mathf.Abs(seen[0].Point.y), Radius + 0.01f);
                Assert.Greater(Vector3.Dot(seen[0].Normal, Vector3.up), 0.9f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ArcShot_OntoConcavePlane_Hits_NoWarning()
        {
            CreateConcavePlane();
            yield return ArcOntoGround();
        }

        [UnityTest]
        public IEnumerator ArcShot_OntoTerrain_Hits_NoWarning()
        {
            var data = Track(new TerrainData { heightmapResolution = 33, size = new Vector3(20f, 1f, 20f) });
            GameObject terrain = Track(Terrain.CreateTerrainGameObject(data));
            terrain.transform.position = new Vector3(-10f, 0f, -10f);
            yield return ArcOntoGround();
        }

        [UnityTest]
        public IEnumerator Pierce1_Multiply_ThroughConcaveTarget_SpawnsChildPastFarSide()
        {
            Vector3 center = new Vector3(0f, 0f, 4f);
            CreateConcaveCube(center, 1f, new TargetId(1));
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe child = CreateRecipe("concave-pierce-child", speed: 8f, lifetime: 2f);
                Recipe parent = CreateRecipe("concave-pierce-parent", speed: 20f, lifetime: 5f);
                parent.OnHit = new IModifier[] { new Pierce(1), new Multiply(1, child) };
                world.Prewarm(parent, 4);
                world.Prewarm(child, 4);
                int hitCount = 0;
                world.Hit += _ => hitCount++;

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                TickUntil(world, 1f, w => hitCount > 0 && w.ActiveCount == 2);

                Assert.AreEqual(1, hitCount);
                Assert.IsTrue(TryFindRecipe(world, child.Id, out Vector3 position, out Vector3 velocity));
                Vector3 expected = center + Vector3.forward * (0.5f + child.Radius + 0.01f);
                Assert.AreEqual(expected.x, position.x, 0.05f);
                Assert.AreEqual(expected.y, position.y, 0.05f);
                Assert.AreEqual(expected.z, position.z, 0.05f);
                Assert.Greater(Vector3.Dot(velocity.normalized, Vector3.forward), 0.9f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Pierce1_ThroughThickConcaveTarget_SmallSteps_HitsOnce()
        {
            CreateConcaveCube(new Vector3(0f, 0f, 4f), 3f, new TargetId(1));
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-thick-pierce", speed: 20f, lifetime: 5f);
                recipe.OnHit = new IModifier[] { new Pierce(1) };
                world.Prewarm(recipe, 2);
                int hitCount = 0;
                world.Hit += _ => hitCount++;

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                TickUntil(
                    world,
                    1f,
                    w => w.ActiveCount == 0 || (w.TryGetActiveAt(0, out _, out Vector3 at, out _, out _) && at.z > 7f),
                    dt: 0.01f);

                Assert.AreEqual(1, hitCount);
                Assert.AreEqual(1, world.ActiveCount);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PierceAndBounce_OffConcaveTargetGround_HitsItAgain()
        {
            CreateSlabWithSpire(new TargetId(3));
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-pierce-bounce", speed: 3f, lifetime: 5f);
                recipe.Delivery = Delivery.Arc;
                recipe.Gravity = 20f;
                recipe.OnHit = new IModifier[] { new Pierce(1), new Bounce(2) };
                world.Prewarm(recipe, 2);
                var hits = new List<HitInfo>();
                world.Hit += info =>
                {
                    if (info.TargetId == new TargetId(3))
                        hits.Add(info);
                };

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1f, 0f),
                    Direction = Vector3.down
                });
                TickUntil(world, 2f, _ => hits.Count >= 2, dt: 0.005f);

                Assert.GreaterOrEqual(hits.Count, 2);
                Assert.Greater(hits[1].Normal.y, 0.5f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator OverlapPick_ConcaveFloorRayMiss_LosesToCloserBoxTarget()
        {
            CreateConcavePlane();
            GameObject dummy = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            dummy.transform.position = new Vector3(0f, 0.2f, 0.3f);
            dummy.transform.localScale = Vector3.one * 0.5f;
            dummy.AddComponent<TestDamageable>().TargetId = new TargetId(5);
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-floor-pick", speed: 5f, lifetime: 5f);
                world.Prewarm(recipe, 2);
                HitInfo? seen = null;
                world.Hit += info => seen ??= info;

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 0.2f, 0f),
                    Direction = Vector3.forward
                });
                world.Tick(0.01f);

                Assert.IsTrue(seen.HasValue);
                Assert.AreEqual(HitKind.Target, seen.Value.Kind);
                Assert.AreEqual(new TargetId(5), seen.Value.TargetId);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Bounce2_OffConcaveGround_HitsThatGroundAgain()
        {
            CreateConcavePlane();
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-bounce", speed: 4f, lifetime: 5f);
                recipe.Delivery = Delivery.Arc;
                recipe.Gravity = 20f;
                recipe.OnHit = new IModifier[] { new Bounce(2) };
                world.Prewarm(recipe, 2);
                int worldHits = 0;
                world.Hit += info =>
                {
                    if (info.Kind == HitKind.World)
                        worldHits++;
                };

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1f, 0f),
                    Direction = Vector3.forward
                });
                TickUntil(world, 4f, _ => worldHits >= 2, dt: 0.02f);

                Assert.GreaterOrEqual(worldHits, 2);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ConcaveIgnoreCollider_ClearsOnceShotIsClear()
        {
            GameObject wall = CreateConcaveCube(Vector3.zero, 1f, null);
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-ignore", speed: 20f, lifetime: 5f);
                world.Prewarm(recipe, 2);
                int hits = 0;
                HitInfo? seen = null;
                world.Hit += info =>
                {
                    hits++;
                    seen = info;
                };

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 0f, 0.45f),
                    Direction = Vector3.forward,
                    IgnoreCollider = wall.GetComponent<Collider>()
                });

                const float dt = 0.05f;
                for (int i = 0; i < 40 && hits == 0; i++)
                {
                    world.Tick(dt);
                    if (world.TryGetActiveAt(0, out _, out Vector3 at, out _, out _) && at.z > 2f)
                        break;
                }

                Assert.AreEqual(0, hits);
                world.Tick(dt);
                Assert.AreEqual(0, hits);
                Assert.IsTrue(world.TryGetActiveAt(0, out _, out Vector3 clear, out _, out _));

                wall.transform.position = clear + Vector3.forward * 0.5f;
                Physics.SyncTransforms();
                world.Tick(dt);

                Assert.AreEqual(1, hits);
                Assert.AreEqual(HitKind.World, seen.Value.Kind);
                Assert.AreEqual(0, world.ActiveCount);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AreaOverlap_OnConcaveDamageable_Hits_NoWarning()
        {
            CreateConcaveCube(new Vector3(2f, 0f, 0f), 1f, new TargetId(7));
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-area", speed: 20f, lifetime: 5f);
                recipe.EffectRadius = 3f;
                recipe.OnTravel = new IModifier[] { new AreaEffect(3f) };
                world.Prewarm(recipe, 2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                Physics.SyncTransforms();
                yield return null;
                Physics.SyncTransforms();

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(new TargetId(7), seen[0].TargetId);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        IEnumerator ArcOntoGround()
        {
            GameObject poolRoot = Track(new GameObject("TestPool"));
            var world = new ProjectileWorld(poolRoot.transform);
            try
            {
                Recipe recipe = CreateRecipe("concave-arc", speed: 5f, lifetime: 5f);
                recipe.Delivery = Delivery.Arc;
                recipe.Gravity = 10f;
                world.Prewarm(recipe, 2);
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);

                Physics.SyncTransforms();
                yield return null;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = new Vector3(0f, 1f, 0f),
                    Direction = Vector3.forward
                });
                TickUntil(world, 3f, w => w.ActiveCount == 0, dt: 0.02f);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitKind.World, seen[0].Kind);
                Assert.LessOrEqual(Mathf.Abs(seen[0].Point.y), Radius + 0.01f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                world.Dispose();
            }
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        void CreateConcavePlane()
        {
            GameObject plane = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            plane.name = "TestConcavePlane";
            Assert.IsFalse(plane.GetComponent<MeshCollider>().convex);
        }

        void CreateSlabWithSpire(TargetId target)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            var vertices = new List<Vector3>();
            foreach (Vector3 v in cubeMesh.vertices)
                vertices.Add(new Vector3(v.x * 10f, v.y - 0.5f, v.z * 10f));
            var triangles = new List<int>(cubeMesh.triangles);
            Object.DestroyImmediate(cube);

            int spire = vertices.Count;
            vertices.Add(new Vector3(4f, 3f, 4f));
            vertices.Add(new Vector3(4.5f, 3f, 4f));
            vertices.Add(new Vector3(4f, 3f, 4.5f));
            triangles.AddRange(new[] { spire, spire + 1, spire + 2, spire, spire + 2, spire + 1 });

            var mesh = Track(new Mesh());
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            GameObject slab = Track(new GameObject("TestSlabWithSpire"));
            var collider = slab.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
            slab.AddComponent<TestDamageable>().TargetId = target;
        }

        GameObject CreateConcaveCube(Vector3 position, float scale, TargetId? target)
        {
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.name = "TestConcaveCube";
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * scale;
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            var mesh = cube.AddComponent<MeshCollider>();
            mesh.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = false;
            if (target.HasValue)
                cube.AddComponent<TestDamageable>().TargetId = target.Value;
            return cube;
        }

        static Recipe CreateRecipe(string id, float speed, float lifetime)
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = Radius,
                LifetimeSeconds = lifetime,
                PresentationId = "sphere"
            };
        }

        static bool TryFindRecipe(ProjectileWorld world, string recipeId, out Vector3 position, out Vector3 velocity)
        {
            for (int i = 0; world.TryGetActiveAt(i, out _, out Vector3 at, out Vector3 vel, out Recipe recipe); i++)
            {
                if (recipe.Id != recipeId)
                    continue;

                position = at;
                velocity = vel;
                return true;
            }

            position = default;
            velocity = default;
            return false;
        }

        static void TickUntil(ProjectileWorld world, float maxSeconds, System.Predicate<ProjectileWorld> done, float dt = 0.05f)
        {
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
