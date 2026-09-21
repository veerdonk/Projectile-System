using Veerdonk.ProjectileSystem;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Samples.Editor.Tests
{
    public sealed class GroundAimTests
    {
        [Test]
        public void AimDirection_IsHorizontalTowardGroundPoint()
        {
            var from = new Vector3(0f, 0.7f, 0f);
            var ray = new Ray(new Vector3(0f, 10f, 0f), new Vector3(4f, -10f, 8f));
            Assert.IsTrue(GroundAim.TryDirection(ray, from, out Vector3 direction));
            Assert.AreEqual(0f, direction.y, 1e-5f);
            Vector3 expected = new Vector3(4f, 0f, 8f).normalized;
            Assert.Less((direction - expected).sqrMagnitude, 1e-6f);
        }

        [Test]
        public void AimDirection_FalseWhenRayMissesGround()
        {
            var from = Vector3.zero;
            var ray = new Ray(new Vector3(0f, 2f, 0f), Vector3.up);
            Assert.IsFalse(GroundAim.TryDirection(ray, from, out _));
        }

        [Test]
        public void TryPoint_HitsGroundPlane()
        {
            var ray = new Ray(new Vector3(0f, 10f, 0f), new Vector3(4f, -10f, 8f));
            Assert.IsTrue(GroundAim.TryPoint(ray, out Vector3 point));
            Assert.AreEqual(0f, point.y, 1e-5f);
            Assert.AreEqual(4f, point.x, 1e-4f);
            Assert.AreEqual(8f, point.z, 1e-4f);
        }

        [Test]
        public void TryPoint_FalseWhenRayMissesGround()
        {
            var ray = new Ray(new Vector3(0f, 2f, 0f), Vector3.up);
            Assert.IsFalse(GroundAim.TryPoint(ray, out _));
        }

        [Test]
        public void PitchLook_HasVerticalTowardGroundPoint()
        {
            var from = new Vector3(0f, 0.7f, 2f);
            var ray = new Ray(new Vector3(0f, 10f, 0f), new Vector3(0f, -10f, 8f));
            Assert.IsTrue(GroundAim.TryPoint(ray, out Vector3 point));
            Vector3 look = (point - from).normalized;
            Assert.Less(look.y, 0f);
            Assert.AreNotEqual(0f, look.y);
        }

        [Test]
        public void Flatten_ZerosY()
        {
            Vector3 flat = GroundAim.Flatten(new Vector3(3f, 5f, 4f));
            Assert.AreEqual(0f, flat.y, 1e-5f);
            Vector3 expected = new Vector3(3f, 0f, 4f).normalized;
            Assert.Less((flat - expected).sqrMagnitude, 1e-6f);
        }

        [Test]
        public void TryAimPoint_HitsCubeTop_NotGroundBehind()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.transform.position = new Vector3(0f, 1f, 8f);
                cube.transform.localScale = Vector3.one * 2f;
                Physics.SyncTransforms();

                var ray = new Ray(new Vector3(0f, 14f, -12f), new Vector3(0f, -12f, 20f));
                Assert.IsTrue(GroundAim.TryPoint(ray, out Vector3 ground));
                Assert.AreEqual(0f, ground.y, 1e-4f);
                Assert.Greater(ground.z, 10f);

                Assert.IsTrue(GroundAim.TryAimPoint(ray, out Vector3 point, out Collider collider));
                Assert.AreEqual(cube.GetComponent<Collider>(), collider);
                Assert.Greater(point.y, 1.5f);
                Assert.Less(Mathf.Abs(point.z - 8f), 1.05f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void SpawnPosition_LiftsAboveGround_WhenOnTravelSizeWouldOverlap()
        {
            var recipe = new Recipe
            {
                Radius = 0.25f,
                OnTravel = new IModifier[] { new Size(4f) }
            };
            Vector3 spawn = SampleYard.SpawnPosition(new Vector3(0f, 0.7f, 2f), recipe);
            Assert.GreaterOrEqual(spawn.y, 1.02f);
            Assert.AreEqual(0f, spawn.x);
            Assert.AreEqual(2f, spawn.z);
        }

        [Test]
        public void SpawnPosition_KeepsMuzzleHeight_WhenSizeFits()
        {
            var recipe = new Recipe
            {
                Radius = 0.25f,
                OnTravel = new IModifier[] { new Size(2f) }
            };
            Vector3 muzzle = new Vector3(0f, 0.7f, 2f);
            Vector3 spawn = SampleYard.SpawnPosition(muzzle, recipe);
            Assert.AreEqual(muzzle.y, spawn.y);
        }
    }
}
