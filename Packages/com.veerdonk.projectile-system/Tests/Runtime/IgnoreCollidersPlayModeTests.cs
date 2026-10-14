using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
{
    public sealed class IgnoreCollidersPlayModeTests
    {
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
        public IEnumerator Straight_PassesPet_HitsDummy()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("straight");
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Arc_PassesPet_HitsDummy()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("arc");
            recipe.Delivery = Delivery.Arc;
            recipe.Gravity = 1f;
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Hitscan_PassesPet_HitsDummy()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("hitscan");
            recipe.Delivery = Delivery.Hitscan;
            yield return Settle();

            Spawn(recipe, shooter, pet);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Pierce_SkipsPetBehindFirstDummy_HitsSecondDummy()
        {
            Collider shooter = Cube(Vector3.zero);
            Dummy(new Vector3(0f, 0f, 3f), 1);
            Collider pet = Cube(new Vector3(0f, 0f, 6f));
            Dummy(new Vector3(0f, 0f, 9f), 2);
            Recipe recipe = Prewarmed("pierce");
            recipe.OnHit = new IModifier[] { new Pierce(1) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(HitKind.Target, hits[0].Kind);
            Assert.AreEqual(new TargetId(1), hits[0].TargetId);
            Assert.AreEqual(HitKind.Target, hits[1].Kind);
            Assert.AreEqual(new TargetId(2), hits[1].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Bounce_ReturnsThroughPetAndShooter_HitsDummyBehind()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Cube(new Vector3(0f, 0f, 6f));
            Dummy(new Vector3(0f, 0f, -6f), 9);
            Recipe recipe = Prewarmed("bounce");
            recipe.OnHit = new IModifier[] { new Bounce(1) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(HitKind.World, hits[0].Kind);
            Assert.Greater(hits[0].Point.z, 4f);
            Assert.AreEqual(HitKind.Target, hits[1].Kind);
            Assert.AreEqual(new TargetId(9), hits[1].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Multicast_ExtrasSkipPet()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe extra = Prewarmed("extra");
            Recipe recipe = Prewarmed("multicast");
            recipe.OnSpawn = new IModifier[] { new Multicast(2, extra, new ParallelPattern()) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            Assert.AreEqual(2, world.ActiveCount);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(2, hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(HitKind.Target, hits[i].Kind);
                Assert.AreEqual(new TargetId(7), hits[i].TargetId);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Multiply_ChildSkipsPetAndShooter_HitsDummyBehind()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 1);
            Dummy(new Vector3(0f, 0f, -6f), 2);
            Recipe child = Prewarmed("child");
            Recipe recipe = Prewarmed("multiply");
            recipe.OnHit = new IModifier[] { new Multiply(1, child) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(HitKind.Target, hits[0].Kind);
            Assert.AreEqual(new TargetId(1), hits[0].TargetId);
            Assert.AreEqual(HitKind.Target, hits[1].Kind);
            Assert.AreEqual(new TargetId(2), hits[1].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AreaEffect_SkipsDamageablePet_HitsOtherTarget()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Dummy(new Vector3(1f, 0f, 0f), 50);
            Dummy(new Vector3(-1f, 0f, 0f), 7);
            Recipe recipe = Prewarmed("area");
            recipe.EffectRadius = 3f;
            recipe.OnTravel = new IModifier[] { new AreaEffect(3f) };
            yield return Settle();

            Spawn(recipe, shooter, pet);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitSource.Area, hits[0].Source);
            Assert.AreEqual(new TargetId(7), hits[0].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GroundField_SkipsDamageablePet_AfterSourceDespawns()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Dummy(new Vector3(1f, 0f, 0f), 50);
            Dummy(new Vector3(-1f, 0f, 0f), 7);
            Recipe recipe = Prewarmed("field");
            recipe.OnTravel = new IModifier[] { new GroundField(2f, 3f, 3f, intervalSeconds: 0.5f) };
            yield return Settle();

            ProjectileId id = Spawn(recipe, shooter, pet);
            world.Despawn(id);
            for (int i = 0; i < 30; i++)
                world.Tick(0.05f);

            Assert.GreaterOrEqual(hits.Count, 2);
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(HitSource.Area, hits[i].Source);
                Assert.AreEqual(new TargetId(7), hits[i].TargetId);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CallerClearsList_AfterSpawn_LiveShotStillSkips()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Collider dummy = Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("reuse");
            yield return Settle();

            var list = new List<Collider> { shooter, pet };
            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreColliders = list
            });
            list.Clear();
            list.Add(dummy);
            TickUntil(w => w.ActiveCount == 0);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NullIgnoreColliders_HitsPetAsWorld()
        {
            Cube(new Vector3(0f, 0f, 3f));
            Recipe recipe = Prewarmed("null-set");
            yield return Settle();

            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreColliders = null
            });
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitKind.World, hits[0].Kind);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EmptyIgnoreColliders_HitsPetAsWorld()
        {
            Cube(new Vector3(0f, 0f, 3f));
            Recipe recipe = Prewarmed("empty-set");
            yield return Settle();

            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreColliders = new Collider[0]
            });
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitKind.World, hits[0].Kind);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator IgnoreColliderAndIgnoreColliders_BothSkipped()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("both");
            yield return Settle();

            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreCollider = shooter,
                IgnoreColliders = new[] { pet }
            });
            TickUntil(w => w.ActiveCount == 0);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestroyedColliderInSet_DoesNotThrow_OthersStillSkip()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Collider gone = Cube(new Vector3(5f, 5f, 5f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("destroyed");
            yield return Settle();

            Object.Destroy(gone.gameObject);
            yield return null;
            Assert.IsTrue(gone == null);

            Assert.DoesNotThrow(() => Spawn(recipe, gone, shooter, pet));
            TickUntil(w => w.ActiveCount == 0);

            AssertSingleTargetHit(7);
            LogAssert.NoUnexpectedReceived();
        }

        ProjectileId Spawn(Recipe recipe, params Collider[] ignore)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward,
                IgnoreColliders = ignore
            });
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

        void AssertSingleTargetHit(int id)
        {
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitKind.Target, hits[0].Kind);
            Assert.AreEqual(new TargetId(id), hits[0].TargetId);
        }

        Collider Cube(Vector3 position)
        {
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            return cube.GetComponent<Collider>();
        }

        Collider Dummy(Vector3 position, int targetId)
        {
            GameObject dummy = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            dummy.transform.position = position;
            dummy.AddComponent<TestDamageable>().TargetId = new TargetId(targetId);
            return dummy.GetComponent<Collider>();
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        sealed class ParallelPattern : IMulticastPattern
        {
            public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
            {
                return forward;
            }
        }

        sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public TargetId TargetId { get; set; }
        }
    }
}
