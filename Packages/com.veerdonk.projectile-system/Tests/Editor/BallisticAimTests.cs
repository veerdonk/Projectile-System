using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class BallisticAimTests
    {
        static readonly Vector3 SpawnPosition = new Vector3(0f, 1f, 0f);
        static readonly Vector3 DummyTargetPosition = new Vector3(0f, 1f, 8f);
        const float AimedSpeed = 32f;
        const float AimedGravity = 80f;
        const float MeetTolerance = 0.5f;
        const float SimDt = 0.01f;

        [Test]
        public void TrySolve_ReachableSandboxDummy_PathMeetsTarget()
        {
            Assert.IsTrue(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                AimedSpeed,
                AimedGravity,
                out Vector3 direction));

            Assert.Greater(direction.y, 0f);
            AssertPathMeetsTarget(direction, AimedSpeed, AimedGravity, DummyTargetPosition);
        }

        [Test]
        public void TrySolve_MinSpeedLevelTarget_ElevationNear45()
        {
            float range = DummyTargetPosition.z - SpawnPosition.z;
            float minSpeed = Mathf.Sqrt(AimedGravity * range);
            float speed = FindLowestReachableSpeed(minSpeed * 0.9f, minSpeed * 1.1f);
            Assert.IsTrue(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                speed,
                AimedGravity,
                out Vector3 direction));

            float elevation = Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.AreEqual(45f, elevation, 5f);
        }

        [Test]
        public void TrySolve_FastLevelTarget_ElevationNearLineOfSight()
        {
            float range = DummyTargetPosition.z - SpawnPosition.z;
            float minSpeed = Mathf.Sqrt(AimedGravity * range);
            float speed = minSpeed * 4f;
            Assert.IsTrue(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                speed,
                AimedGravity,
                out Vector3 direction));

            float elevation = Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.Less(elevation, 10f);
            Assert.Less(elevation, 22.5f);
            AssertPathMeetsTarget(direction, speed, AimedGravity, DummyTargetPosition);
        }

        [Test]
        public void TrySolve_TooSlow_LevelTarget_ReturnsFalse()
        {
            float range = DummyTargetPosition.z - SpawnPosition.z;
            float minSpeed = Mathf.Sqrt(AimedGravity * range);
            Assert.IsFalse(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                minSpeed * 0.5f,
                AimedGravity,
                out Vector3 direction));
            Assert.AreEqual(Vector3.zero, direction);
        }

        [Test]
        public void TrySolve_TooFar_ReturnsFalse()
        {
            Vector3 far = new Vector3(0f, 1f, 80f);
            Assert.IsFalse(BallisticAim.TrySolve(
                SpawnPosition,
                far,
                AimedSpeed,
                AimedGravity,
                out Vector3 direction));
            Assert.AreEqual(Vector3.zero, direction);
        }

        [Test]
        public void TrySolve_Gravity0_HighTarget_ReturnsFalse()
        {
            Vector3 highTarget = new Vector3(0f, 5f, 8f);
            Assert.IsFalse(BallisticAim.TrySolve(
                SpawnPosition,
                highTarget,
                AimedSpeed,
                0f,
                out Vector3 direction));
            Assert.AreEqual(Vector3.zero, direction);
        }

        [Test]
        public void TrySolve_Gravity0_TargetOnTheRay_ReturnsHorizontalDirection()
        {
            Assert.IsTrue(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                AimedSpeed,
                0f,
                out Vector3 direction));
            Assert.AreEqual(0f, direction.y, 0.0001f);
            Assert.AreEqual(0f, direction.x, 0.0001f);
            Assert.AreEqual(1f, direction.z, 0.0001f);
        }

        [Test]
        public void TrySolve_NonPositiveSpeed_ReturnsFalse()
        {
            Assert.IsFalse(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                0f,
                AimedGravity,
                out _));
            Assert.IsFalse(BallisticAim.TrySolve(
                SpawnPosition,
                DummyTargetPosition,
                -10f,
                AimedGravity,
                out _));
        }

        static void AssertPathMeetsTarget(Vector3 direction, float speed, float gravity, Vector3 target)
        {
            Recipe recipe = SimulationTestHelper.CreateRecipe(
                "ballistic-aim",
                speed: speed,
                lifetime: 5f,
                gravity: gravity);
            (ProjectileWorld world, _) = SimulationTestHelper.CreateWorld(recipe);
            using (world)
            {
                ProjectileId id = world.Spawn(new SpawnRequest
                {
                    Recipe = recipe,
                    Position = SpawnPosition,
                    Direction = direction
                });

                float closest = float.MaxValue;
                Vector3 previous = SpawnPosition;
                for (int i = 0; i < 500; i++)
                {
                    if (!world.TryGetState(id, out _, out _, out _))
                        break;

                    world.Tick(SimDt);
                    if (!world.TryGetState(id, out Vector3 position, out _, out _))
                        break;

                    closest = Mathf.Min(closest, DistanceToSegment(previous, position, target));
                    previous = position;
                    if (closest <= MeetTolerance)
                        break;
                }

                Assert.LessOrEqual(closest, MeetTolerance);
            }
        }

        static float FindLowestReachableSpeed(float lo, float hi)
        {
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (BallisticAim.TrySolve(SpawnPosition, DummyTargetPosition, mid, AimedGravity, out _))
                    hi = mid;
                else
                    lo = mid;
            }

            return hi;
        }

        static float DistanceToSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 1e-12f)
                return (point - a).magnitude;

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return (a + ab * t - point).magnitude;
        }
    }
}
