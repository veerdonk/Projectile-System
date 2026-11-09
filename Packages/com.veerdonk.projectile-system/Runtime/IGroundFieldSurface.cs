using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Finds the ground a <see cref="GroundField"/> is planted on. Set it on
    /// <see cref="IProjectileWorld.GroundFieldSurface"/>. The world calls it once per plant, inside the stage that
    /// planted the field (OnHit, OnTravel, or OnExpire, including OnExpire run by <see cref="IProjectileWorld.Despawn"/>).
    /// </summary>
    public interface IGroundFieldSurface
    {
        /// <summary>
        /// Returns the ground under <paramref name="point"/>: the contact point on OnHit, the instance position
        /// otherwise. <paramref name="groundNormal"/> does not need to be normalized; one facing down is flipped, and
        /// zero or one more than about 84 degrees from up is replaced by <see cref="Vector3.up"/>.
        /// False plants the field as without a surface: at <paramref name="point"/> with Y = 0, normal up.
        /// </summary>
        bool TryGetGround(Vector3 point, out Vector3 groundPoint, out Vector3 groundNormal);
    }
}
