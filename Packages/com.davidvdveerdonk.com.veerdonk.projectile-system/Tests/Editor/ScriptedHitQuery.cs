using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    sealed class ScriptedHitQuery : IHitQuery
    {
        HitInfo? queued;
        bool hasQueued;

        public void Queue(HitKind kind, Vector3 point, Vector3 normal, TargetId targetId = default)
        {
            queued = new HitInfo
            {
                Kind = kind,
                Point = point,
                Normal = normal,
                TargetId = targetId
            };
            hasQueued = true;
        }

        public bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit)
        {
            if (!hasQueued)
            {
                hit = default;
                return false;
            }

            hit = queued.Value;
            hit.InstanceId = instanceId;
            hasQueued = false;
            queued = null;
            return true;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
        }
    }
}
