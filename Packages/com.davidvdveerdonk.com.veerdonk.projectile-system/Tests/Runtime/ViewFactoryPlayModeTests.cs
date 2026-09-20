using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Tests
{
    public sealed class ViewFactoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator CubePresentation_RentsCubeView_HitsTarget()
        {
            yield return AssertPresentationHits("cube", "Cube");
        }

        [UnityTest]
        public IEnumerator SpherePresentation_RentsSphereView_HitsTarget()
        {
            yield return AssertPresentationHits("sphere", "Sphere");
        }

        [UnityTest]
        public IEnumerator ReturnToPool_ReusesSameViewGameObject()
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            GameObject cubePrefab = null;
            GameObject spherePrefab = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(3));
                poolRoot = new GameObject("TestPool");
                cubePrefab = CreatePrefabTemplate(PrimitiveType.Cube, "cube-prefab");
                spherePrefab = CreatePrefabTemplate(PrimitiveType.Sphere, "sphere-prefab");
                var factory = new PrefabViewFactory(new Dictionary<string, GameObject>
                {
                    ["cube"] = cubePrefab,
                    ["sphere"] = spherePrefab
                });
                var world = new ProjectileWorld(poolRoot.transform, factory);
                try
                {
                    Recipe recipe = CreateRecipe("cube-reuse", "cube");
                    world.Prewarm(recipe, 1);
                    int availableBefore = world.AvailableCount;

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });
                    Physics.SyncTransforms();
                    yield return null;

                    ProjectileView first = FindLiveView(poolRoot.transform);
                    Assert.IsNotNull(first);
                    GameObject rented = first.gameObject;

                    TickUntilIdle(world, 1f);
                    Assert.AreEqual(0, world.ActiveCount);
                    Assert.AreEqual(availableBefore, world.AvailableCount);
                    Assert.IsFalse(rented.activeSelf);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });
                    Physics.SyncTransforms();
                    yield return null;

                    ProjectileView second = FindLiveView(poolRoot.transform);
                    Assert.IsNotNull(second);
                    Assert.AreSame(rented, second.gameObject);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                DestroyIfPresent(dummy);
                DestroyIfPresent(poolRoot);
                DestroyIfPresent(cubePrefab);
                DestroyIfPresent(spherePrefab);
            }
        }

        [UnityTest]
        public IEnumerator UnknownPresentationId_FallsBackToSphere()
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            GameObject cubePrefab = null;
            GameObject spherePrefab = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(4));
                poolRoot = new GameObject("TestPool");
                cubePrefab = CreatePrefabTemplate(PrimitiveType.Cube, "cube-prefab");
                spherePrefab = CreatePrefabTemplate(PrimitiveType.Sphere, "sphere-prefab");
                var factory = new PrefabViewFactory(new Dictionary<string, GameObject>
                {
                    ["cube"] = cubePrefab,
                    ["sphere"] = spherePrefab
                });
                var world = new ProjectileWorld(poolRoot.transform, factory);
                try
                {
                    Recipe recipe = CreateRecipe("missing-view", "missing");
                    world.Prewarm(recipe, 1);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });
                    Physics.SyncTransforms();
                    yield return null;

                    ProjectileView view = FindLiveView(poolRoot.transform);
                    Assert.IsNotNull(view);
                    AssertMeshName(view, "Sphere");
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                DestroyIfPresent(dummy);
                DestroyIfPresent(poolRoot);
                DestroyIfPresent(cubePrefab);
                DestroyIfPresent(spherePrefab);
            }
        }

        static IEnumerator AssertPresentationHits(string presentationId, string expectedMesh)
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            GameObject cubePrefab = null;
            GameObject spherePrefab = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(7));
                poolRoot = new GameObject("TestPool");
                cubePrefab = CreatePrefabTemplate(PrimitiveType.Cube, "cube-prefab");
                spherePrefab = CreatePrefabTemplate(PrimitiveType.Sphere, "sphere-prefab");
                var factory = new PrefabViewFactory(new Dictionary<string, GameObject>
                {
                    ["cube"] = cubePrefab,
                    ["sphere"] = spherePrefab
                });
                var world = new ProjectileWorld(poolRoot.transform, factory);
                try
                {
                    Recipe recipe = CreateRecipe(presentationId + "-shot", presentationId);
                    world.Prewarm(recipe, 4);

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

                    ProjectileView view = FindLiveView(poolRoot.transform);
                    Assert.IsNotNull(view);
                    AssertMeshName(view, expectedMesh);

                    TickUntilIdle(world, 1f);

                    Assert.IsTrue(seen.HasValue);
                    Assert.AreEqual(HitKind.Target, seen.Value.Kind);
                    Assert.AreEqual(new TargetId(7), seen.Value.TargetId);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                DestroyIfPresent(dummy);
                DestroyIfPresent(poolRoot);
                DestroyIfPresent(cubePrefab);
                DestroyIfPresent(spherePrefab);
            }
        }

        static Recipe CreateRecipe(string id, string presentationId)
        {
            return new Recipe
            {
                Id = id,
                Speed = 20f,
                Radius = 0.25f,
                LifetimeSeconds = 5f,
                PresentationId = presentationId
            };
        }

        static GameObject CreatePrefabTemplate(PrimitiveType primitive, string name)
        {
            GameObject prefab = GameObject.CreatePrimitive(primitive);
            prefab.name = name;
            prefab.transform.localScale = Vector3.one * 0.5f;
            prefab.SetActive(false);
            return prefab;
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

        static ProjectileView FindLiveView(Transform poolRoot)
        {
            ProjectileView[] views = poolRoot.GetComponentsInChildren<ProjectileView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.activeSelf)
                    return views[i];
            }

            return null;
        }

        static void AssertMeshName(ProjectileView view, string expected)
        {
            var filter = view.GetComponent<MeshFilter>();
            Assert.IsNotNull(filter);
            Assert.IsNotNull(filter.sharedMesh);
            Assert.AreEqual(expected, filter.sharedMesh.name);
        }

        static void TickUntilIdle(ProjectileWorld world, float maxSeconds)
        {
            float elapsed = 0f;
            const float dt = 0.05f;
            while (elapsed < maxSeconds && world.ActiveCount > 0)
            {
                world.Tick(dt);
                elapsed += dt;
            }
        }

        static void DestroyIfPresent(GameObject go)
        {
            if (go != null)
                Object.Destroy(go);
        }

        sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public TargetId TargetId { get; set; }
        }
    }
}
