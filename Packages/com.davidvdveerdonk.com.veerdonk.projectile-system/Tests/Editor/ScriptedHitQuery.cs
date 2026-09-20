using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    sealed class ScriptedHitQuery : IHitQuery
    {
        readonly List<OverlapBody> overlapBodies = new List<OverlapBody>();
        HitInfo? queued;
        bool hasQueued;
        bool filterInstance;
        ProjectileId queuedInstance;

        public void Queue(
            HitKind kind,
            Vector3 point,
            Vector3 normal,
            TargetId targetId = default,
            ProtectionLevel protection = ProtectionLevel.None)
        {
            queued = new HitInfo
            {
                Kind = kind,
                Protection = protection,
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
            TargetId targetId = default,
            ProtectionLevel protection = ProtectionLevel.None)
        {
            Queue(kind, point, normal, targetId, protection);
            filterInstance = true;
            queuedInstance = instanceId;
        }

        public void AddOverlapTarget(
            TargetId targetId,
            Vector3 position,
            ProtectionLevel protection = ProtectionLevel.None)
        {
            overlapBodies.Add(new OverlapBody
            {
                Kind = HitKind.Target,
                TargetId = targetId,
                Position = position,
                Protection = protection
            });
        }

        public void AddOverlapWorld(Vector3 position)
        {
            overlapBodies.Add(new OverlapBody
            {
                Kind = HitKind.World,
                Position = position
            });
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

        public void Overlap(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            results.Clear();
            if (radius <= 0f)
                return;

            float radiusSq = radius * radius;
            for (int i = 0; i < overlapBodies.Count; i++)
            {
                OverlapBody body = overlapBodies[i];
                if (body.Kind != HitKind.Target || body.Protection == ProtectionLevel.Strong)
                    continue;

                Vector3 delta = body.Position - center;
                if (delta.sqrMagnitude > radiusSq)
                    continue;

                results.Add(new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Protection = body.Protection,
                    TargetId = body.TargetId,
                    Point = body.Position,
                    Normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up
                });
            }
        }

        public void OverlapDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            results.Clear();
            if (radius <= 0f)
                return;

            float radiusSq = radius * radius;
            for (int i = 0; i < overlapBodies.Count; i++)
            {
                OverlapBody body = overlapBodies[i];
                if (body.Kind != HitKind.Target || body.Protection == ProtectionLevel.Strong)
                    continue;

                float dx = body.Position.x - center.x;
                float dz = body.Position.z - center.z;
                if (dx * dx + dz * dz > radiusSq)
                    continue;

                Vector3 delta = body.Position - center;
                results.Add(new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Protection = body.Protection,
                    TargetId = body.TargetId,
                    Point = body.Position,
                    Normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up
                });
            }
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
        }

        struct OverlapBody
        {
            public HitKind Kind;
            public TargetId TargetId;
            public Vector3 Position;
            public ProtectionLevel Protection;
        }
    }
}
