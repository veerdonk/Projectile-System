using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    interface IHitQuery
    {
        bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit);

        void Overlap(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        void OverlapDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results);

        void CopyIgnore(ProjectileId from, ProjectileId to);
    }
}
