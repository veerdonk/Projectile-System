using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    sealed class ScriptedHitQuery : IHitQuery
    {
        readonly List<OverlapBody> overlapBodies = new List<OverlapBody>();
        HitInfo? queued;
        bool hasQueued;
        bool filterInstance;
        ProjectileId queuedInstance;
        HitTargetCatalog hitTargets;

        HitTargetCatalog IHitQuery.HitTargets
        {
            get => hitTargets;
            set => hitTargets = value;
        }

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

        public void ClearOverlaps()
        {
            overlapBodies.Clear();
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
            hitTargets?.ReleaseIfSeparated(instanceId, origin, radius);
            bool queuedForThis = hasQueued && (!filterInstance || queuedInstance == instanceId);
            HitInfo publishedHit = default;
            float publishedTravel = 0f;
            bool published = false;
            if (hitTargets != null)
            {
                published = hitTargets.TrySweep(
                    instanceId,
                    origin,
                    direction,
                    distance,
                    radius,
                    out publishedHit,
                    out publishedTravel,
                    out _);
            }

            if (published && (!queuedForThis || publishedTravel < QueuedTravel(origin, direction, queued.Value.Point)))
            {
                hit = publishedHit;
                hit.InstanceId = instanceId;
                hitTargets.NoteHit(instanceId, hit.TargetId);
                return true;
            }

            if (!queuedForThis)
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

        public bool TryPierceExit(ProjectileId instanceId, Vector3 travel, out Vector3 surface)
        {
            if (hitTargets != null && hitTargets.TryPierceExit(instanceId, travel, out surface))
                return true;

            surface = default;
            return false;
        }

        static float QueuedTravel(Vector3 origin, Vector3 direction, Vector3 point)
        {
            if (direction.sqrMagnitude <= 1e-12f)
                return 0f;
            return Vector3.Dot(point - origin, direction.normalized);
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

            hitTargets?.AppendSphere(instanceId, center, radius, results);
        }

        public void OverlapDisk(ProjectileId instanceId, ProjectileId ignoreSetOwner, Vector3 center, float radius, List<HitInfo> results)
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

            hitTargets?.AppendDisk(instanceId, center, radius, results);
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            hitTargets?.CopyIgnore(from, to);
        }

        public void SetIgnore(ProjectileId instanceId, Collider collider)
        {
        }

        public void SetIgnoreSet(ProjectileId instanceId, IReadOnlyList<Collider> colliders)
        {
        }

        public void CopyIgnoreSet(ProjectileId from, ProjectileId to)
        {
        }

        public void ReleaseIgnoreSet(ProjectileId instanceId)
        {
        }

        public void ClearIgnore(ProjectileId instanceId)
        {
            hitTargets?.ClearIgnore(instanceId);
        }

        public void HoldIgnore(ProjectileId instanceId)
        {
            hitTargets?.HoldIgnore(instanceId);
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
