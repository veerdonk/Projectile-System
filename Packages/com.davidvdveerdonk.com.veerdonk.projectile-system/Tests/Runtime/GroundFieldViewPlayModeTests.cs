using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Tests
{
    public sealed class GroundFieldViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrefabFactory_RentsAuthoredMesh_AndReusesView()
        {
            GameObject poolRoot = null;
            GameObject prefab = null;
            try
            {
                poolRoot = new GameObject("TestPool");
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prefab.name = "ground-field-prefab";
                prefab.transform.localScale = new Vector3(3f, 0.05f, 3f);
                prefab.SetActive(false);

                var factory = new PrefabGroundFieldViewFactory(prefab);
                var world = new ProjectileWorld(poolRoot.transform, viewFactory: null, factory);
                try
                {
                    Recipe recipe = new Recipe
                    {
                        Id = "ground-prefab",
                        Speed = 20f,
                        Radius = 0.25f,
                        LifetimeSeconds = 5f,
                        PresentationId = "sphere",
                        OnTravel = new IModifier[] { new GroundField(2f, 0.25f, 3f, intervalSeconds: 0.5f) }
                    };
                    world.Prewarm(recipe, 1);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });
                    Physics.SyncTransforms();
                    yield return null;

                    ProjectileView first = FindGroundFieldView(poolRoot.transform);
                    Assert.IsNotNull(first);
                    Assert.IsFalse(first.ScaleToRadius);
                    Assert.AreEqual(new Vector3(3f, 0.05f, 3f), first.transform.localScale);
                    var filter = first.GetComponent<MeshFilter>();
                    Assert.IsNotNull(filter);
                    Assert.AreEqual("Cube", filter.sharedMesh.name);
                    GameObject rented = first.gameObject;

                    TickFieldAway(world, 0.5f);
                    Assert.IsFalse(rented.activeSelf);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = new Vector3(1f, 0f, 0f),
                        Direction = Vector3.forward
                    });
                    Physics.SyncTransforms();
                    yield return null;

                    ProjectileView second = FindGroundFieldView(poolRoot.transform);
                    Assert.IsNotNull(second);
                    Assert.AreSame(rented, second.gameObject);
                    Assert.AreEqual(new Vector3(3f, 0.05f, 3f), second.transform.localScale);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (prefab != null)
                    Object.Destroy(prefab);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        static ProjectileView FindGroundFieldView(Transform poolRoot)
        {
            ProjectileView[] views = poolRoot.GetComponentsInChildren<ProjectileView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                ProjectileView view = views[i];
                if (view != null && view.gameObject.activeSelf && view.gameObject.name == "GroundField")
                    return view;
            }

            return null;
        }

        static void TickFieldAway(ProjectileWorld world, float seconds)
        {
            float elapsed = 0f;
            const float dt = 0.05f;
            while (elapsed < seconds)
            {
                world.Tick(dt);
                elapsed += dt;
            }
        }
    }
}
