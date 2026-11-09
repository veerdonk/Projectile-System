using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Live poses Homing can lock onto. Games publish them through
    /// <see cref="IProjectileWorld.SetHomingCandidate"/>; acquisition and steering only read ids and positions.
    /// </summary>
    public sealed class HomingCatalog
    {
        readonly List<(TargetId id, Vector3 position)> poses = new List<(TargetId id, Vector3 position)>();
        readonly Dictionary<TargetId, int> indexById = new Dictionary<TargetId, int>();
        bool hasDeadSlots;

        /// <summary>Number of published poses.</summary>
        public int Count => indexById.Count;

        /// <summary>Publishes or moves a candidate pose.</summary>
        public void Set(TargetId id, Vector3 position)
        {
            if (indexById.TryGetValue(id, out int index))
            {
                poses[index] = (id, position);
                return;
            }

            indexById.Add(id, poses.Count);
            poses.Add((id, position));
        }

        /// <summary>Drops a previously published pose.</summary>
        public void Remove(TargetId id)
        {
            if (indexById.Remove(id))
                hasDeadSlots = true;
        }

        /// <summary>Indexed read for acquisition. False when <paramref name="index"/> is out of range.</summary>
        public bool TryGet(int index, out TargetId id, out Vector3 position)
        {
            Compact();
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

        /// <summary>Looks up the published pose for an assigned lock.</summary>
        public bool TryGetPosition(in HomingTarget target, out Vector3 position)
        {
            if (!target.IsAssigned)
            {
                position = default;
                return false;
            }

            if (!indexById.TryGetValue(target.Id, out int index))
            {
                position = default;
                return false;
            }

            position = poses[index].position;
            return true;
        }

        void Compact()
        {
            if (!hasDeadSlots)
                return;

            int write = 0;
            for (int read = 0; read < poses.Count; read++)
            {
                (TargetId id, Vector3 position) pose = poses[read];
                // A slot is live only while the index points at it; a removed and re-set id owns a later slot.
                if (!indexById.TryGetValue(pose.id, out int index) || index != read)
                    continue;

                if (write != read)
                {
                    poses[write] = pose;
                    indexById[pose.id] = write;
                }

                write++;
            }

            poses.RemoveRange(write, poses.Count - write);
            hasDeadSlots = false;
        }
    }

    /// <summary>
    /// Picks a homing target for an instance that did not receive one on <see cref="SpawnRequest"/>.
    /// </summary>
    public interface IHomingAcquisition
    {
        /// <summary>
        /// Returns true and a target when this instance should lock. Returning false leaves the instance
        /// unassigned so a later tick can try again. Homing calls this whenever the lock is empty, including after
        /// a Target hit and after the locked target stops being published, except the first lock when <see cref="SpawnRequest.HomingAcquirePointSet"/> is true
        /// (that pick is <see cref="ClosestToPointAcquisition"/>). Skip <see cref="ModifierContext.HomingIgnoredTarget"/>
        /// while it is set, and always skip <see cref="ModifierContext.ShooterTarget"/> (Homing drops a pick of it).
        /// The cone (<see cref="Homing.AcquireHalfAngleDegrees"/>) and the last hit target's fallback rule are applied by
        /// <see cref="ClosestInTravelDirectionAcquisition"/>; a custom acquisition sees a passed hit target as an ordinary candidate.
        /// </summary>
        bool TryAcquire(ModifierContext context, out HomingTarget target);
    }

    /// <summary>
    /// Locks the nearest registered candidate whose offset from the instance is less than the calling
    /// <see cref="Homing.AcquireHalfAngleDegrees"/> off travel (90: a positive dot with travel; 180: every candidate).
    /// The instance's last hit target, once it is behind travel, is locked only when it is the only candidate in that cone.
    /// Later strategies (player view, random) are other <see cref="IHomingAcquisition"/> implementations.
    /// </summary>
    public sealed class ClosestInTravelDirectionAcquisition : IHomingAcquisition
    {
        /// <inheritdoc />
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

            var cone = new Cone(context.HomingAcquireHalfAngleDegrees, travel);
            var best = new Candidate();
            bool fallbackInCone = false;
            HitTargetCatalog hitTargets = context.HitTargets;
            if (hitTargets != null)
            {
                int hitCount = hitTargets.Count;
                for (int i = 0; i < hitCount; i++)
                {
                    if (!hitTargets.TryGet(i, out TargetId id, out Vector3 position))
                        continue;
                    Consider(context, id, position, cone, ref best, ref fallbackInCone);
                }
            }

            HomingCatalog catalog = context.HomingCatalog;
            int count = catalog.Count;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGet(i, out TargetId id, out Vector3 position))
                    continue;
                if (hitTargets != null && hitTargets.Contains(id))
                    continue;
                Consider(context, id, position, cone, ref best, ref fallbackInCone);
            }

            if (best.Found)
            {
                target = HomingTarget.Assigned(best.Id);
                return true;
            }

            if (fallbackInCone)
            {
                target = context.HomingFallbackTarget;
                return true;
            }

            target = default;
            return false;
        }

        static void Consider(
            ModifierContext context,
            TargetId id,
            Vector3 position,
            in Cone cone,
            ref Candidate best,
            ref bool fallbackInCone)
        {
            if (context.HomingIgnoredTarget.IsAssigned && id.Equals(context.HomingIgnoredTarget.Id))
                return;
            if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                return;

            Vector3 toTarget = position - context.Position;
            if (!cone.Contains(toTarget))
                return;

            if (context.HomingFallbackTarget.IsAssigned && id.Equals(context.HomingFallbackTarget.Id))
            {
                fallbackInCone = true;
                return;
            }

            float sqrDistance = toTarget.sqrMagnitude;
            if (best.Found && sqrDistance >= best.SqrDistance)
                return;

            best.SqrDistance = sqrDistance;
            best.Id = id;
            best.Found = true;
        }

        readonly struct Cone
        {
            readonly Vector3 travel;
            readonly float cos;
            readonly bool all;
            readonly bool none;

            public Cone(float halfAngleDegrees, Vector3 travel)
            {
                this.travel = travel;
                float half = Mathf.Clamp(halfAngleDegrees, 0f, 180f);
                all = half >= 180f;
                none = half <= 0f;
                // 90° must be exactly dot > 0; Mathf.Cos(PI / 2) is a few 1e-8 off zero.
                cos = Mathf.Approximately(half, 90f) ? 0f : Mathf.Cos(half * Mathf.Deg2Rad);
            }

            public bool Contains(Vector3 toTarget)
            {
                if (all)
                    return true;
                if (none)
                    return false;

                float dot = Vector3.Dot(toTarget, travel);
                if (cos == 0f)
                    return dot > 0f;
                return dot > cos * toTarget.magnitude * travel.magnitude;
            }
        }

        struct Candidate
        {
            public TargetId Id;
            public float SqrDistance;
            public bool Found;
        }
    }

    /// <summary>
    /// Locks the catalog candidate closest to <see cref="ModifierContext.HomingAcquirePoint"/>.
    /// Distance is 3D squared magnitude. There is no travel-direction filter. Ties keep the first candidate
    /// considered (hit targets, then homing poses). The same id in both catalogs is one candidate, and the
    /// hit-target homing point wins.
    /// </summary>
    public sealed class ClosestToPointAcquisition : IHomingAcquisition
    {
        /// <inheritdoc />
        public bool TryAcquire(ModifierContext context, out HomingTarget target)
        {
            if (context == null || !context.HomingAcquirePointSet)
            {
                target = default;
                return false;
            }

            Vector3 point = context.HomingAcquirePoint;
            var best = new Candidate();
            HitTargetCatalog hitTargets = context.HitTargets;
            if (hitTargets != null)
            {
                int hitCount = hitTargets.Count;
                for (int i = 0; i < hitCount; i++)
                {
                    if (!hitTargets.TryGet(i, out TargetId id, out Vector3 position))
                        continue;
                    Consider(context, id, position, point, ref best);
                }
            }

            HomingCatalog catalog = context.HomingCatalog;
            int count = catalog.Count;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGet(i, out TargetId id, out Vector3 position))
                    continue;
                if (hitTargets != null && hitTargets.Contains(id))
                    continue;
                Consider(context, id, position, point, ref best);
            }

            if (!best.Found)
            {
                target = default;
                return false;
            }

            target = HomingTarget.Assigned(best.Id);
            return true;
        }

        static void Consider(
            ModifierContext context,
            TargetId id,
            Vector3 position,
            Vector3 point,
            ref Candidate best)
        {
            if (context.HomingIgnoredTarget.IsAssigned && id.Equals(context.HomingIgnoredTarget.Id))
                return;
            if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                return;

            float sqrDistance = (position - point).sqrMagnitude;
            if (best.Found && sqrDistance >= best.SqrDistance)
                return;

            best.SqrDistance = sqrDistance;
            best.Id = id;
            best.Found = true;
        }

        struct Candidate
        {
            public TargetId Id;
            public float SqrDistance;
            public bool Found;
        }
    }
}
