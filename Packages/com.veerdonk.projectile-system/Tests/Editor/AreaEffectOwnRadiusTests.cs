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
            }
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
