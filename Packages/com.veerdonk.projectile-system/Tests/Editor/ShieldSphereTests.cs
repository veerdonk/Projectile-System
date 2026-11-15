using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class ShieldSphereTests
    {
        static readonly Vector3 Center = new Vector3(0f, 0f, 10f);
        const float ShieldRadius = 4f;

        [Test]
        public void Break_StraightFromOutside_EndsOnSurface_NoHitNoExpireStage()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "shield-break",
                speed: 20f,
                lifetime: 5f,
                effectRadius: 2f,
                onExpire: new IModifier[] { new AreaEffect() });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int shield = world.AddShieldSphere(Center, ShieldRadius);
                var order = new List<string>();
                var blocks = new List<ShieldBlockInfo>();
                ExpireInfo? expired = null;
                int hits = 0;
                int bursts = 0;
                world.ShieldBlocked += info => { blocks.Add(info); order.Add("block"); };
                world.Expired += info => { expired = info; order.Add("expire"); };
                world.Hit += _ => hits++;
                world.AreaBurst += _ => bursts++;

                ProjectileId id = Spawn(world, recipe, Vector3.zero, Vector3.forward);
                for (int i = 0; i < 10 && world.ActiveCount > 0; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, world.ActiveCount);
                Assert.AreEqual(new[] { "block", "expire" }, order.ToArray());
                Assert.AreEqual(id, blocks[0].InstanceId);
                Assert.AreEqual(shield, blocks[0].ShieldId);
                Assert.AreEqual(HitOutcome.Ended, blocks[0].Outcome);
                AssertNear(new Vector3(0f, 0f, 6f), blocks[0].Position);
                AssertNear(Vector3.back, blocks[0].Normal);
                Assert.AreEqual(ExpireReason.Blocked, expired.Value.Reason);
                AssertNear(new Vector3(0f, 0f, 6f), expired.Value.Position);
                Assert.AreEqual(0, hits);
                Assert.AreEqual(0, bursts);
            }
        }

        [Test]
        public void Break_ArcFromOutside_EndsOnSurface()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-arc", speed: 15f, lifetime: 5f, gravity: 9.81f);
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                ExpireInfo? expired = null;
                world.Expired += info => expired = info;

                Spawn(world, recipe, Vector3.zero, Vector3.forward, aimPoint: Center);
                for (int i = 0; i < 100 && world.ActiveCount > 0; i++)
                    world.Tick(1f / 60f);

                Assert.IsTrue(expired.HasValue);
                Assert.AreEqual(ExpireReason.Blocked, expired.Value.Reason);
                Assert.AreEqual(ShieldRadius, (expired.Value.Position - Center).magnitude, 1e-3f);
            }
        }

        [Test]
        public void SpawnedInside_LeavesFreely()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-out", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                int blocks = 0;
                world.ShieldBlocked += _ => blocks++;

                ProjectileId id = Spawn(world, recipe, Center, Vector3.forward);
                for (int i = 0; i < 10; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, blocks);
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out _, out _));
                AssertNear(Center + Vector3.forward * 20f, position);
            }
        }

        [Test]
        public void OutsideMiss_AndRemovedShield_DoNotBlock()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-removed", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                int shield = world.AddShieldSphere(Center, ShieldRadius);
                int blocks = 0;
                world.ShieldBlocked += _ => blocks++;

                ProjectileId miss = Spawn(world, recipe, new Vector3(5f, 0f, 0f), Vector3.forward);
                world.RemoveShieldSphere(shield);
                ProjectileId through = Spawn(world, recipe, Vector3.zero, Vector3.forward);
                for (int i = 0; i < 10; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(0, blocks);
                Assert.IsTrue(world.TryGetState(miss, out _, out _, out _));
                Assert.IsTrue(world.TryGetState(through, out Vector3 position, out _, out _));
                AssertNear(new Vector3(0f, 0f, 20f), position);
            }
        }

        [Test]
        public void PassThroughInOneTick_IsBlockedAtEntry()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-fast", speed: 200f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, 1f);
                ExpireInfo? expired = null;
                world.Expired += info => expired = info;

                Spawn(world, recipe, Vector3.zero, Vector3.forward);
                world.Tick(0.1f);

                Assert.AreEqual(ExpireReason.Blocked, expired.Value.Reason);
                AssertNear(new Vector3(0f, 0f, 9f), expired.Value.Position);
            }
        }

        [Test]
        public void OverlappingSpheres_EachBlocksOnItsOwn()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-overlap", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Vector3.zero, 4f);
                int second = world.AddShieldSphere(new Vector3(0f, 0f, 6f), 4f);
                var blocks = new List<ShieldBlockInfo>();
                world.ShieldBlocked += info => blocks.Add(info);

                Spawn(world, recipe, Vector3.zero, Vector3.forward);
                for (int i = 0; i < 10 && world.ActiveCount > 0; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, blocks.Count);
                Assert.AreEqual(second, blocks[0].ShieldId);
                AssertNear(new Vector3(0f, 0f, 2f), blocks[0].Position);
            }
        }

        [Test]
        public void DirectContactBeforeShield_Hits_TargetBehindShield_IsNotHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-order", speed: 20f, lifetime: 5f);
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 8);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                world.SetHitTarget(new TargetId(1), new Vector3(0f, 0f, 3f), 0.5f);
                world.SetHitTarget(new TargetId(2), new Vector3(3f, 0f, 10f), 0.5f);
                var hits = new List<HitInfo>();
                int blocks = 0;
                world.Hit += info => hits.Add(info);
                world.ShieldBlocked += _ => blocks++;

                Spawn(world, recipe, Vector3.zero, Vector3.forward);
                Spawn(world, recipe, new Vector3(3f, 0f, 0f), Vector3.forward);
                for (int i = 0; i < 10 && world.ActiveCount > 0; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, hits.Count);
                Assert.AreEqual(new TargetId(1), hits[0].TargetId);
                Assert.AreEqual(1, blocks);
            }
        }

        [Test]
        public void Bounce_ReflectsAboutNormal_IsNotAHit_KeepsBounceCharges()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "shield-bounce",
                speed: 20f,
                lifetime: 5f,
                onHit: new IModifier[] { new Bounce(2) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                var blocks = new List<ShieldBlockInfo>();
                int hits = 0;
                world.ShieldBlocked += info => blocks.Add(info);
                world.Hit += _ => hits++;

                ProjectileId id = Spawn(world, recipe, Vector3.zero, Vector3.forward, ShieldResponse.Bounce);
                for (int i = 0; i < 4; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(1, blocks.Count);
                Assert.AreEqual(HitOutcome.Bounced, blocks[0].Outcome);
                Assert.AreEqual(0, hits);
                Assert.IsTrue(world.TryGetRemaining(id, out _, out int bounce));
                Assert.AreEqual(2, bounce);
                Assert.IsTrue(world.TryGetState(id, out Vector3 position, out Vector3 velocity, out _));
                AssertNear(Vector3.back * 20f, velocity);
                Assert.Less(position.z, 6f);
                Assert.Greater((position - Center).magnitude, ShieldRadius);
            }
        }

        [Test]
        public void Bounce_ArcOffTheTop_NeverEntersAndLandsOutside()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-lob", speed: 2f, lifetime: 6f, gravity: 9.81f);
            recipe.Delivery = Delivery.Arc;
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Vector3.zero, ShieldRadius);
                int blocks = 0;
                world.ShieldBlocked += _ => blocks++;

                ProjectileId id = Spawn(world, recipe, new Vector3(1f, 8f, 0f), Vector3.down, ShieldResponse.Bounce);
                Vector3 position = Vector3.zero;
                for (int i = 0; i < 120; i++)
                {
                    world.Tick(1f / 60f);
                    Assert.IsTrue(world.TryGetState(id, out position, out _, out _));
                    Assert.GreaterOrEqual(position.magnitude, ShieldRadius - 1e-3f);
                }

                Assert.GreaterOrEqual(blocks, 1);
                Assert.Greater(new Vector2(position.x, position.z).magnitude, 1f);
            }
        }

        [Test]
        public void MulticastExtras_InheritTheResponse()
        {
            Recipe extra = SimulationTestHelper.CreateRecipe("shield-extra", speed: 20f, lifetime: 5f);
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "shield-multicast",
                speed: 20f,
                lifetime: 5f,
                onSpawn: new IModifier[] { new Multicast(3, extra, new RandomConePattern(0f)) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe, prewarm: 8);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                var blocks = new List<ShieldBlockInfo>();
                world.ShieldBlocked += info => blocks.Add(info);

                Spawn(world, recipe, Vector3.zero, Vector3.forward, ShieldResponse.Bounce);
                Assert.AreEqual(3, world.ActiveCount);
                for (int i = 0; i < 4; i++)
                    world.Tick(0.1f);

                Assert.AreEqual(3, blocks.Count);
                Assert.AreEqual(3, world.ActiveCount);
                for (int i = 0; i < blocks.Count; i++)
                    Assert.AreEqual(HitOutcome.Bounced, blocks[i].Outcome);
            }
        }

        [Test]
        public void HomingCurvingIntoShield_IsBlocked()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "shield-homing",
                speed: 10f,
                lifetime: 5f,
                onTick: new IModifier[] { new Homing(720f) });
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                world.SetHomingCandidate(new TargetId(5), Center);
                ExpireInfo? expired = null;
                world.Expired += info => expired = info;

                Spawn(world, recipe, new Vector3(-8f, 0f, 10f), new Vector3(0f, 0f, 1f), lockOn: new TargetId(5));
                for (int i = 0; i < 60 && world.ActiveCount > 0; i++)
                    world.Tick(1f / 30f);

                Assert.AreEqual(ExpireReason.Blocked, expired.Value.Reason);
                Assert.AreEqual(ShieldRadius, (expired.Value.Position - Center).magnitude, 1e-3f);
            }
        }

        [Test]
        public void Hitscan_StopsAtShield_TargetBehindIsNotHit()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-hitscan", speed: 20f, lifetime: 5f);
            recipe.Delivery = Delivery.Hitscan;
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                world.SetHitTarget(new TargetId(1), Center + Vector3.forward * 10f, 0.5f);
                var order = new List<string>();
                ExpireInfo? expired = null;
                int hits = 0;
                world.ShieldBlocked += _ => order.Add("block");
                world.Expired += info => { expired = info; order.Add("expire"); };
                world.Hit += _ => hits++;

                Spawn(world, recipe, Vector3.zero, Vector3.forward, ShieldResponse.Bounce);

                Assert.AreEqual(new[] { "block", "expire" }, order.ToArray());
                Assert.AreEqual(0, hits);
                Assert.AreEqual(ExpireReason.Blocked, expired.Value.Reason);
                AssertNear(new Vector3(0f, 0f, 6f), expired.Value.Position);

                order.Clear();
                Spawn(world, recipe, Center, Vector3.forward);
                Assert.AreEqual(1, hits);
                Assert.AreEqual(new[] { "expire" }, order.ToArray());
            }
        }

        [Test]
        public void Beam_PulseStopsAtShield_EveryPulse_StaysLive()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-beam", speed: 20f, lifetime: 5f);
            recipe.Delivery = Delivery.Beam;
            recipe.BeamPulseSeconds = 0.1f;
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                world.AddShieldSphere(Center, ShieldRadius);
                world.SetHitTarget(new TargetId(1), Center, 0.5f);
                int blocks = 0;
                int hits = 0;
                world.ShieldBlocked += _ => blocks++;
                world.Hit += _ => hits++;

                ProjectileId id = Spawn(world, recipe, Vector3.zero, Vector3.forward);
                world.Tick(0.1f);
                world.AimBeam(id, Vector3.zero, Vector3.forward);

                Assert.AreEqual(2, blocks);
                Assert.AreEqual(0, hits);
                Assert.AreEqual(1, world.ActiveCount);
            }
        }

        [Test]
        public void AddShieldSphere_RejectsUnusableInput()
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe("shield-invalid");
            (ProjectileWorld world, ScriptedHitQuery _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                Assert.AreEqual(0, world.AddShieldSphere(Vector3.zero, 0f));
                Assert.AreEqual(0, world.AddShieldSphere(Vector3.zero, -1f));
                Assert.AreEqual(0, world.AddShieldSphere(Vector3.zero, float.NaN));
                Assert.AreEqual(0, world.AddShieldSphere(Vector3.zero, float.PositiveInfinity));
                Assert.AreEqual(0, world.AddShieldSphere(new Vector3(float.NaN, 0f, 0f), 1f));
                int a = world.AddShieldSphere(Vector3.zero, 1f);
                int b = world.AddShieldSphere(Vector3.zero, 1f);
                Assert.Greater(a, 0);
                Assert.AreNotEqual(a, b);
                world.RemoveShieldSphere(12345);
            }
        }

        static ProjectileId Spawn(
            ProjectileWorld world,
            Recipe recipe,
            Vector3 position,
            Vector3 direction,
            ShieldResponse response = ShieldResponse.Break,
            Vector3? aimPoint = null,
            TargetId? lockOn = null)
        {
            return world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = position,
                Direction = direction,
                ShieldResponse = response,
                HomingAcquirePointSet = aimPoint.HasValue,
                HomingAcquirePoint = aimPoint ?? default,
                HomingTarget = lockOn.HasValue ? HomingTarget.Assigned(lockOn.Value) : default
            });
        }

        static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }
    }
}
