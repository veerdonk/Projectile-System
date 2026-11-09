using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class AreaEffectOwnRadiusTests
    {
        static readonly Vector3 Contact = new Vector3(0f, 0f, 2f);

        RecipeAsset asset;
        ModifierKit kit;

        [TearDown]
        public void TearDown()
        {
            if (asset != null)
                Object.DestroyImmediate(asset);
            if (kit != null)
                Object.DestroyImmediate(kit);
        }

        [Test]
        public void TwoAreaEffects_OnHit_WithDifferentOwnRadii_HitDifferentTargets()
        {
            var contact = new TargetId(1);
            var near = new TargetId(2);
            var far = new TargetId(3);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-two",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 1f,
                onHit: new IModifier[] { new AreaEffect(5f, 1.5f), new AreaEffect(7f, 4f) });

            HitOnce(
                recipe,
                hits =>
                {
                    hits.AddOverlapTarget(contact, Contact);
                    hits.AddOverlapTarget(near, Contact + new Vector3(1f, 0f, 0f));
                    hits.AddOverlapTarget(far, Contact + new Vector3(3f, 0f, 0f));
                },
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(2, bursts.Count);
            Assert.AreEqual(1.5f, bursts[0].Radius, 1e-4f);
            Assert.AreEqual(4f, bursts[1].Radius, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { contact, near }, TargetsWithDamage(areaHits, 5f));
            CollectionAssert.AreEquivalent(new[] { contact, near, far }, TargetsWithDamage(areaHits, 7f));
            foreach (HitInfo hit in areaHits)
                Assert.AreEqual(Mathf.Approximately(hit.Damage, 5f) ? 1.5f : 4f, hit.EffectRadius, 1e-4f);
        }

        [Test]
        public void OwnRadius0_UsesTheRecipeEffectRadius()
        {
            var contact = new TargetId(1);
            var inside = new TargetId(2);
            var outside = new TargetId(3);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-zero",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { new AreaEffect(3f) });

            HitOnce(
                recipe,
                hits =>
                {
                    hits.AddOverlapTarget(contact, Contact);
                    hits.AddOverlapTarget(inside, Contact + new Vector3(2.5f, 0f, 0f));
                    hits.AddOverlapTarget(outside, Contact + new Vector3(3.5f, 0f, 0f));
                },
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(0f, ((AreaEffect)recipe.OnHit[0]).Radius);
            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { contact, inside }, TargetsWithDamage(areaHits, 3f));
            foreach (HitInfo hit in areaHits)
                Assert.AreEqual(3f, hit.EffectRadius, 1e-4f);
        }

        [Test]
        public void OwnRadius_WithRecipeEffectRadius0_StillBursts()
        {
            var contact = new TargetId(1);
            var nearby = new TargetId(2);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-recipe-zero",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 0f,
                onHit: new IModifier[] { new AreaEffect(3f, 2f) });

            HitOnce(
                recipe,
                hits =>
                {
                    hits.AddOverlapTarget(contact, Contact);
                    hits.AddOverlapTarget(nearby, Contact + new Vector3(1.5f, 0f, 0f));
                },
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { contact, nearby }, TargetsWithDamage(areaHits, 3f));
        }

        [Test]
        public void EffectRadius2_InTheSameOnHitList_DoublesTheOwnRadius()
        {
            var contact = new TargetId(1);
            var doubled = new TargetId(2);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-scaled-onhit",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 1f,
                onHit: new IModifier[] { new EffectRadius(2f), new AreaEffect(3f, 1.5f) });

            HitOnce(
                recipe,
                hits =>
                {
                    hits.AddOverlapTarget(contact, Contact);
                    hits.AddOverlapTarget(doubled, Contact + new Vector3(2.5f, 0f, 0f));
                },
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { contact, doubled }, TargetsWithDamage(areaHits, 3f));
            foreach (HitInfo hit in areaHits)
                Assert.AreEqual(3f, hit.EffectRadius, 1e-4f);
            Assert.AreEqual(1.5f, ((AreaEffect)recipe.OnHit[1]).Radius);
        }

        [Test]
        public void EffectRadius2_OnTravel_DoublesTheOwnRadiusAtTheLaterHit()
        {
            var contact = new TargetId(1);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-scaled-travel",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 1f,
                onTravel: new IModifier[] { new EffectRadius(2f) },
                onHit: new IModifier[] { new AreaEffect(3f, 1.5f), new AreaEffect(4f) });

            HitOnce(
                recipe,
                hits => hits.AddOverlapTarget(contact, Contact),
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> _);

            Assert.AreEqual(2, bursts.Count);
            Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
            Assert.AreEqual(2f, bursts[1].Radius, 1e-4f);
        }

        [Test]
        public void Occupancy_EnterAndExit_ReportTheOwnRadius()
        {
            var target = new TargetId(1);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-occupancy",
                speed: 1f,
                lifetime: 5f,
                effectRadius: 5f,
                onTick: new IModifier[] { new AreaEffect(3f, 2f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                var bursts = new List<AreaBurstInfo>();
                world.Hit += seen.Add;
                world.AreaBurst += bursts.Add;
                hits.AddOverlapTarget(target, new Vector3(0.5f, 0f, 0f));
                hits.AddOverlapTarget(new TargetId(2), new Vector3(3.5f, 0f, 0f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.1f);
                hits.ClearOverlaps();
                world.Tick(0.1f);

                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(HitSource.OccupancyEnter, seen[0].Source);
                Assert.AreEqual(HitSource.OccupancyExit, seen[1].Source);
                Assert.AreEqual(target, seen[0].TargetId);
                Assert.AreEqual(target, seen[1].TargetId);
                Assert.AreEqual(2f, seen[0].EffectRadius, 1e-4f);
                Assert.AreEqual(2f, seen[1].EffectRadius, 1e-4f);
                Assert.AreEqual(1, bursts.Count);
                Assert.IsTrue(bursts[0].Occupancy);
                Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
            }
        }

        [Test]
        public void OnTravel_UsesTheOwnRadius()
        {
            var inside = new TargetId(1);
            var outside = new TargetId(2);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-travel",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 5f,
                onTravel: new IModifier[] { new AreaEffect(3f, 2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                var areaHits = new List<HitInfo>();
                world.AreaBurst += bursts.Add;
                world.Hit += areaHits.Add;
                hits.AddOverlapTarget(inside, new Vector3(1.5f, 0f, 0f));
                hits.AddOverlapTarget(outside, new Vector3(3f, 0f, 0f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(RecipeStage.OnTravel, bursts[0].Stage);
                Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
                Assert.AreEqual(1, areaHits.Count);
                Assert.AreEqual(HitSource.Area, areaHits[0].Source);
                Assert.AreEqual(inside, areaHits[0].TargetId);
                Assert.AreEqual(2f, areaHits[0].EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void OnInterval_UsesTheOwnRadius()
        {
            var inside = new TargetId(1);
            var outside = new TargetId(2);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-interval",
                speed: 1f,
                lifetime: 5f,
                effectRadius: 5f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new AreaEffect(3f, 2f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                var areaHits = new List<HitInfo>();
                world.AreaBurst += bursts.Add;
                world.Hit += areaHits.Add;
                hits.AddOverlapTarget(inside, new Vector3(1.5f, 0f, 0.4f));
                hits.AddOverlapTarget(outside, new Vector3(3f, 0f, 0.4f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.25f);
                Assert.AreEqual(0, bursts.Count);

                world.Tick(0.25f);

                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(RecipeStage.OnInterval, bursts[0].Stage);
                Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
                Assert.AreEqual(1, areaHits.Count);
                Assert.AreEqual(inside, areaHits[0].TargetId);
                Assert.AreEqual(2f, areaHits[0].EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void EffectRadiusHalf_HalvesTheOwnRadius()
        {
            var contact = new TargetId(1);
            var inside = new TargetId(2);
            var outside = new TargetId(3);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-halved",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 5f,
                onHit: new IModifier[] { new EffectRadius(0.5f), new AreaEffect(3f, 4f) });

            HitOnce(
                recipe,
                hits =>
                {
                    hits.AddOverlapTarget(contact, Contact);
                    hits.AddOverlapTarget(inside, Contact + new Vector3(1.5f, 0f, 0f));
                    hits.AddOverlapTarget(outside, Contact + new Vector3(3f, 0f, 0f));
                },
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { contact, inside }, TargetsWithDamage(areaHits, 3f));
        }

        [Test]
        public void EffectRadiusZeroOrNegative_LeavesTheOwnRadius()
        {
            var contact = new TargetId(1);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-nonpositive-scale",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 5f,
                onHit: new IModifier[] { new EffectRadius(0f), new EffectRadius(-2f), new AreaEffect(3f, 1.5f) });

            HitOnce(
                recipe,
                hits => hits.AddOverlapTarget(contact, Contact),
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> areaHits);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(1.5f, bursts[0].Radius, 1e-4f);
            Assert.AreEqual(1.5f, areaHits[0].EffectRadius, 1e-4f);
        }

        [Test]
        public void MultiplyChild_BurstsItsOwnRadius_NotTheParentScale()
        {
            Recipe child = SimulationTestHelper.CreateRecipe(
                "own-radius-child",
                speed: 8f,
                lifetime: 2f,
                onTravel: new IModifier[] { new AreaEffect(1f, 1.5f) });
            Recipe parent = SimulationTestHelper.CreateRecipe(
                "own-radius-parent",
                speed: 10f,
                lifetime: 1f,
                onTravel: new IModifier[] { new EffectRadius(2f) },
                onExpire: new IModifier[] { new Multiply(1, child) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(parent);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                world.AreaBurst += bursts.Add;

                ProjectileId parentId = world.Spawn(new SpawnRequest
                {
                    Recipe = parent,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);
                world.Tick(0.5f);
                world.Tick(0.1f);

                Assert.AreEqual(1, bursts.Count);
                Assert.AreNotEqual(parentId, bursts[0].InstanceId);
                Assert.AreEqual(RecipeStage.OnTravel, bursts[0].Stage);
                Assert.AreEqual(1.5f, bursts[0].Radius, 1e-4f);
            }
        }

        [Test]
        public void PooledEntity_StartsTheNextShotAtScaleOne()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-pool",
                speed: 10f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Interval(0.5f) },
                onInterval: new IModifier[] { new EffectRadius(2f) },
                onExpire: new IModifier[] { new AreaEffect(1f, 1.5f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 1);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                world.AreaBurst += bursts.Add;
                Assert.AreEqual(1, world.AvailableCount);

                ProjectileId first = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.25f);
                world.Tick(0.25f);
                world.Despawn(first);
                Assert.AreEqual(1, world.AvailableCount);

                ProjectileId second = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                Assert.AreEqual(0, world.AvailableCount);
                world.Despawn(second);

                Assert.AreEqual(2, bursts.Count);
                Assert.AreEqual(first, bursts[0].InstanceId);
                Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
                Assert.AreEqual(second, bursts[1].InstanceId);
                Assert.AreEqual(1.5f, bursts[1].Radius, 1e-4f);
            }
        }

        [Test]
        public void OnExpire_UsesTheOwnRadius()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-expire",
                speed: 10f,
                lifetime: 1f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f, 3.5f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var inside = new TargetId(1);
                var outside = new TargetId(2);
                var bursts = new List<AreaBurstInfo>();
                var areaHits = new List<HitInfo>();
                world.AreaBurst += bursts.Add;
                world.Hit += info =>
                {
                    if (info.Source == HitSource.Area)
                        areaHits.Add(info);
                };
                hits.AddOverlapTarget(inside, new Vector3(0f, 0f, 8f));
                hits.AddOverlapTarget(outside, new Vector3(0f, 0f, 9f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);
                Assert.AreEqual(0, bursts.Count);

                world.Tick(0.5f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(new Vector3(0f, 0f, 5f), bursts[0].Center);
                Assert.AreEqual(RecipeStage.OnExpire, bursts[0].Stage);
                Assert.AreEqual(3.5f, bursts[0].Radius, 1e-4f);
                Assert.AreEqual(1, areaHits.Count);
                Assert.AreEqual(inside, areaHits[0].TargetId);
                Assert.AreEqual(3.5f, areaHits[0].EffectRadius, 1e-4f);
            }
        }

        [Test]
        public void NegativeRadius_ReadsZero_AndUsesTheRecipeRadius()
        {
            var contact = new TargetId(1);
            var area = new AreaEffect(3f, -2f);
            Assert.AreEqual(0f, area.Radius);
            Assert.AreEqual(0f, new AreaEffect(3f) { Radius = -1f }.Radius);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "own-radius-negative",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onHit: new IModifier[] { area });

            HitOnce(
                recipe,
                hits => hits.AddOverlapTarget(contact, Contact),
                contact,
                out List<AreaBurstInfo> bursts,
                out List<HitInfo> _);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(3f, bursts[0].Radius, 1e-4f);
        }

        [Test]
        public void Radius_IsSettable()
        {
            var area = new AreaEffect(3f) { Radius = 2.5f };

            Assert.AreEqual(2.5f, area.Radius);
            Assert.AreEqual(3f, area.Damage);
        }

        [Test]
        public void ToRecipe_AndClone_CopyTheOwnRadius()
        {
            asset = ScriptableObject.CreateInstance<RecipeAsset>();
            asset.Id = "own-radius-asset";
            asset.OnHit = new IModifier[] { new AreaEffect(3f, 2.5f, null, false, false, true) };

            Recipe recipe = asset.ToRecipe();
            Recipe clone = recipe.Clone();

            var fromAsset = (AreaEffect)recipe.OnHit[0];
            var fromClone = (AreaEffect)clone.OnHit[0];
            Assert.AreNotSame(asset.OnHit[0], fromAsset);
            Assert.AreNotSame(fromAsset, fromClone);
            foreach (AreaEffect copy in new[] { fromAsset, fromClone })
            {
                Assert.AreEqual(2.5f, copy.Radius);
                Assert.AreEqual(3f, copy.Damage);
                Assert.IsFalse(copy.IncludeDirectHit);
                Assert.IsFalse(copy.IncludeDirectStatus);
                Assert.IsTrue(copy.Occupancy);
            }
        }

        [Test]
        public void Kit_CopiesTheOwnRadius()
        {
            var authored = new AreaEffect(3f, 2.5f);
            kit = ScriptableObject.CreateInstance<ModifierKit>();
            kit.Layers = new[] { new ModifierKitLayer(RecipeStage.OnHit, authored) };
            Recipe basis = SimulationTestHelper.CreateRecipe("own-radius-kit-basis");

            Recipe composed = Recipe.Compose(basis, "own-radius-kit", kit);

            Assert.AreEqual(1, composed.OnHit.Length);
            var copy = (AreaEffect)composed.OnHit[0];
            Assert.AreNotSame(authored, copy);
            Assert.AreEqual(2.5f, copy.Radius);
        }

        [Test]
        public void SerializedAreaEffectWithoutTheRadius_ReadsZero()
        {
            asset = ScriptableObject.CreateInstance<RecipeAsset>();
            asset.OnHit = new IModifier[] { new AreaEffect(3f, 2.75f) };
            string json = EditorJsonUtility.ToJson(asset);
            var field = new Regex("\"radius\":2\\.75,?");
            Assert.IsTrue(field.IsMatch(json), json);
            string old = field.Replace(json, string.Empty);
            Assert.IsFalse(old.Contains("2.75"), old);

            var loaded = ScriptableObject.CreateInstance<RecipeAsset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(old, loaded);
                var area = (AreaEffect)loaded.OnHit[0];
                Assert.AreEqual(3f, area.Damage);
                Assert.AreEqual(0f, area.Radius);
            }
            finally
            {
                Object.DestroyImmediate(loaded);
            }
        }

        static void HitOnce(
            Recipe recipe,
            System.Action<ScriptedHitQuery> addTargets,
            TargetId contact,
            out List<AreaBurstInfo> bursts,
            out List<HitInfo> areaHits)
        {
            var burstList = new List<AreaBurstInfo>();
            var areaList = new List<HitInfo>();
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AreaBurst += burstList.Add;
                world.Hit += info =>
                {
                    if (info.Source == HitSource.Area)
                        areaList.Add(info);
                };
                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                hits.Queue(HitKind.Target, Contact, Vector3.back, contact);
                addTargets(hits);
                world.Tick(0.1f);
            }

            bursts = burstList;
            areaHits = areaList;
        }

        static List<TargetId> TargetsWithDamage(List<HitInfo> areaHits, float damage)
        {
            var targets = new List<TargetId>();
            foreach (HitInfo hit in areaHits)
            {
                if (Mathf.Approximately(hit.Damage, damage))
                    targets.Add(hit.TargetId);
            }

            return targets;
        }
    }
}
