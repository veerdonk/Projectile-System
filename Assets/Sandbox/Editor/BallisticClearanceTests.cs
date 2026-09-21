using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using NUnit.Framework;
using UnityEngine;

namespace Sandbox.Editor.Tests
{
    public sealed class BallisticClearanceTests
    {
        static readonly Vector3 Origin = new Vector3(0f, 0.7f, 2f);
        static readonly Vector3 BounceGround = new Vector3(4f, 0f, 12f);
        const float Speed = 40f;
        const float Gravity = 80f;

        [Test]
        public void TrySolve_ClearPath_UsesFlatterRoot()
        {
            Assert.IsTrue(BallisticAim.TrySolve(Origin, BounceGround, Speed, Gravity, out Vector3 low, highArc: false));
            Assert.IsTrue(BallisticClearance.TrySolve(
                Origin,
                BounceGround,
                Speed,
                Gravity,
                _ => BallisticClearance.PathOutcome.Clear,
                out Vector3 chosen));
            Assert.Less((chosen - low).sqrMagnitude, 1e-6f);
        }

        [Test]
        public void TrySolve_BlockedFlatterPath_UsesMinimumPitchNotHighRoot()
        {
            Assert.IsTrue(BallisticAim.TrySolve(Origin, BounceGround, Speed, Gravity, out Vector3 low, highArc: false));
            Assert.IsTrue(BallisticAim.TrySolve(Origin, BounceGround, Speed, Gravity, out Vector3 high, highArc: true));
            Assert.Greater(high.y, low.y);

            float lowPitch = BallisticClearance.Pitch(low);
            float highPitch = BallisticClearance.Pitch(high);
            float clearPitch = Mathf.Lerp(lowPitch, highPitch, 0.12f);

            Assert.IsTrue(BallisticClearance.TrySolve(
                Origin,
                BounceGround,
                Speed,
                Gravity,
                dir => BallisticClearance.Pitch(dir) + 1e-4f < clearPitch
                    ? BallisticClearance.PathOutcome.Blocked
                    : BallisticClearance.PathOutcome.Clear,
                out Vector3 chosen));

            float chosenPitch = BallisticClearance.Pitch(chosen);
            Assert.Greater(chosenPitch, lowPitch);
            Assert.Less(chosenPitch, Mathf.Lerp(lowPitch, highPitch, 0.4f));
            Assert.Less(Mathf.Abs(chosenPitch - clearPitch), 0.08f);
        }

        [Test]
        public void TrySolve_Unreachable_ReturnsFalse()
        {
            Vector3 far = new Vector3(0f, 0f, 80f);
            Assert.IsFalse(BallisticClearance.TrySolve(
                Origin,
                far,
                Speed,
                Gravity,
                _ => BallisticClearance.PathOutcome.Clear,
                out Vector3 direction));
            Assert.AreEqual(Vector3.zero, direction);
        }
    }
}
