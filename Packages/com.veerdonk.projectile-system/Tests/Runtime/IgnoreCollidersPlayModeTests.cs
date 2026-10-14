using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        [UnityTest]
        public IEnumerator RetiredShot_ReleasesIgnoreSet()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("release");
            yield return Settle();

            ProjectileId id = Spawn(recipe, shooter, pet);
            Dictionary<ProjectileId, List<Collider>> sets = IgnoreSets();
            Assert.IsTrue(sets.ContainsKey(id));
            Assert.AreEqual(2, sets[id].Count);

            TickUntil(w => w.ActiveCount == 0);
            world.Tick(0.05f);

            AssertSingleTargetHit(7);
            Assert.IsFalse(sets.ContainsKey(id));
            Assert.AreEqual(0, sets.Count);
            Stack<List<Collider>> pool = IgnoreSetPool();
            Assert.GreaterOrEqual(pool.Count, 1);
            Assert.AreEqual(0, pool.Peek().Count);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecycledSet_DoesNotLeakIntoNextShot()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("recycle");
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);
            world.Tick(0.05f);
            AssertSingleTargetHit(7);
            Assert.GreaterOrEqual(IgnoreSetPool().Count, 1);

            hits.Clear();
            Spawn(recipe, shooter);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(HitKind.World, hits[0].Kind);
            Assert.Less(hits[0].Point.z, 3f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Beam_PulsePassesPet_HitsDummy()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("beam");
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            yield return Settle();

            Spawn(recipe, shooter, pet);
            for (int i = 0; i < 3; i++)
                world.Tick(0.1f);

            Assert.AreEqual(1, world.ActiveCount);
            Assert.GreaterOrEqual(hits.Count, 3);
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(HitKind.Target, hits[i].Kind);
                Assert.AreEqual(new TargetId(7), hits[i].TargetId);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GroundFieldOnHit_SkipsPet()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Dummy(new Vector3(1.5f, 0f, 5f), 50);
            Dummy(new Vector3(-1.5f, 0f, 5f), 8);
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("field-on-hit");
            recipe.OnHit = new IModifier[] { new GroundField(3f, 3f, 3f, intervalSeconds: 0.5f) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);
            for (int i = 0; i < 20; i++)
                world.Tick(0.05f);

            int otherAreaHits = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Source != HitSource.Area)
                    continue;

                Assert.AreNotEqual(new TargetId(50), hits[i].TargetId);
                if (hits[i].TargetId == new TargetId(8))
                    otherAreaHits++;
            }

            Assert.GreaterOrEqual(otherAreaHits, 2);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MultiplyGrandchildren_InheritSet()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Cube(new Vector3(0f, 0f, 3f));
            Dummy(new Vector3(0f, 0f, 6f), 1);
            Dummy(new Vector3(0f, 0f, -6f), 2);
            Recipe grandchild = Prewarmed("grandchild");
            Recipe child = Prewarmed("child-gen1");
            child.OnHit = new IModifier[] { new Multiply(1, grandchild) };
            Recipe recipe = Prewarmed("multiply-gen0");
            recipe.MultiplyGenerations = 1;
            recipe.OnHit = new IModifier[] { new Multiply(1, child) };
            yield return Settle();

            Spawn(recipe, shooter, pet);
            TickUntil(w => w.ActiveCount == 0);

            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(HitKind.Target, hits[0].Kind);
            Assert.AreEqual(new TargetId(1), hits[0].TargetId);
            Assert.AreEqual(HitKind.Target, hits[1].Kind);
            Assert.AreEqual(new TargetId(2), hits[1].TargetId);
            Assert.AreEqual(HitKind.Target, hits[2].Kind);
            Assert.AreEqual(new TargetId(1), hits[2].TargetId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HitHandlerDespawnsShot_OnExpireFieldStillSkipsPet()
        {
            Collider shooter = Cube(Vector3.zero);
            Collider pet = Dummy(new Vector3(1.5f, 0f, 5f), 50);
            Dummy(new Vector3(-1.5f, 0f, 5f), 8);
            Dummy(new Vector3(0f, 0f, 6f), 7);
            Recipe recipe = Prewarmed("despawn-in-handler");
            recipe.OnExpire = new IModifier[] { new GroundField(3f, 3f, 3f, intervalSeconds: 0.5f) };
            yield return Settle();

            var expired = new List<ExpireInfo>();
            bool despawned = false;
            world.Expired += info => expired.Add(info);
            world.Hit += info =>
            {
                if (despawned || info.Source != HitSource.Direct)
                    return;

                despawned = true;
                world.Despawn(info.InstanceId);
            };

            ProjectileId id = Spawn(recipe, shooter, pet);
            Assert.DoesNotThrow(() =>
            {
                TickUntil(w => w.ActiveCount == 0);
                for (int i = 0; i < 20; i++)
                    world.Tick(0.05f);
            });

            Assert.IsTrue(despawned);
            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual(id, expired[0].InstanceId);
            int otherAreaHits = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Source != HitSource.Area)
                    continue;

                Assert.AreNotEqual(new TargetId(50), hits[i].TargetId);
                if (hits[i].TargetId == new TargetId(8))
                    otherAreaHits++;
            }

            Assert.GreaterOrEqual(otherAreaHits, 2);
            LogAssert.NoUnexpectedReceived();
        }

        PhysicsHitQuery Query()
        {
            FieldInfo field = typeof(ProjectileWorld).GetField("hitQuery", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field);
            return (PhysicsHitQuery)field.GetValue(world);
        }

        Dictionary<ProjectileId, List<Collider>> IgnoreSets()
        {
            return (Dictionary<ProjectileId, List<Collider>>)PrivateField("ignoreSetByInstance").GetValue(Query());
        }

        Stack<List<Collider>> IgnoreSetPool()
        {
            return (Stack<List<Collider>>)PrivateField("ignoreSetPool").GetValue(Query());
        }

        static FieldInfo PrivateField(string name)
        {
            FieldInfo field = typeof(PhysicsHitQuery).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            return field;
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
