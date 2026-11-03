using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class AreaEffectOnExpireTests
    {
        [Test]
        public void Append_OnExpire_IsAllowed()
        {
            var recipe = new Recipe { Id = "append-expire" };

            recipe.Append(RecipeStage.OnExpire, new AreaEffect(2f));

            Assert.AreEqual(1, recipe.OnExpire.Length);
        }

        [Test]
        public void Lifetime_HitsTargetInsideRadius_AtExpirePosition_WithOneBurst()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-lifetime",
                speed: 10f,
                lifetime: 1f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var inside = new TargetId(1);
                var outside = new TargetId(2);
                var order = new List<string>();
                var areaHits = new List<HitInfo>();
                var bursts = new List<AreaBurstInfo>();
                ExpireInfo expired = default;
                world.AreaBurst += info =>
                {
                    order.Add("burst");
                    bursts.Add(info);
                };
                world.Hit += info =>
                {
                    order.Add("hit:" + info.Source);
                    areaHits.Add(info);
                };
                world.Expired += info =>
                {
                    order.Add("expired");
                    expired = info;
                };
                hits.AddOverlapTarget(inside, new Vector3(1f, 0f, 5f));
                hits.AddOverlapTarget(outside, new Vector3(0f, 0f, 8f));

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);
                Assert.AreEqual(0, bursts.Count);

                world.Tick(0.5f);

                Assert.AreEqual(0, world.ActiveCount);
                CollectionAssert.AreEqual(new[] { "burst", "hit:Area", "expired" }, order);
                Assert.AreEqual(ExpireReason.Lifetime, expired.Reason);
                Assert.AreEqual(id, bursts[0].InstanceId);
                Assert.AreEqual(RecipeStage.OnExpire, bursts[0].Stage);
                Assert.AreEqual(new Vector3(0f, 0f, 5f), expired.Position);
                Assert.AreEqual(expired.Position, bursts[0].Center);
                Assert.AreEqual(2f, bursts[0].Radius, 1e-4f);
                Assert.IsFalse(bursts[0].Occupancy);
                Assert.AreEqual(inside, areaHits[0].TargetId);
                Assert.AreEqual(HitKind.Target, areaHits[0].Kind);
                Assert.AreEqual(4f, areaHits[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void Lifetime_RaisesBurst_WithNoTargetInside()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-empty",
                speed: 10f,
                lifetime: 0.5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int bursts = 0;
                int seen = 0;
                world.AreaBurst += _ => bursts++;
                world.Hit += _ => seen++;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.6f);

                Assert.AreEqual(1, bursts);
                Assert.AreEqual(0, seen);
            }
        }

        [Test]
        public void HitEnded_RaisesNoBurstOrAreaHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-hit",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onExpire: new IModifier[] { new AreaEffect(4f, includeDirectHit: false) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int bursts = 0;
                var seen = new List<HitInfo>();
                ExpireInfo expired = default;
                world.AreaBurst += _ => bursts++;
                world.Hit += seen.Add;
                world.Expired += info => expired = info;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 2f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1));
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(ExpireReason.Hit, expired.Reason);
                Assert.AreEqual(0, bursts);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Direct, seen[0].Source);
            }
        }

        [Test]
        public void WorldHitEnded_RaisesNoBurst()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-world",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int bursts = 0;
                int areaHits = 0;
                world.AreaBurst += _ => bursts++;
                world.Hit += info =>
                {
                    if (info.Source == HitSource.Area)
                        areaHits++;
                };
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 2f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.World, new Vector3(0f, 0f, 2f), Vector3.up);
                world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(0, bursts);
                Assert.AreEqual(0, areaHits);
            }
        }

        [Test]
        public void StrongProtection_SkipsTheBurst()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-strong",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 3f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int bursts = 0;
                ExpireInfo expired = default;
                world.AreaBurst += _ => bursts++;
                world.Expired += info => expired = info;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 2f), Vector3.back, new TargetId(1), ProtectionLevel.Strong);
                world.Tick(0.1f);

                Assert.AreEqual(ExpireReason.Absorbed, expired.Reason);
                Assert.AreEqual(0, bursts);
            }
        }

        [Test]
        public void PiercedThenLifetime_StillBursts()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-pierce",
                speed: 10f,
                lifetime: 1f,
                effectRadius: 2f,
                onHit: new IModifier[] { new Pierce(1) },
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                ExpireInfo expired = default;
                world.AreaBurst += bursts.Add;
                world.Expired += info => expired = info;

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                hits.Queue(HitKind.Target, new Vector3(0f, 0f, 1f), Vector3.back, new TargetId(1));
                world.Tick(0.2f);
                Assert.AreEqual(1, world.ActiveCount);
                Assert.AreEqual(0, bursts.Count);

                world.Tick(1f);

                Assert.AreEqual(ExpireReason.Lifetime, expired.Reason);
                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(RecipeStage.OnExpire, bursts[0].Stage);
            }
        }

        [Test]
        public void Status_IsRaisedPerTargetInside()
        {
            var status = new DummyStatusEffect();
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-status",
                speed: 10f,
                lifetime: 0.5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f, status) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var applied = new List<StatusInfo>();
                world.StatusApplied += applied.Add;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 1f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);

                Assert.AreEqual(1, applied.Count);
                Assert.AreEqual(new TargetId(1), applied[0].TargetId);
                Assert.AreSame(status, applied[0].Effect);
            }
        }

        [Test]
        public void Occupancy_IsIgnored_OnExpire()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-occupancy",
                speed: 10f,
                lifetime: 0.5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f, occupancy: true) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                var seen = new List<HitInfo>();
                world.AreaBurst += bursts.Add;
                world.Hit += seen.Add;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 1f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);

                Assert.AreEqual(1, bursts.Count);
                Assert.IsFalse(bursts[0].Occupancy);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
                Assert.AreEqual(4f, seen[0].Damage, 1e-4f);
            }
        }

        [Test]
        public void HitscanEnd_BurstsAtTheExpirePosition()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-hitscan",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            recipe.Delivery = Delivery.Hitscan;
            recipe.MaxRange = 6f;
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                var seen = new List<HitInfo>();
                ExpireInfo expired = default;
                world.AreaBurst += bursts.Add;
                world.Hit += seen.Add;
                world.Expired += info => expired = info;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 1f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });

                Assert.AreEqual(ExpireReason.HitscanEnd, expired.Reason);
                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(RecipeStage.OnExpire, bursts[0].Stage);
                Assert.AreEqual(expired.Position, bursts[0].Center);
                Assert.AreEqual(1, seen.Count);
                Assert.AreEqual(HitSource.Area, seen[0].Source);
            }
        }

        [Test]
        public void Despawn_BurstsAtTheInstancePosition()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-despawn",
                speed: 10f,
                lifetime: 5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                var bursts = new List<AreaBurstInfo>();
                ExpireInfo expired = default;
                world.AreaBurst += bursts.Add;
                world.Expired += info => expired = info;

                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.2f);
                world.Despawn(id);

                Assert.AreEqual(1, bursts.Count);
                Assert.AreEqual(expired.Position, bursts[0].Center);
            }
        }

        [Test]
        public void HitHandlerDespawningTheShot_DuringItsExpireBurst_ExpiresItOnce()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "area-expire-reentrant",
                speed: 10f,
                lifetime: 0.5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect(4f) });
            (ProjectileWorld world, ScriptedHitQuery hits) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int expired = 0;
                world.Hit += info => world.Despawn(info.InstanceId);
                world.Expired += _ => expired++;
                hits.AddOverlapTarget(new TargetId(1), new Vector3(0f, 0f, 1f));

                world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = Vector3.zero,
                    Direction = Vector3.forward
                });
                world.Tick(0.5f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(1, expired);
            }
        }
    }
}
