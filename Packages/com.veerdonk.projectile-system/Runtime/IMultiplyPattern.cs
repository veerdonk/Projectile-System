using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Places the children a <see cref="Multiply"/> produces.
    /// Square, Circle, and RandomCircle do not aim them. <see cref="GridMultiplyPattern"/> does.
    /// </summary>
    public interface IMultiplyPattern
    {
        /// <summary>
        /// World-space offset from the parent or hit point for child <paramref name="index"/> of
        /// <paramref name="count"/>. The plane is perpendicular to <paramref name="forward"/>.
        /// Local +X is right, +Y is up. Procedural patterns treat <paramref name="spread"/> as meters
        /// (0 is a zero offset). <see cref="GridMultiplyPattern"/> uses its own Spacing and ignores
        /// <paramref name="spread"/> here.
        /// </summary>
        Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count);

        /// <summary>
        /// Travel direction for child <paramref name="index"/> of <paramref name="count"/>.
        /// Procedural patterns return <paramref name="forward"/>.
        /// <see cref="GridMultiplyPattern"/> rotates toward that cell so the farthest numbered cell
        /// is <paramref name="spread"/> degrees off <paramref name="forward"/> (0 returns forward).
        /// </summary>
        Vector3 GetDirection(ModifierContext context, Vector3 forward, float spread, int index, int count);

        /// <summary>
        /// How many children <see cref="Multiply"/> spawns for <paramref name="requested"/>.
        /// Procedural patterns return <paramref name="requested"/>.
        /// <see cref="GridMultiplyPattern"/> returns the smaller of that and the numbered-cell count.
        /// </summary>
        int ResolveCount(int requested);
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
