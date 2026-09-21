using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class PhysicsHitQuery : IHitQuery
    {
        const int BufferSize = 16;

        readonly Collider[] overlapBuffer = new Collider[BufferSize];
        readonly RaycastHit[] castBuffer = new RaycastHit[BufferSize];
        readonly Dictionary<ProjectileId, Collider> ignoreColliderByInstance = new Dictionary<ProjectileId, Collider>();

        public bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit)
        {
            ignoreColliderByInstance.TryGetValue(instanceId, out Collider ignore);

            int overlapCount = Physics.OverlapSphereNonAlloc(
                origin,
                radius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 incoming = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.forward;

            if (TryPickOverlap(instanceId, origin, incoming, overlapCount, ignore, out hit, out Collider hitCollider))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                return true;
            }

            if (distance <= 1e-8f || direction.sqrMagnitude <= 1e-12f)
            {
                RememberOrClearIgnore(instanceId, ignore, origin, radius, castCount: 0);
                hit = default;
                return false;
            }

            Vector3 dir = incoming;
            int castCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                dir,
                castBuffer,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (TryPickCast(instanceId, origin, dir, castCount, ignore, out hit, out hitCollider))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                return true;
            }

            RememberOrClearIgnore(instanceId, ignore, origin, radius, castCount);
            return false;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            if (from == to)
                return;
            if (!ignoreColliderByInstance.TryGetValue(from, out Collider collider) || collider == null)
                return;
            ignoreColliderByInstance[to] = collider;
        }

        public void Overlap(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (radius <= 0f)
                return;

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled)
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null)
                    continue;

                ProtectionLevel protection = ReadProtection(collider);
                if (protection == ProtectionLevel.Strong)
                    continue;

                Vector3 point = collider.ClosestPoint(center);
                Vector3 delta = point - center;
                Vector3 normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up;
                results.Add(new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Protection = protection,
                    TargetId = damageable.TargetId,
                    Point = point,
                    Normal = normal
                });
            }
        }

        public void OverlapDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (radius <= 0f)
                return;

            const float halfHeight = 1f;
            Vector3 point0 = center + Vector3.up * halfHeight;
            Vector3 point1 = center - Vector3.up * halfHeight;
            int count = Physics.OverlapCapsuleNonAlloc(
                point0,
                point1,
                radius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            float radiusSq = radius * radius;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled)
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null)
                    continue;

                ProtectionLevel protection = ReadProtection(collider);
                if (protection == ProtectionLevel.Strong)
                    continue;

                Vector3 sample = ((Component)damageable).transform.position;
                float dx = sample.x - center.x;
                float dz = sample.z - center.z;
                if (dx * dx + dz * dz > radiusSq)
                    continue;

                Vector3 point = collider.ClosestPoint(center);
                Vector3 delta = point - center;
                Vector3 normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up;
                results.Add(new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Protection = protection,
                    TargetId = damageable.TargetId,
                    Point = point,
                    Normal = normal
                });
            }
        }

        bool TryPickOverlap(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 incoming,
            int count,
            Collider ignore,
            out HitInfo hit,
            out Collider hitCollider)
        {
            hit = default;
            hitCollider = null;
            float best = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled || collider == ignore)
                    continue;

                Vector3 point = collider.ClosestPoint(origin);
                float distSq = (point - origin).sqrMagnitude;
                if (distSq >= best)
                    continue;

                best = distSq;
                hit = BuildHit(instanceId, collider, origin, incoming, point, origin - point);
                hitCollider = collider;
                found = true;
            }

            return found;
        }

        bool TryPickCast(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 incoming,
            int count,
            Collider ignore,
            out HitInfo hit,
            out Collider hitCollider)
        {
            hit = default;
            hitCollider = null;
            float best = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit rayHit = castBuffer[i];
                Collider collider = rayHit.collider;
                if (collider == null || !collider.enabled || collider == ignore)
                    continue;

                if (rayHit.distance >= best)
                    continue;

                best = rayHit.distance;
                hit = BuildHit(instanceId, collider, origin, incoming, rayHit.point, rayHit.normal);
                hitCollider = collider;
                found = true;
            }

            return found;
        }

        void RememberOrClearIgnore(ProjectileId instanceId, Collider ignore, Vector3 origin, float radius, int castCount)
        {
            if (ignore == null)
            {
                ignoreColliderByInstance.Remove(instanceId);
                return;
            }

            if (Overlaps(ignore, origin, radius) || CastIncludes(castCount, ignore))
            {
                ignoreColliderByInstance[instanceId] = ignore;
                return;
            }

            ignoreColliderByInstance.Remove(instanceId);
        }

        bool CastIncludes(int count, Collider ignore)
        {
            for (int i = 0; i < count; i++)
            {
                if (castBuffer[i].collider == ignore)
                    return true;
            }

            return false;
        }

        static bool Overlaps(Collider collider, Vector3 origin, float radius)
        {
            if (collider == null || !collider.enabled)
                return false;

            Vector3 closest = collider.ClosestPoint(origin);
            float skin = radius + 0.01f;
            return (closest - origin).sqrMagnitude <= skin * skin;
        }

        static HitInfo BuildHit(
            ProjectileId instanceId,
            Collider collider,
            Vector3 origin,
            Vector3 incoming,
            Vector3 point,
            Vector3 reportedNormal)
        {
            var info = new HitInfo
            {
                InstanceId = instanceId,
                Point = point,
                Normal = SurfaceNormal(collider, origin, incoming, point, reportedNormal)
            };

            var damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                info.Kind = HitKind.Target;
                info.TargetId = damageable.TargetId;
            }
            else
            {
                info.Kind = HitKind.World;
            }

            info.Protection = ReadProtection(collider);
            return info;
        }

        static ProtectionLevel ReadProtection(Collider collider)
        {
            var protection = collider.GetComponentInParent<IProjectileProtection>();
            return protection != null ? protection.Level : ProtectionLevel.None;
        }

        static Vector3 SurfaceNormal(
            Collider collider,
            Vector3 origin,
            Vector3 incoming,
            Vector3 point,
            Vector3 reported)
        {
            Vector3 incomingDir = incoming.sqrMagnitude > 1e-12f ? incoming.normalized : Vector3.forward;
            float rayLength = Vector3.Distance(origin, point) + 1f;
            if (collider != null && collider.Raycast(new Ray(origin, incomingDir), out RaycastHit surface, rayLength))
                return surface.normal;

            Vector3 normal = reported;
            if (normal.sqrMagnitude < 1e-10f && collider != null)
            {
                Vector3 fromSurface = origin - collider.ClosestPoint(origin);
                if (fromSurface.sqrMagnitude > 1e-10f)
                    normal = fromSurface;
            }

            if (normal.sqrMagnitude < 1e-10f)
                normal = -incomingDir;

            normal.Normalize();
            if (Vector3.Dot(normal, incomingDir) > 0f)
                normal = -normal;
            return normal;
        }
    }
}
