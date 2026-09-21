using UnityEngine;

namespace Sandbox
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

        public static bool TryAimPoint(Ray cameraRay, out Vector3 point, out Collider collider)
        {
            if (Physics.Raycast(cameraRay, out RaycastHit hit, AimRayLength) && hit.collider != null)
            {
                point = hit.point;
                collider = hit.collider;
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
