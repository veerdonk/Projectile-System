using UnityEngine;

namespace Veerdonk.ProjectileSystem.Samples
{
    /// <summary>
    /// Aim from a camera ray: colliders first (dummy tops), else the world ground plane (Y = 0).
    /// </summary>
    public static class GroundAim
    {
        const float AimRayLength = 500f;

        public static bool TryPoint(Ray cameraRay, out Vector3 point)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(cameraRay, out float enter) || enter < 0f)
            {
                point = Vector3.zero;
                return false;
            }

            point = cameraRay.GetPoint(enter);
            return true;
        }

        static readonly RaycastHit[] aimHits = new RaycastHit[32];

        public static bool TryAimPoint(Ray cameraRay, out Vector3 point, out Collider collider)
        {
            int count = Physics.RaycastNonAlloc(cameraRay, aimHits, AimRayLength);
            float best = float.MaxValue;
            RaycastHit chosen = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = aimHits[i].collider;
                if (candidate == null || candidate.GetComponentInParent<ProjectileView>() != null)
                    continue;
                if (aimHits[i].distance >= best)
                    continue;

                best = aimHits[i].distance;
                chosen = aimHits[i];
                found = true;
            }

            if (found)
            {
                point = chosen.point;
                collider = chosen.collider;
                return true;
            }

            collider = null;
            return TryPoint(cameraRay, out point);
        }

        public static bool TryDirection(Ray cameraRay, Vector3 from, out Vector3 direction)
        {
            if (!TryPoint(cameraRay, out Vector3 hit))
            {
                direction = Vector3.forward;
                return false;
            }

            Vector3 flat = hit - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f)
            {
                direction = Vector3.forward;
                return false;
            }

            direction = flat.normalized;
            return true;
        }

        public static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f)
                return Vector3.forward;
            return direction.normalized;
        }
    }
}
