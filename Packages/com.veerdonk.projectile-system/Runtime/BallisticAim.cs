using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Fire-time launch direction so a given speed and gravity pass through a point.
    /// Uses world down (0,-1,0). Always the shorter-time (flatter) root: extra speed
    /// flattens toward the line of sight; a level shot at minimum speed leaves at about 45°.
    /// Does not simulate. <see cref="Delivery.Arc"/> calls this from <see cref="IProjectileWorld.Spawn"/>
    /// when <see cref="SpawnRequest.HomingAcquirePointSet"/> is true. Games may still call it to pitch a barrel.
    /// Arc flight still uses the Burst integrate. Straight ignores gravity.
    /// </summary>
    public static class BallisticAim
    {
        const float Epsilon = 1e-6f;

        /// <summary>
        /// Computes the launch direction from <paramref name="origin"/> through <paramref name="target"/>
        /// at <paramref name="speed"/> with downward <paramref name="gravity"/>.
        /// </summary>
        /// <returns>
        /// <c>false</c> if the point is unreachable (too far, non-positive speed, or
        /// gravity 0 with a target that is not on the horizontal ray).
        /// </returns>
        public static bool TrySolve(
            Vector3 origin,
            Vector3 target,
            float speed,
            float gravity,
            out Vector3 direction)
        {
            direction = default;
            if (speed <= Epsilon)
                return false;

            Vector3 delta = target - origin;
            float distanceSq = delta.sqrMagnitude;
            if (distanceSq <= Epsilon)
                return false;

            if (gravity <= 0f)
                return TrySolveStraight(delta, out direction);

            return TrySolveArcs(delta, speed, gravity, out direction);
        }

        static bool TrySolveStraight(Vector3 delta, out Vector3 direction)
        {
            direction = default;
            if (Mathf.Abs(delta.y) > Epsilon)
                return false;

            Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
            if (horizontal.sqrMagnitude <= Epsilon)
                return false;

            direction = horizontal.normalized;
            return true;
        }

        static bool TrySolveArcs(
            Vector3 delta,
            float speed,
            float gravity,
            out Vector3 direction)
        {
            direction = default;

            double g = gravity;
            double vx = speed;
            double dx = delta.x;
            double dy = delta.y;
            double dz = delta.z;
            double a = 0.25 * g * g;
            double b = g * dy - vx * vx;
            double c = dx * dx + dy * dy + dz * dz;
            double disc = b * b - 4.0 * a * c;
            if (disc < 0.0)
            {
                if (disc > -1e-8)
                    disc = 0.0;
                else
                    return false;
            }

            double sqrtDisc = System.Math.Sqrt(disc);
            double inv2A = 0.5 / a;
            double u1 = (-b - sqrtDisc) * inv2A;
            double u2 = (-b + sqrtDisc) * inv2A;

            double t = 0.0;
            int count = 0;
            if (TryPositiveTime(u1, out double t1))
            {
                t = t1;
                count = 1;
            }

            if (TryPositiveTime(u2, out double t2) && (count == 0 || t2 < t))
            {
                t = t2;
                count = 1;
            }

            if (count == 0)
                return false;

            Vector3 launch = new Vector3(
                delta.x,
                delta.y + 0.5f * gravity * (float)(t * t),
                delta.z);
            float denom = speed * (float)t;
            if (denom <= Epsilon || launch.sqrMagnitude <= Epsilon)
                return false;

            direction = launch / denom;
            float mag = direction.magnitude;
            if (mag <= Epsilon)
                return false;

            direction /= mag;
            return true;
        }

        static bool TryPositiveTime(double u, out double t)
        {
            t = 0.0;
            if (u <= Epsilon)
                return false;

            t = System.Math.Sqrt(u);
            return t > Epsilon;
        }
    }
}
