using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// PhysX (or scripted) queries for one world. The pool never calls this; the simulation does.
    /// </summary>
    interface IHitQuery
    {
        /// <summary>SphereCast along a move segment. False means no hit.</summary>
        bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit);

        /// <summary>Sphere overlap of Targets for AreaEffect. Ignores World and other projectiles. Skips Strong.</summary>
        void Overlap(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        /// <summary>
        /// Flat XZ disk of Targets for GroundField. Y does not count. Ignores World and other projectiles. Skips Strong.
        /// </summary>
        void OverlapDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        /// <summary>Child instances ignore the same collider the parent already overlapped.</summary>
        void CopyIgnore(ProjectileId from, ProjectileId to);
    }
}
