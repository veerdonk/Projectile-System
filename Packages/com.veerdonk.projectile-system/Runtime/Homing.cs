using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// OnTick: steer toward a homing target. <see cref="TurnRateDegreesPerSecond"/> is the maximum turn in
    /// degrees per second — Homing(100) turns at 100°/s, Homing(10) at 10°/s. If spawn did not assign a target,
    /// the first lock is <see cref="ClosestToPointAcquisition"/> when <see cref="SpawnRequest.HomingAcquirePointSet"/>
    /// is true, and <see cref="Acquisition"/> otherwise. After that first lock, empty locks use <see cref="Acquisition"/>.
    /// Candidates are <see cref="IProjectileWorld.SetHomingCandidate"/> poses and published hit-target positions.
    /// The same <see cref="TargetId"/> in both catalogs is one candidate, and the hit-target position wins.
    /// A Target hit drops the lock and ignores that TargetId while it stays in front of travel, so a pierce shot
    /// does not re-lock the dummy it is still overlapping. Other candidates may lock on a later tick.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTick)]
    public sealed class Homing : IModifier, IDeserializationCallback
    {
        static readonly ClosestToPointAcquisition AimPoint = new ClosestToPointAcquisition();

        [SerializeField] float turnRateDegreesPerSecond;
        IHomingAcquisition acquisition;

        public Homing()
        {
            acquisition = new ClosestInTravelDirectionAcquisition();
        }

        public Homing(float turnRateDegreesPerSecond, IHomingAcquisition acquisition = null)
        {
            this.turnRateDegreesPerSecond = turnRateDegreesPerSecond;
            this.acquisition = acquisition ?? new ClosestInTravelDirectionAcquisition();
        }

        /// <summary>Maximum turn in degrees per second.</summary>
        public float TurnRateDegreesPerSecond
        {
            get => turnRateDegreesPerSecond;
            set => turnRateDegreesPerSecond = value;
        }

        /// <summary>
        /// Picks a lock when spawn did not assign one, and every empty lock after the first.
        /// When <see cref="SpawnRequest.HomingAcquirePointSet"/> is true, the first lock uses
        /// <see cref="ClosestToPointAcquisition"/> instead. Default is closest-in-travel-direction.
        /// </summary>
        public IHomingAcquisition Acquisition => acquisition;

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (acquisition == null)
                acquisition = new ClosestInTravelDirectionAcquisition();
        }

        internal static void ReleaseLockOnTargetHit(ref Projectile projectile, in HitInfo hit)
        {
            if (hit.Kind != HitKind.Target)
                return;

            projectile.HomingHasTarget = false;
            projectile.HomingTargetId = default;
            projectile.HomingHasIgnoredTarget = true;
            projectile.HomingIgnoredTargetId = hit.TargetId;
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            DropIgnoredIfPassed(context);

            HomingTarget target = context.HomingTarget;
            if (target.IsAssigned && SameTarget(target, context.HomingIgnoredTarget))
            {
                target = default;
                context.HomingTarget = default;
            }

            if (!target.IsAssigned)
            {
                if (!TryAcquireLock(context, out target))
                    return;
                context.HomingTarget = target;
            }
            else if (context.HomingAcquirePointSet)
                context.HomingAimPointUsed = true;

            if (!context.TryGetSteeringPosition(target, out Vector3 targetPosition))
                return;

            Vector3 toTarget = targetPosition - context.Position;
            if (toTarget.sqrMagnitude < 1e-12f)
                return;

            Vector3 current = context.Velocity;
            float speed = current.magnitude;
            if (speed < 1e-6f)
            {
                speed = context.Speed;
                if (speed < 1e-6f)
                    return;
                current = toTarget.normalized * speed;
            }

            Vector3 currentDir = current / speed;
            float maxRadians = TurnRateDegreesPerSecond * context.DeltaTime * Mathf.Deg2Rad;
            if (maxRadians < 0f)
                maxRadians = 0f;

            Vector3 newDir = Vector3.RotateTowards(currentDir, toTarget.normalized, maxRadians, 0f);
            context.Velocity = newDir * speed;
        }

        bool TryAcquireLock(ModifierContext context, out HomingTarget target)
        {
            if (context.HomingAcquirePointSet && !context.HomingAimPointUsed)
            {
                if (!AimPoint.TryAcquire(context, out target))
                    return false;

                context.HomingAimPointUsed = true;
                return true;
            }

            return Acquisition.TryAcquire(context, out target);
        }

        static void DropIgnoredIfPassed(ModifierContext context)
        {
            HomingTarget ignored = context.HomingIgnoredTarget;
            if (!ignored.IsAssigned)
                return;

            if (!context.TryGetSteeringPosition(ignored, out Vector3 position))
            {
                context.HomingIgnoredTarget = default;
                return;
            }

            Vector3 travel = context.Velocity;
            if (travel.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(position - context.Position, travel) <= 0f)
                context.HomingIgnoredTarget = default;
        }

        static bool SameTarget(HomingTarget left, HomingTarget right)
        {
            return left.IsAssigned && right.IsAssigned && left.Id.Equals(right.Id);
        }
    }
}
