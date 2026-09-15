using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// OnTick: steer toward a homing target. <see cref="TurnRateDegreesPerSecond"/> is the maximum turn in
    /// degrees per second — Homing(100) turns at 100°/s, Homing(10) at 10°/s. If spawn did not assign a target,
    /// <see cref="Acquisition"/> picks one once; the instance keeps that lock and does not retarget.
    /// </summary>
    public sealed class Homing : IModifier
    {
        public Homing(float turnRateDegreesPerSecond, IHomingAcquisition acquisition = null)
        {
            TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
            Acquisition = acquisition ?? new ClosestInTravelDirectionAcquisition();
        }

        public float TurnRateDegreesPerSecond { get; }

        public IHomingAcquisition Acquisition { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            HomingTarget target = context.HomingTarget;
            if (!target.IsAssigned)
            {
                if (!Acquisition.TryAcquire(context, out target))
                    return;
                context.HomingTarget = target;
            }

            if (!context.HomingCatalog.TryGetPosition(target, out Vector3 targetPosition))
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
    }
}
