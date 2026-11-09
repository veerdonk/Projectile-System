using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// OnTick: steer toward a homing target. <see cref="TurnRateDegreesPerSecond"/> is the maximum turn in
    /// degrees per second — Homing(100) turns at 100°/s, Homing(10) at 10°/s.
    /// Hitscan uses the same value as the maximum kink in degrees per polyline segment.
    /// Beam steps along the pulse and turns at degrees per second, using distance / speed as the step time,
    /// so the polyline curves the way a Straight shot with this turn rate would.
    /// If spawn did not assign a target,
    /// the first lock is <see cref="ClosestToPointAcquisition"/> when <see cref="SpawnRequest.HomingAcquirePointSet"/>
    /// is true, and <see cref="Acquisition"/> otherwise. After that first lock, empty locks use <see cref="Acquisition"/>.
    /// A lock whose <see cref="TargetId"/> is in neither catalog is dropped the next time Homing runs and re-acquired
    /// through <see cref="Acquisition"/>, never the aim point; with nothing in the cone the instance flies on unlocked.
    /// On Hitscan that covers a spawn-assigned target that is not published when the query resolves.
    /// A lock whose target is still published is kept, even when another candidate is nearer.
    /// A Beam lock set by the four-argument <c>AimBeam</c> is kept when its target is gone.
    /// Candidates are <see cref="IProjectileWorld.SetHomingCandidate"/> poses and published hit-target positions.
    /// The same <see cref="TargetId"/> in both catalogs is one candidate, and the hit-target position wins.
    /// The default acquisition only takes candidates inside <see cref="AcquireHalfAngleDegrees"/> of travel.
    /// A Target hit drops the lock and ignores that TargetId while it stays in front of travel, so a pierce shot
    /// does not re-lock the dummy it is still overlapping. Once it is behind travel it stays a fallback: the default
    /// acquisition locks it only when no other candidate is in the cone. Multiply children spawned by a Target hit
    /// start with that target as their fallback.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTick)]
    public sealed class Homing : IModifier, IDeserializationCallback
    {
        static readonly ClosestToPointAcquisition AimPoint = new ClosestToPointAcquisition();

        /// <summary>Half-angle a new Homing starts with. 90 takes every candidate in front of travel.</summary>
        public const float DefaultAcquireHalfAngleDegrees = 90f;

        [SerializeField] float turnRateDegreesPerSecond;
        [SerializeField, Range(0f, 180f)] float acquireHalfAngleDegrees = DefaultAcquireHalfAngleDegrees;
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

        /// <summary>
        /// Homing with an acquire cone. <paramref name="acquireHalfAngleDegrees"/> is clamped to 0..180
        /// (<see cref="AcquireHalfAngleDegrees"/>). Null <paramref name="acquisition"/> is closest-in-travel-direction.
        /// </summary>
        public Homing(float turnRateDegreesPerSecond, float acquireHalfAngleDegrees, IHomingAcquisition acquisition = null)
            : this(turnRateDegreesPerSecond, acquisition)
        {
            AcquireHalfAngleDegrees = acquireHalfAngleDegrees;
        }

        /// <summary>
        /// Maximum turn in degrees per second on Straight, Arc, and Beam.
        /// On Hitscan, maximum kink in degrees per polyline segment.
        /// </summary>
        public float TurnRateDegreesPerSecond
        {
            get => turnRateDegreesPerSecond;
            set => turnRateDegreesPerSecond = value;
        }

        /// <summary>
        /// Degrees between travel and the offset to a candidate below which the default acquisition takes it.
        /// Clamped to 0..180 when set: 90 (default) takes every candidate in front of travel, 180 every candidate, 0 none.
        /// A serialized value outside that range is clamped when the acquisition reads it.
        /// Applies to every lock through <see cref="Acquisition"/>, not to the aim-point first lock or a kept lock.
        /// </summary>
        public float AcquireHalfAngleDegrees
        {
            get => acquireHalfAngleDegrees;
            set => acquireHalfAngleDegrees = Mathf.Clamp(value, 0f, 180f);
        }

        /// <summary>
        /// Picks a lock when spawn did not assign one, every empty lock after the first, and a new lock when the
        /// locked target is no longer published.
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
            projectile.HomingIgnoredPassed = false;
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

            if (target.IsAssigned)
            {
                if (context.HomingAcquirePointSet)
                    context.HomingAimPointUsed = true;
                if (!context.HomingLockHeld && !context.IsPublished(target))
                {
                    target = default;
                    context.HomingTarget = default;
                }
            }

            if (!target.IsAssigned)
            {
                if (!TryAcquireLock(context, out target))
                    return;
                context.HomingTarget = target;
                context.NoteHomingAcquired(target);
            }

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

            context.HomingAcquireHalfAngleDegrees = AcquireHalfAngleDegrees;
            return Acquisition.TryAcquire(context, out target) && !SameTarget(target, context.ShooterTarget);
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
            {
                context.HomingFallbackTarget = ignored;
                context.HomingIgnoredTarget = default;
            }
        }

        static bool SameTarget(HomingTarget left, HomingTarget right)
        {
            return left.IsAssigned && right.IsAssigned && left.Id.Equals(right.Id);
        }
    }
}
