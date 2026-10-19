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
        /// Flat XZ disk of Targets for GroundField. Y does not count. Skips the set owned by <paramref name="ignoreSetOwner"/>. Ignores World and other projectiles. Skips Strong.
        /// </summary>
        void OverlapDisk(ProjectileId instanceId, ProjectileId ignoreSetOwner, Vector3 center, float radius, List<HitInfo> results);

        /// <summary>
        /// Child instances ignore the same collider the parent already overlapped, copy its ignore set
        /// (<see cref="CopyIgnoreSet"/>), and copy its shooter hit-target skip as it stands (a skip the parent already cleared
        /// starts cleared).
        /// </summary>
        void CopyIgnore(ProjectileId from, ProjectileId to);

        /// <summary>
        /// Direct overlap and sweep skip this collider until the instance body no longer overlaps it.
        /// Same one slot as <see cref="CopyIgnore"/>. Null does nothing.
        /// </summary>
        void SetIgnore(ProjectileId instanceId, Collider collider);

        /// <summary>
        /// Copies <paramref name="colliders"/> into a pooled set the instance skips for its whole life
        /// (Direct, area, and ground queries). Null or empty does nothing.
        /// </summary>
        void SetIgnoreSet(ProjectileId instanceId, IReadOnlyList<Collider> colliders);

        /// <summary>Copies only the ignore set of <paramref name="from"/> into its own pooled list for <paramref name="to"/>.</summary>
        void CopyIgnoreSet(ProjectileId from, ProjectileId to);

        /// <summary>Returns the instance's ignore set to the pool. Not part of <see cref="ClearIgnore"/>.</summary>
        void ReleaseIgnoreSet(ProjectileId instanceId);

        /// <summary>
        /// Drops the one-slot collider skip and the post-hit published-target skip for this instance, and arms the shooter
        /// hit-target skip again (<see cref="SpawnRequest.IgnoreTarget"/>). Beam calls this at the start of every pulse.
        /// </summary>
        void ClearIgnore(ProjectileId instanceId);

        /// <summary>
        /// The next cast keeps the current skip even if the body has left the surface.
        /// Used so a beam does not immediately hit the same wall or dummy again.
        /// </summary>
        void HoldIgnore(ProjectileId instanceId);
    }
}
