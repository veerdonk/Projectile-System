using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Live poses Homing can lock onto. Games publish them through
    /// <see cref="IProjectileWorld.SetHomingCandidate"/>; acquisition and steering only read ids and positions.
    /// </summary>
    public sealed class HomingCatalog
    {
        readonly List<(TargetId id, Vector3 position)> poses = new List<(TargetId id, Vector3 position)>();

        public int Count => poses.Count;

        public void Set(TargetId id, Vector3 position)
        {
            for (int i = 0; i < poses.Count; i++)
            {
                if (!poses[i].id.Equals(id))
                    continue;

                poses[i] = (id, position);
                return;
            }

            poses.Add((id, position));
        }

        public void Remove(TargetId id)
        {
            for (int i = poses.Count - 1; i >= 0; i--)
            {
                if (poses[i].id.Equals(id))
                    poses.RemoveAt(i);
            }
        }

        public bool TryGet(int index, out TargetId id, out Vector3 position)
        {
            if (index < 0 || index >= poses.Count)
            {
                id = default;
                position = default;
                return false;
            }

            (TargetId poseId, Vector3 posePosition) = poses[index];
            id = poseId;
            position = posePosition;
            return true;
        }

        public bool TryGetPosition(in HomingTarget target, out Vector3 position)
        {
            if (!target.IsAssigned)
            {
                position = default;
                return false;
            }

            for (int i = 0; i < poses.Count; i++)
            {
                if (!poses[i].id.Equals(target.Id))
                    continue;

                position = poses[i].position;
                return true;
            }

            position = default;
            return false;
        }
    }

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

            HomingCatalog catalog = context.HomingCatalog;
            TargetId bestId = default;
            float bestSqrDistance = float.PositiveInfinity;
            bool found = false;
            int count = catalog.Count;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGet(i, out TargetId id, out Vector3 position))
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
