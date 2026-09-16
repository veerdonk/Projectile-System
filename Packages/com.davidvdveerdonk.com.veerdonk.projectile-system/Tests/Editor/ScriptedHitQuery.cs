using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    sealed class ScriptedHitQuery : IHitQuery
    {
        HitInfo? queued;
        bool hasQueued;
        bool filterInstance;
        ProjectileId queuedInstance;

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
            filterInstance = false;
            queuedInstance = default;
        }

        public void QueueFor(
            ProjectileId instanceId,
            HitKind kind,
            Vector3 point,
            Vector3 normal,
            TargetId targetId = default)
        {
            Queue(kind, point, normal, targetId);
            filterInstance = true;
            queuedInstance = instanceId;
        }

        public bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit)
        {
            if (!hasQueued)
            {
                hit = default;
                return false;
            }

            if (filterInstance && queuedInstance != instanceId)
            {
                hit = default;
                return false;
            }

            hit = queued.Value;
            hit.InstanceId = instanceId;
            hasQueued = false;
            queued = null;
            filterInstance = false;
            return true;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
        }
    }
}
