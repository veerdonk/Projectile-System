using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Places the children a <see cref="Multiply"/> produces. Does not aim them.</summary>
    public interface IMultiplyPattern
    {
        /// <summary>
        /// World-space offset from the parent or hit point for child <paramref name="index"/> of
        /// <paramref name="count"/>. The plane is perpendicular to <paramref name="forward"/>.
        /// Local +X is right, +Y is up. <paramref name="spread"/> of 0 is a zero offset.
        /// </summary>
        Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count);
    }

    static class MultiplyPatternPlane
    {
        internal static Vector3 ToWorld(Vector3 forward, float localX, float localY)
        {
            if (forward.sqrMagnitude < 1e-12f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < 1e-8f)
                right = Vector3.right;
            else
                right.Normalize();

            Vector3 up = Vector3.Cross(right, forward);
            return right * localX + up * localY;
        }

        internal static Vector3 FromLocal(Vector3 forward, Vector2 local)
        {
            return ToWorld(forward, local.x, local.y);
        }
    }
}
