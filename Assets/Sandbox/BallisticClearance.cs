using System;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Prefers the flatter <see cref="BallisticAim"/> root, then the high root if that path
    /// hits a collider before the aim point. No high-arc HUD; this is obstacle clearance.
    /// </summary>
    public static class BallisticClearance
    {
        public const float SimDt = 0.01f;
        public const float TargetHitRadius = 2.5f;

        public static bool TrySolve(
            Vector3 origin,
            Vector3 target,
            float speed,
            float gravity,
            float radius,
            float lifetimeSeconds,
            out Vector3 direction)
        {
            return TrySolve(
                origin,
                target,
                speed,
                gravity,
                dir => PathHitsObstacle(origin, dir, speed, gravity, radius, target, lifetimeSeconds),
                out direction);
        }

        public static bool TrySolve(
            Vector3 origin,
            Vector3 target,
            float speed,
            float gravity,
            Func<Vector3, bool> pathHitsObstacle,
            out Vector3 direction)
        {
            direction = default;
            if (!BallisticAim.TrySolve(origin, target, speed, gravity, out Vector3 low, highArc: false))
                return false;

            if (pathHitsObstacle == null || !pathHitsObstacle(low))
            {
                direction = low;
                return true;
            }

            if (BallisticAim.TrySolve(origin, target, speed, gravity, out Vector3 high, highArc: true) &&
                (high - low).sqrMagnitude > 1e-6f &&
                !pathHitsObstacle(high))
            {
                direction = high;
                return true;
            }

            direction = low;
            return true;
        }

        public static bool PathHitsObstacle(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float gravity,
            float radius,
            Vector3 target,
            float lifetimeSeconds)
        {
            if (direction.sqrMagnitude < 1e-8f || speed <= 0f || lifetimeSeconds <= 0f)
                return false;

            if (radius < 0.01f)
                radius = 0.01f;

            Vector3 velocity = direction.normalized * speed;
            Vector3 position = origin;
            float elapsed = 0f;
            float arrivalSq = TargetHitRadius * TargetHitRadius;
            Vector3 down = new Vector3(0f, -1f, 0f);

            while (elapsed < lifetimeSeconds)
            {
                if (gravity > 0f)
                    velocity += down * gravity * SimDt;

                Vector3 next = position + velocity * SimDt;
                Vector3 delta = next - position;
                float dist = delta.magnitude;
                if (dist > 1e-6f &&
                    Physics.SphereCast(position, radius, delta / dist, out RaycastHit hit, dist) &&
                    hit.distance > 1e-4f)
                {
                    return (hit.point - target).sqrMagnitude > arrivalSq;
                }

                position = next;
                elapsed += SimDt;

                Vector3 toTarget = target - position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude <= arrivalSq)
                    return false;
            }

            return false;
        }
    }
}
