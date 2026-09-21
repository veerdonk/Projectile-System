using System;
using Veerdonk.ProjectileSystem;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Samples
{
    /// <summary>
    /// Prefers the flatter <see cref="BallisticAim"/> root. If that path hits something
    /// other than the aimed collider, searches the minimum extra pitch that still hits it.
    /// </summary>
    public static class BallisticClearance
    {
        public const float SimDt = 0.01f;
        public const float TargetHitRadius = 2.5f;
        public const int LoftSearchSteps = 14;

        public enum PathOutcome
        {
            Clear,
            Blocked,
            Miss
        }

        public static bool TrySolve(
            Vector3 origin,
            Vector3 target,
            float speed,
            float gravity,
            float radius,
            float lifetimeSeconds,
            Collider intended,
            out Vector3 direction)
        {
            return TrySolve(
                origin,
                target,
                speed,
                gravity,
                dir => Trace(origin, dir, speed, gravity, radius, target, lifetimeSeconds, intended),
                out direction);
        }

        public static bool TrySolve(
            Vector3 origin,
            Vector3 target,
            float speed,
            float gravity,
            Func<Vector3, PathOutcome> trace,
            out Vector3 direction)
        {
            direction = default;
            if (!BallisticAim.TrySolve(origin, target, speed, gravity, out Vector3 low))
                return false;

            if (trace == null || trace(low) == PathOutcome.Clear)
            {
                direction = low;
                return true;
            }

            Vector3 cap = DirectionFromPitch(target - origin, 80f * Mathf.Deg2Rad);
            if (TryMinimumLoft(origin, target, low, cap, trace, out direction))
                return true;

            direction = low;
            return true;
        }

        public static bool TryMinimumLoft(
            Vector3 origin,
            Vector3 target,
            Vector3 low,
            Vector3 high,
            Func<Vector3, PathOutcome> trace,
            out Vector3 direction)
        {
            direction = default;
            if (trace == null)
                return false;

            float lo = Pitch(low);
            float hi = Pitch(high);
            if (hi < lo + 1e-4f)
                hi = lo + 70f * Mathf.Deg2Rad;

            Vector3 azimuth = target - origin;
            Vector3 best = default;
            bool found = false;

            for (int i = 0; i < LoftSearchSteps; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 dir = DirectionFromPitch(azimuth, mid);
                PathOutcome outcome = trace(dir);
                if (outcome == PathOutcome.Blocked)
                {
                    lo = mid;
                    continue;
                }

                hi = mid;
                if (outcome == PathOutcome.Clear)
                {
                    best = dir;
                    found = true;
                }
            }

            if (!found)
                return false;

            direction = best;
            return true;
        }

        public static float Pitch(Vector3 direction)
        {
            Vector3 n = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            return Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f));
        }

        public static Vector3 DirectionFromPitch(Vector3 azimuth, float pitch)
        {
            Vector3 flat = azimuth;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f)
                flat = Vector3.forward;
            flat.Normalize();
            float xz = Mathf.Cos(pitch);
            return new Vector3(flat.x * xz, Mathf.Sin(pitch), flat.z * xz);
        }

        public static PathOutcome Trace(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float gravity,
            float radius,
            Vector3 target,
            float lifetimeSeconds,
            Collider intended)
        {
            if (direction.sqrMagnitude < 1e-8f || speed <= 0f || lifetimeSeconds <= 0f)
                return PathOutcome.Miss;

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
                    return IsIntendedHit(hit, intended, target, arrivalSq)
                        ? PathOutcome.Clear
                        : PathOutcome.Blocked;
                }

                position = next;
                elapsed += SimDt;

                Vector3 toTarget = target - position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > arrivalSq)
                    continue;

                if (intended != null)
                    return PathOutcome.Miss;

                return PathOutcome.Clear;
            }

            return PathOutcome.Miss;
        }

        static bool IsIntendedHit(RaycastHit hit, Collider intended, Vector3 target, float arrivalSq)
        {
            if (intended != null)
            {
                if (hit.collider == intended)
                    return true;

                SandboxDummyTarget hitDummy = hit.collider.GetComponentInParent<SandboxDummyTarget>();
                SandboxDummyTarget intendedDummy = intended.GetComponentInParent<SandboxDummyTarget>();
                return hitDummy != null && hitDummy == intendedDummy;
            }

            return (hit.point - target).sqrMagnitude <= arrivalSq;
        }
    }
}
