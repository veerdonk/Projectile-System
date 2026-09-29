using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// PhysX (or scripted) queries for one world. The pool never calls this; the simulation does.
    /// </summary>
    interface IHitQuery
    {
        HitTargetCatalog HitTargets { get; set; }

        /// <summary>SphereCast along a move segment. False means no hit.</summary>
        bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit);

        /// <summary>
        /// Far surface of the body this instance just hit, along <paramref name="travel"/>.
        /// False when that hit has no collider and no published volume.
        /// </summary>
        bool TryPierceExit(ProjectileId instanceId, Vector3 travel, out Vector3 surface);

        /// <summary>Sphere overlap of Targets for AreaEffect. Ignores World and other projectiles. Skips Strong.</summary>
        void Overlap(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        /// <summary>
        /// Flat XZ disk of Targets for GroundField. Y does not count. Ignores World and other projectiles. Skips Strong.
        /// </summary>
        void OverlapDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        /// <summary>Child instances ignore the same collider the parent already overlapped.</summary>
        void CopyIgnore(ProjectileId from, ProjectileId to);

        /// <summary>
        /// Direct overlap and sweep skip this collider until the instance body no longer overlaps it.
        /// Same one slot as <see cref="CopyIgnore"/>. Null does nothing.
        /// </summary>
        void SetIgnore(ProjectileId instanceId, Collider collider);

        /// <summary>Drops the one-slot collider skip and the published-target skip for this instance.</summary>
        void ClearIgnore(ProjectileId instanceId);

        /// <summary>
        /// The next cast keeps the current skip even if the body has left the surface.
        /// Used so a beam does not immediately hit the same wall or dummy again.
        /// </summary>
        void HoldIgnore(ProjectileId instanceId);
    }
}
