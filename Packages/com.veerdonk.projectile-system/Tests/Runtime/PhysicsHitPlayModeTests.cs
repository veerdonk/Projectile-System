using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veerdonk.ProjectileSystem.Tests
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
                try
                {
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
                    world.Dispose();
                }
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
                try
                {
                    Recipe child = CreateRecipe("child", speed: 8f, lifetime: 2f);
                    Recipe parent = CreateRecipe("parent", speed: 20f, lifetime: 5f);
                    parent.OnHit = new IModifier[] { new Multiply(1, child) };
                    world.Prewarm(parent, 4);
                    world.Prewarm(child, 4);

                    HitInfo? seen = null;
                    int hitCount = 0;
                    world.Hit += info =>
                    {
                        seen = info;
                        hitCount++;
                    };

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = parent,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntil(world, 1f, w => seen.HasValue && w.ActiveCount == 1);

                    Assert.AreEqual(1, world.ActiveCount);
                    Assert.AreEqual(1, hitCount);
                    Assert.IsTrue(seen.HasValue);
                    Assert.Greater(Vector3.Dot(seen.Value.Normal.normalized, Vector3.back), 0.9f);
                    Assert.IsTrue(world.TryGetActiveAt(0, out _, out _, out Vector3 velocity, out Recipe recipe));
                    Assert.AreEqual(child.Id, recipe.Id);
                    Vector3 childDir = velocity.normalized;
                    Assert.Greater(Vector3.Dot(childDir, seen.Value.Normal.normalized), 0.9f);
                    Assert.Less(Vector3.Dot(childDir, Vector3.forward), 0f);

                    for (int i = 0; i < 12; i++)
                        world.Tick(1f / 60f);

                    Assert.AreEqual(1, world.ActiveCount);
                    Assert.AreEqual(1, hitCount);
                }
                finally
                {
                    world.Dispose();
                }
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
        public IEnumerator OnHitMultiply3_DoesNotRehitSameTarget()
        {
            GameObject dummy = null;
            GameObject poolRoot = null;
            try
            {
                dummy = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(1), scale: 2f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe child = CreateRecipe("child-3", speed: 8f, lifetime: 2f);
                    Recipe parent = CreateRecipe("parent-3", speed: 20f, lifetime: 5f);
                    parent.OnHit = new IModifier[] { new Multiply(3, child) };
                    world.Prewarm(parent, 4);
                    world.Prewarm(child, 8);

                    int hitCount = 0;
                    world.Hit += _ => hitCount++;

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = parent,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntil(world, 1f, w => hitCount > 0 && w.ActiveCount == 3);

                    Assert.AreEqual(3, world.ActiveCount);
                    Assert.AreEqual(1, hitCount);

                    for (int i = 0; i < 12; i++)
                        world.Tick(1f / 60f);

                    Assert.AreEqual(3, world.ActiveCount);
                    Assert.AreEqual(1, hitCount);
                }
                finally
                {
                    world.Dispose();
                }
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
                try
                {
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
                    world.Dispose();
                }
            }
            finally
            {
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator Pierce2_HitsThreeTargets_ThenReturnsToPool()
        {
            var dummies = new List<GameObject>();
            GameObject poolRoot = null;
            try
            {
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(1)));
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 8f), new TargetId(2)));
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 12f), new TargetId(3)));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("pierce", speed: 20f, lifetime: 5f);
                    recipe.OnHit = new IModifier[] { new Pierce(2) };
                    world.Prewarm(recipe, 4);
                    int availableBefore = world.AvailableCount;
                    var seen = new List<HitInfo>();
                    world.Hit += info => seen.Add(info);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntilIdle(world, 2f);

                    Assert.AreEqual(3, seen.Count);
                    Assert.AreEqual(new TargetId(1), seen[0].TargetId);
                    Assert.AreEqual(new TargetId(2), seen[1].TargetId);
                    Assert.AreEqual(new TargetId(3), seen[2].TargetId);
                    Assert.AreEqual(0, world.ActiveCount);
                    Assert.AreEqual(availableBefore, world.AvailableCount);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                for (int i = 0; i < dummies.Count; i++)
                {
                    if (dummies[i] != null)
                        Object.Destroy(dummies[i]);
                }

                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator Pierce2_LargeCubesSmallDt_DoesNotSpendAllPierceOnFirstTarget()
        {
            var dummies = new List<GameObject>();
            GameObject poolRoot = null;
            try
            {
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 8f), new TargetId(1), scale: 2f));
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 14f), new TargetId(2), scale: 2f));
                dummies.Add(CreateDummy(new Vector3(0f, 0f, 20f), new TargetId(3), scale: 2f));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("pierce-sandbox", speed: 20f, lifetime: 5f);
                    recipe.OnHit = new IModifier[] { new Pierce(2) };
                    world.Prewarm(recipe, 4);
                    var seen = new List<HitInfo>();
                    world.Hit += info => seen.Add(info);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntil(world, 3f, w => w.ActiveCount == 0, dt: 1f / 60f);

                    Assert.AreEqual(3, seen.Count);
                    Assert.AreEqual(new TargetId(1), seen[0].TargetId);
                    Assert.AreEqual(new TargetId(2), seen[1].TargetId);
                    Assert.AreEqual(new TargetId(3), seen[2].TargetId);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                for (int i = 0; i < dummies.Count; i++)
                {
                    if (dummies[i] != null)
                        Object.Destroy(dummies[i]);
                }

                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator Bounce1_ReflectsOffWall_ThenHitsTarget()
        {
            GameObject wall = null;
            GameObject dummy = null;
            GameObject poolRoot = null;
            try
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "TestWall";
                wall.transform.position = new Vector3(8f, 0f, 8f);
                wall.transform.localScale = new Vector3(0.5f, 4f, 16f);
                dummy = CreateDummy(new Vector3(4f, 0f, 12f), new TargetId(9));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("bounce", speed: 20f, lifetime: 5f);
                    recipe.OnHit = new IModifier[] { new Bounce(1) };
                    world.Prewarm(recipe, 4);
                    int availableBefore = world.AvailableCount;
                    var seen = new List<HitInfo>();
                    world.Hit += info => seen.Add(info);

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = new Vector3(1f, 0f, 1f)
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntilIdle(world, 3f);

                    Assert.AreEqual(2, seen.Count);
                    Assert.AreEqual(HitKind.World, seen[0].Kind);
                    Assert.AreEqual(HitKind.Target, seen[1].Kind);
                    Assert.AreEqual(new TargetId(9), seen[1].TargetId);
                    Assert.AreEqual(0, world.ActiveCount);
                    Assert.AreEqual(availableBefore, world.AvailableCount);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (dummy != null)
                    Object.Destroy(dummy);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator OnTravel_AreaOverlap_HitsDamageable_IgnoresWorld()
        {
            GameObject dummy = null;
            GameObject wall = null;
            GameObject poolRoot = null;
            try
            {
                dummy = CreateDummy(new Vector3(2f, 0f, 0f), new TargetId(7));
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "TestWorld";
                wall.transform.position = new Vector3(-2f, 0f, 0f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("area-travel", speed: 20f, lifetime: 5f);
                    recipe.EffectRadius = 3f;
                    recipe.OnTravel = new IModifier[] { new AreaEffect(3f) };
                    world.Prewarm(recipe, 4);

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
                    Assert.AreEqual(HitKind.Target, seen[0].Kind);
                    Assert.AreEqual(new TargetId(7), seen[0].TargetId);
                    Assert.AreEqual(3f, seen[0].Damage, 1e-4f);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (dummy != null)
                    Object.Destroy(dummy);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator OnHit_AreaOverlap_IncludesContactedDummy()
        {
            GameObject contact = null;
            GameObject nearby = null;
            GameObject poolRoot = null;
            try
            {
                contact = CreateDummy(new Vector3(0f, 0f, 4f), new TargetId(1));
                nearby = CreateDummy(new Vector3(2f, 0f, 4f), new TargetId(2));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("area-onhit", speed: 20f, lifetime: 5f);
                    recipe.EffectRadius = 3f;
                    recipe.OnHit = new IModifier[] { new AreaEffect(3f) };
                    world.Prewarm(recipe, 4);

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

                    TickUntilIdle(world, 1f);

                    HitInfo? direct = null;
                    HitInfo? areaContact = null;
                    HitInfo? areaNearby = null;
                    for (int i = 0; i < seen.Count; i++)
                    {
                        HitInfo hit = seen[i];
                        if (hit.Source == HitSource.Direct && hit.TargetId.Equals(new TargetId(1)))
                            direct = hit;
                        else if (hit.Source == HitSource.Area && hit.TargetId.Equals(new TargetId(1)))
                            areaContact = hit;
                        else if (hit.Source == HitSource.Area && hit.TargetId.Equals(new TargetId(2)))
                            areaNearby = hit;
                    }

                    Assert.IsTrue(direct.HasValue);
                    Assert.AreEqual(1f, direct.Value.Damage, 1e-4f);
                    Assert.IsTrue(areaContact.HasValue);
                    Assert.AreEqual(3f, areaContact.Value.Damage, 1e-4f);
                    Assert.IsTrue(areaNearby.HasValue);
                    Assert.AreEqual(3f, areaNearby.Value.Damage, 1e-4f);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (nearby != null)
                    Object.Destroy(nearby);
                if (contact != null)
                    Object.Destroy(contact);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator OnTravel_GroundFieldDisk_HitsInsideXZ_IgnoresOutsideCircle()
        {
            GameObject inside = null;
            GameObject standing = null;
            GameObject beside = null;
            GameObject poolRoot = null;
            try
            {
                inside = CreateDummy(new Vector3(1f, 0f, 0f), new TargetId(1));
                standing = CreateDummy(new Vector3(0.5f, 1f, 0f), new TargetId(2));
                beside = CreateDummy(new Vector3(2.5f, 0.5f, 0f), new TargetId(3));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("ground-travel", speed: 20f, lifetime: 5f);
                    recipe.OnTravel = new IModifier[] { new GroundField(2f, 2f, 3f, intervalSeconds: 0.5f) };
                    world.Prewarm(recipe, 4);

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

                    Assert.AreEqual(2, seen.Count);
                    Assert.AreEqual(HitSource.Area, seen[0].Source);
                    Assert.AreEqual(HitSource.Area, seen[1].Source);
                    bool sawInside = false;
                    bool sawStanding = false;
                    bool sawBeside = false;
                    for (int i = 0; i < seen.Count; i++)
                    {
                        Assert.AreEqual(3f, seen[i].Damage, 1e-4f);
                        if (seen[i].TargetId == new TargetId(1))
                            sawInside = true;
                        else if (seen[i].TargetId == new TargetId(2))
                            sawStanding = true;
                        else if (seen[i].TargetId == new TargetId(3))
                            sawBeside = true;
                    }

                    Assert.IsTrue(sawInside);
                    Assert.IsTrue(sawStanding);
                    Assert.IsFalse(sawBeside);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (inside != null)
                    Object.Destroy(inside);
                if (standing != null)
                    Object.Destroy(standing);
                if (beside != null)
                    Object.Destroy(beside);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator IgnoreCollider_OverlappingWorld_FirstTickStaysLive()
        {
            GameObject wall = null;
            GameObject poolRoot = null;
            try
            {
                wall = CreateWorldCube(Vector3.zero, 4f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("ignore-overlap", speed: 5f, lifetime: 5f);
                    recipe.OnHit = new IModifier[] { new Pierce(1), new Bounce(1), new ApplyStatus(new DummyStatusEffect()) };
                    world.Prewarm(recipe, 2);

                    int hits = 0;
                    int statuses = 0;
                    world.Hit += _ => hits++;
                    world.StatusApplied += _ => statuses++;

                    ProjectileId id = world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        IgnoreCollider = wall.GetComponent<Collider>()
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    world.Tick(1f / 60f);

                    Assert.AreEqual(0, hits);
                    Assert.AreEqual(0, statuses);
                    Assert.AreEqual(1, world.ActiveCount);
                    Assert.IsTrue(world.TryGetRemaining(id, out int pierce, out int bounce));
                    Assert.AreEqual(1, pierce);
                    Assert.AreEqual(1, bounce);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator OverlappingWorld_WithoutIgnoreCollider_Despawns()
        {
            GameObject wall = null;
            GameObject poolRoot = null;
            try
            {
                wall = CreateWorldCube(Vector3.zero, 4f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("overlap-despawn", speed: 5f, lifetime: 5f);
                    world.Prewarm(recipe, 2);

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

                    world.Tick(1f / 60f);

                    Assert.IsTrue(seen.HasValue);
                    Assert.AreEqual(HitKind.World, seen.Value.Kind);
                    Assert.AreEqual(0, world.ActiveCount);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator IgnoreCollider_AfterClear_Reoverlap_IsWorldHit()
        {
            GameObject wall = null;
            GameObject poolRoot = null;
            try
            {
                wall = CreateWorldCube(Vector3.zero, 1f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("ignore-clear", speed: 20f, lifetime: 5f);
                    world.Prewarm(recipe, 2);

                    int hits = 0;
                    HitInfo? seen = null;
                    world.Hit += info =>
                    {
                        hits++;
                        seen = info;
                    };

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        IgnoreCollider = wall.GetComponent<Collider>()
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    const float dt = 0.05f;
                    for (int i = 0; i < 40 && world.ActiveCount > 0; i++)
                    {
                        world.Tick(dt);
                        if (hits != 0)
                            break;
                        if (world.TryGetActiveAt(0, out _, out Vector3 position, out _, out _) && position.z > 2f)
                            break;
                    }

                    Assert.AreEqual(0, hits);
                    Assert.AreEqual(1, world.ActiveCount);
                    Assert.IsTrue(world.TryGetActiveAt(0, out _, out Vector3 clear, out _, out _));
                    Assert.Greater(clear.z, 2f);

                    world.Tick(dt);
                    Assert.AreEqual(0, hits);
                    Assert.AreEqual(1, world.ActiveCount);
                    Assert.IsTrue(world.TryGetActiveAt(0, out _, out clear, out _, out _));

                    wall.transform.position = clear;
                    Physics.SyncTransforms();
                    world.Tick(dt);

                    Assert.AreEqual(1, hits);
                    Assert.IsTrue(seen.HasValue);
                    Assert.AreEqual(HitKind.World, seen.Value.Kind);
                    Assert.AreEqual(0, world.ActiveCount);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator IgnoreCollider_MulticastExtras_SkipWhileOverlapping()
        {
            GameObject wall = null;
            GameObject poolRoot = null;
            try
            {
                wall = CreateWorldCube(Vector3.zero, 8f);
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe extra = CreateRecipe("ignore-extra", speed: 2f, lifetime: 5f);
                    Recipe recipe = CreateRecipe("ignore-multicast", speed: 2f, lifetime: 5f);
                    recipe.OnSpawn = new IModifier[] { new Multicast(3, extra) };
                    world.Prewarm(recipe, 2);
                    world.Prewarm(extra, 4);

                    int hits = 0;
                    world.Hit += _ => hits++;

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        IgnoreCollider = wall.GetComponent<Collider>()
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    Assert.AreEqual(3, world.ActiveCount);
                    world.Tick(0.05f);

                    Assert.AreEqual(0, hits);
                    Assert.AreEqual(3, world.ActiveCount);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (poolRoot != null)
                    Object.Destroy(poolRoot);
            }
        }

        [UnityTest]
        public IEnumerator IgnoreCollider_HomingAndApplyStatus_StillHitDummy()
        {
            GameObject wall = null;
            GameObject dummy = null;
            GameObject poolRoot = null;
            try
            {
                wall = CreateWorldCube(Vector3.zero, 1f);
                dummy = CreateDummy(new Vector3(0f, 0f, 6f), new TargetId(4));
                poolRoot = new GameObject("TestPool");
                var world = new ProjectileWorld(poolRoot.transform);
                try
                {
                    Recipe recipe = CreateRecipe("ignore-home", speed: 20f, lifetime: 5f);
                    recipe.OnTick = new IModifier[] { new Homing(300f) };
                    recipe.OnHit = new IModifier[] { new ApplyStatus(new DummyStatusEffect()) };
                    world.Prewarm(recipe, 2);
                    world.SetHomingCandidate(new TargetId(4), dummy.transform.position);

                    HitInfo? seen = null;
                    int worldHits = 0;
                    StatusInfo? status = null;
                    world.Hit += info =>
                    {
                        seen = info;
                        if (info.Kind == HitKind.World)
                            worldHits++;
                    };
                    world.StatusApplied += info => status = info;

                    world.Spawn(new SpawnRequest
                    {
                        Recipe = recipe,
                        Position = Vector3.zero,
                        Direction = Vector3.forward,
                        IgnoreCollider = wall.GetComponent<Collider>()
                    });

                    Physics.SyncTransforms();
                    yield return null;

                    TickUntil(world, 1f, w => seen.HasValue);

                    Assert.AreEqual(0, worldHits);
                    Assert.IsTrue(seen.HasValue);
                    Assert.AreEqual(HitKind.Target, seen.Value.Kind);
                    Assert.AreEqual(new TargetId(4), seen.Value.TargetId);
                    Assert.IsTrue(status.HasValue);
                    Assert.AreEqual(new TargetId(4), status.Value.TargetId);
                    Assert.IsInstanceOf<DummyStatusEffect>(status.Value.Effect);
                }
                finally
                {
                    world.Dispose();
                }
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
                if (dummy != null)
                    Object.Destroy(dummy);
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

        static GameObject CreateDummy(Vector3 position, TargetId targetId, float scale = 1f)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "TestDummy";
            dummy.transform.position = position;
            dummy.transform.localScale = Vector3.one * scale;
            var marker = dummy.AddComponent<TestDamageable>();
            marker.TargetId = targetId;
            return dummy;
        }

        static GameObject CreateWorldCube(Vector3 position, float scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "TestWorld";
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * scale;
            return cube;
        }

        static void TickUntilIdle(ProjectileWorld world, float maxSeconds)
        {
            TickUntil(world, maxSeconds, w => w.ActiveCount == 0);
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
