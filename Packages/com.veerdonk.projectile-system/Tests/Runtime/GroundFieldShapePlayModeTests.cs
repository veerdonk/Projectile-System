using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
{
    public sealed class GroundFieldShapePlayModeTests
    {
        readonly List<Object> spawned = new List<Object>();
        readonly List<HitInfo> hits = new List<HitInfo>();
        Transform poolRoot;
        ProjectileWorld world;

        [SetUp]
        public void SetUp()
        {
            hits.Clear();
            poolRoot = Track(new GameObject("TestPool")).transform;
            world = new ProjectileWorld(poolRoot);
            world.Hit += hits.Add;
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
        public IEnumerator Rectangle_HitsColliders_InsideTheRectangleOnly()
        {
            Dummy(new Vector3(5f, 0.5f, 0.5f), 1);
            Dummy(new Vector3(7f, 0.5f, 0f), 2);
            Dummy(new Vector3(3f, 0.5f, 2f), 3);
            Dummy(new Vector3(-1.5f, 0.5f, 0f), 4);
            Recipe recipe = Carrier(new GroundField(0f, 2f, 3f, intervalSeconds: 0.5f)
            {
                Shape = GroundFieldShape.Rectangle,
                Length = 6f,
                Width = 2f
            });
            yield return Settle();

            Plant(recipe, Vector3.right);

            CollectionAssert.AreEquivalent(new[] { new TargetId(1) }, AreaTargets());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Sector_HitsColliders_InsideTheConeOnly()
        {
            Dummy(Quaternion.AngleAxis(20f, Vector3.up) * Vector3.forward * 4f + Vector3.up * 0.5f, 1);
            Dummy(Quaternion.AngleAxis(50f, Vector3.up) * Vector3.forward * 4f + Vector3.up * 0.5f, 2);
            Dummy(new Vector3(0f, 0.5f, 6f), 3);
            Recipe recipe = Carrier(new GroundField(0f, 2f, 3f, intervalSeconds: 0.5f)
            {
                Shape = GroundFieldShape.Sector,
                Length = 5f,
                AngleDegrees = 60f
            });
            yield return Settle();

            Plant(recipe, Vector3.forward);

            CollectionAssert.AreEquivalent(new[] { new TargetId(1) }, AreaTargets());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PrimitiveViews_TakeTheShape_AndPoolPerShape()
        {
            Recipe rectangle = Carrier(new GroundField(0f, 0.25f, 3f, intervalSeconds: 0.5f)
            {
                Shape = GroundFieldShape.Rectangle,
                Length = 6f,
                Width = 2f
            });
            Recipe sector = Carrier(new GroundField(0f, 0.25f, 3f, intervalSeconds: 0.5f)
            {
                Shape = GroundFieldShape.Sector,
                Length = 5f,
                AngleDegrees = 90f
            });
            Recipe disk = Carrier(new GroundField(2f, 0.25f, 3f, intervalSeconds: 0.5f));
            yield return Settle();

            Plant(rectangle, Vector3.right);
            ProjectileView box = ActiveFieldView();
            Assert.IsNotNull(box);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), box.transform.position);
            Assert.AreEqual(new Vector3(2f, PrimitiveGroundFieldViewFactory.DiskHalfHeight * 2f, 6f), box.transform.localScale);
            Assert.Less(Vector3.Angle(Vector3.right, box.transform.forward), 0.01f);
            Assert.AreEqual(24, box.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            GameObject boxObject = box.gameObject;
            TickFieldAway();

            Plant(disk, Vector3.right);
            ProjectileView diskView = ActiveFieldView();
            Assert.AreNotSame(boxObject, diskView.gameObject);
            Assert.AreEqual(new Vector3(4f, PrimitiveGroundFieldViewFactory.DiskHalfHeight, 4f), diskView.transform.localScale);
            TickFieldAway();

            Plant(sector, Vector3.forward);
            ProjectileView fan = ActiveFieldView();
            Assert.AreEqual(Vector3.zero, fan.transform.position);
            Assert.AreEqual(5f, fan.transform.localScale.x, 1e-5f);
            Mesh mesh = fan.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual("GroundFieldSector", mesh.name);
            Assert.AreEqual(1f, mesh.bounds.max.z, 1e-4f);
            Assert.AreEqual(Mathf.Sin(45f * Mathf.Deg2Rad), mesh.bounds.max.x, 1e-4f);
            TickFieldAway();

            Plant(rectangle, Vector3.forward);
            Assert.AreSame(boxObject, ActiveFieldView().gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        Recipe Carrier(GroundField field)
        {
            var recipe = new Recipe
            {
                Id = "ground-shape-play",
                Speed = 0f,
                Radius = 0.25f,
                LifetimeSeconds = 5f,
                PresentationId = "sphere",
                OnExpire = new IModifier[] { field }
            };
            world.Prewarm(recipe, 1);
            return recipe;
        }

        void Plant(Recipe recipe, Vector3 direction)
        {
            ProjectileId id = world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = new Vector3(0f, 0.5f, 0f),
                Direction = direction
            });
            world.Despawn(id);
        }

        List<TargetId> AreaTargets()
        {
            var ids = new List<TargetId>();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Source == HitSource.Area && !ids.Contains(hits[i].TargetId))
                    ids.Add(hits[i].TargetId);
            }

            return ids;
        }

        ProjectileView ActiveFieldView()
        {
            ProjectileView[] views = poolRoot.GetComponentsInChildren<ProjectileView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].gameObject.activeSelf && views[i].gameObject.name == "GroundField")
                    return views[i];
            }

            return null;
        }

        void TickFieldAway()
        {
            for (int i = 0; i < 10; i++)
                world.Tick(0.05f);
        }

        static IEnumerator Settle()
        {
            Physics.SyncTransforms();
            yield return null;
            Physics.SyncTransforms();
        }

        void Dummy(Vector3 position, int targetId)
        {
            GameObject dummy = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            dummy.transform.position = position;
            dummy.AddComponent<TestDamageable>().TargetId = new TargetId(targetId);
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
