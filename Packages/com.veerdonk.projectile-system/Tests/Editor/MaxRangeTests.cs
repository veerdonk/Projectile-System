using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class MaxRangeTests
    {
        [Test]
        public void Beam_MaxRange30_HitsAt25_NotAt40()
        {
            Recipe recipe = Recipe("beam-range", Delivery.Beam, maxRange: 30f);
            Assert.AreEqual(new TargetId(1), FirstHit(recipe, 25f));
            Assert.IsNull(FirstHit(recipe, 40f));
        }

        [Test]
        public void Hitscan_MaxRange30_HitsAt25_NotAt40()
        {
            Recipe recipe = Recipe("hitscan-range", Delivery.Hitscan, maxRange: 30f);
            Assert.AreEqual(new TargetId(1), FirstHit(recipe, 25f));
            Assert.IsNull(FirstHit(recipe, 40f));
        }

        [Test]
        public void Hitscan_ReachesTheCapWithoutAHit_ExpiresAsHitscanEnd()
        {
            Recipe recipe = Recipe("hitscan-range-end", Delivery.Hitscan, maxRange: 30f);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var expires = new List<ExpireInfo>();
                world.Expired += info => expires.Add(info);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 40f), 0.5f);
                Fire(world, recipe);

                Assert.AreEqual(1, expires.Count);
                Assert.AreEqual(ExpireReason.HitscanEnd, expires[0].Reason);
            }
        }

        [Test]
        public void MaxRangeZero_KeepsTheTenKilometreDefault()
        {
            Assert.AreEqual(new TargetId(1), FirstHit(Recipe("beam-default", Delivery.Beam), 40f));
            Assert.AreEqual(new TargetId(1), FirstHit(Recipe("hitscan-default", Delivery.Hitscan), 40f));
        }

        [Test]
        public void Beam_MaxRange30_Pierce1_OneBudget_NotResetPerSegment()
        {
            Recipe recipe = Recipe(
                "beam-range-pierce",
                Delivery.Beam,
                maxRange: 30f,
                onHit: new IModifier[] { new Pierce(1) });
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 20f), 0.5f);
                world.SetHitTarget(new TargetId(2), new Vector3(0f, 0f, 35f), 0.5f);
                Fire(world, recipe);

                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(new TargetId(1), seen[0].TargetId);
            }
        }

        [TestCase(30f)]
        [TestCase(60f)]
        public void Beam_View_EndsAtTheCap_WhenNothingIsHit(float maxRange)
        {
            var parent = new GameObject("beam-range-view");
            try
            {
                Recipe recipe = Recipe("beam-range-view-" + maxRange, Delivery.Beam, maxRange: maxRange);
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    Fire(world, recipe);
                    Assert.AreEqual(maxRange, FarthestZ(parent.transform), 0.05f);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Beam_View_UncappedBeam_StillDraws40Metres()
        {
            var parent = new GameObject("beam-uncapped-view");
            try
            {
                Recipe recipe = Recipe("beam-uncapped-view", Delivery.Beam);
                var world = new ProjectileWorld(parent.transform);
                using (world)
                {
                    Fire(world, recipe);
                    Assert.AreEqual(40f, FarthestZ(parent.transform), 0.05f);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        static float FarthestZ(Transform root)
        {
            float farthest = 0f;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform segment = transforms[i];
                if (segment.name != "BeamSegment" || !segment.gameObject.activeInHierarchy)
                    continue;

                Vector3 half = segment.up * segment.lossyScale.y;
                farthest = Mathf.Max(farthest, (segment.position + half).z, (segment.position - half).z);
            }

            return farthest;
        }

        [Test]
        public void Straight_IgnoresMaxRange()
        {
            Recipe recipe = Recipe("straight-range", Delivery.Straight, maxRange: 1f);
            recipe.LifetimeSeconds = 10f;
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 5f), 0.5f);
                Fire(world, recipe);
                for (int i = 0; i < 10 && seen.Count == 0; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, seen.Count);
            }
        }

        [Test]
        public void RecipeAsset_CopiesMaxRange_AndCloneKeepsIt()
        {
            var asset = ScriptableObject.CreateInstance<RecipeAsset>();
            try
            {
                asset.Id = "asset-range";
                asset.MaxRange = 12.5f;
                Recipe recipe = asset.ToRecipe();
                Assert.AreEqual(12.5f, recipe.MaxRange);
                Recipe clone = recipe.Clone();
                clone.MaxRange = 3f;
                Assert.AreEqual(12.5f, recipe.MaxRange);
                Assert.AreEqual(3f, clone.MaxRange);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        static TargetId? FirstHit(Recipe recipe, float distance)
        {
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var seen = new List<HitInfo>();
                world.Hit += info => seen.Add(info);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, distance), 0.5f);
                Fire(world, recipe);
                return seen.Count > 0 ? seen[0].TargetId : (TargetId?)null;
            }
        }

        static ProjectileId Fire(ProjectileWorld world, Recipe recipe)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = Vector3.zero,
                Direction = Vector3.forward
            });
        }

        static Recipe Recipe(string id, Delivery delivery, float maxRange = 0f, IModifier[] onHit = null)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(id, speed: 20f, lifetime: 5f, radius: 0.25f, onHit: onHit);
            recipe.Delivery = delivery;
            recipe.BeamPulseSeconds = 0.1f;
            recipe.MaxRange = maxRange;
            return recipe;
        }
    }
}
