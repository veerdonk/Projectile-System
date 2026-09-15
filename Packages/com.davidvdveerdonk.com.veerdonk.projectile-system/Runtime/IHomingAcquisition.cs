using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Picks a homing target for an instance that did not receive one on <see cref="SpawnRequest"/>.
    /// </summary>
    public interface IHomingAcquisition
    {
        /// <summary>
        /// Returns true and a target when this instance should lock. Returning false leaves the instance
        /// unassigned so a later tick can try again. Once a lock is stored, <see cref="Homing"/> does not call this.
        /// </summary>
        bool TryAcquire(ModifierContext context, out HomingTarget target);
    }

    /// <summary>
    /// Locks the nearest registered candidate whose offset from the instance has a positive dot with travel.
    /// Later strategies (player view, random) are other <see cref="IHomingAcquisition"/> implementations.
    /// </summary>
    public sealed class ClosestInTravelDirectionAcquisition : IHomingAcquisition
    {
        public bool TryAcquire(ModifierContext context, out HomingTarget target)
        {
            if (context == null)
            {
                target = default;
                return false;
            }

            Vector3 travel = context.Velocity;
            if (travel.sqrMagnitude < 1e-12f)
            {
                target = default;
                return false;
            }

            TargetId bestId = default;
            float bestSqrDistance = float.PositiveInfinity;
            bool found = false;
            int count = context.HomingCandidateCount;
            for (int i = 0; i < count; i++)
            {
                if (!context.TryGetHomingCandidate(i, out TargetId id, out Vector3 position))
                    continue;

                Vector3 toTarget = position - context.Position;
                if (Vector3.Dot(toTarget, travel) <= 0f)
                    continue;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                bestId = id;
                found = true;
            }

            if (!found)
            {
                target = default;
                return false;
            }

            target = HomingTarget.Assigned(bestId);
            return true;
        }
    }
}
