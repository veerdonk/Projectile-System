using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// An <see cref="IGroundFieldSurface"/> that also gets the radius of the field being planted, so it can sample
    /// the ground across the whole disk. When <see cref="IProjectileWorld.GroundFieldSurface"/> implements it, the
    /// world calls this overload instead of the point-only one.
    /// </summary>
    public interface IGroundFieldAreaSurface : IGroundFieldSurface
    {
        /// <summary>
        /// As <see cref="IGroundFieldSurface.TryGetGround(Vector3, out Vector3, out Vector3)"/>, with
        /// <paramref name="radius"/> the radius in metres the field is planted with
        /// (<see cref="GroundField.Radius"/> for the package modifier). The returned point and normal are checked and
        /// sanitized the same way.
        /// </summary>
        bool TryGetGround(Vector3 point, float radius, out Vector3 groundPoint, out Vector3 groundNormal);
    }
}
