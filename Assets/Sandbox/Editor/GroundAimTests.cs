using NUnit.Framework;
using UnityEngine;

namespace Sandbox.Editor.Tests
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
    }
}
