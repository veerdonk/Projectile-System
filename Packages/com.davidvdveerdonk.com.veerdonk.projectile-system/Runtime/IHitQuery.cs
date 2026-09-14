using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    interface IHitQuery
    {
        bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit);
    }
}
