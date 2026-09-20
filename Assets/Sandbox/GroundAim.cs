using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Horizontal aim from a camera ray against the world ground plane (Y = 0).
    /// </summary>
    public static class GroundAim
    {
        public static bool TryDirection(Ray cameraRay, Vector3 from, out Vector3 direction)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(cameraRay, out float enter) || enter < 0f)
            {
                direction = Vector3.forward;
                return false;
            }

            Vector3 hit = cameraRay.GetPoint(enter);
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
    }
}
