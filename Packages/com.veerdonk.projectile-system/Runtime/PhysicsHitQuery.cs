using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class PhysicsHitQuery : IHitQuery
    {
        const int BufferSize = 16;
        const float Skin = 0.01f;

        readonly Collider[] overlapBuffer = new Collider[BufferSize];
        readonly Collider[] skinBuffer = new Collider[BufferSize * 4];
        readonly RaycastHit[] castBuffer = new RaycastHit[BufferSize];
        readonly Dictionary<ProjectileId, Collider> ignoreColliderByInstance = new Dictionary<ProjectileId, Collider>();
        readonly Dictionary<ProjectileId, PierceContact> pierceContactByInstance = new Dictionary<ProjectileId, PierceContact>();
        readonly HashSet<ProjectileId> holdIgnore = new HashSet<ProjectileId>();

        public HitTargetCatalog HitTargets { get; set; }

        public bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit)
        {
            bool hold = holdIgnore.Remove(instanceId);
            HitTargets?.ReleaseIfSeparated(instanceId, origin, radius);
            if (!hold
                && ignoreColliderByInstance.TryGetValue(instanceId, out Collider stored)
                && (stored == null || !stored.enabled || !Overlaps(stored, origin, radius)))
                ignoreColliderByInstance.Remove(instanceId);

            ignoreColliderByInstance.TryGetValue(instanceId, out Collider ignore);

            int overlapCount = Physics.OverlapSphereNonAlloc(
                origin,
                radius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 incoming = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.forward;
            HitInfo publishedHit = default;
            float publishedTravel = 0f;
            float publishedClose = 0f;
            bool published = HitTargets != null && HitTargets.TrySweep(
                instanceId,
                origin,
                incoming,
                distance,
                radius,
                out publishedHit,
                out publishedTravel,
                out publishedClose);

            if (TryPickOverlap(instanceId, origin, incoming, radius, overlapCount, ignore, out hit, out Collider hitCollider, out float overlapClose)
                && (!published || publishedTravel > 1e-4f || overlapClose <= publishedClose))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                RememberPierceContact(instanceId, hitCollider, hit.Point, incoming);
                return true;
            }

            if (distance <= 1e-8f || direction.sqrMagnitude <= 1e-12f)
                return TakePublishedOrMiss(instanceId, origin, radius, ignore, castCount: 0, published, publishedHit, ref hit);

            int castCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                incoming,
                castBuffer,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (TryPickCast(instanceId, origin, incoming, radius, castCount, ignore, out hit, out hitCollider, out float castTravel)
                && (!published || castTravel <= publishedTravel))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                RememberPierceContact(instanceId, hitCollider, hit.Point, incoming);
                return true;
            }

            return TakePublishedOrMiss(instanceId, origin, radius, ignore, castCount, published, publishedHit, ref hit);
        }

        public bool TryPierceExit(ProjectileId instanceId, Vector3 travel, out Vector3 surface)
        {
            if (pierceContactByInstance.TryGetValue(instanceId, out PierceContact contact))
                return TryExitSurface(contact, out surface);

            if (HitTargets != null && HitTargets.TryPierceExit(instanceId, travel, out surface))
                return true;

            surface = default;
            return false;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            HitTargets?.CopyIgnore(from, to);
            if (from == to)
                return;
            if (!ignoreColliderByInstance.TryGetValue(from, out Collider collider) || collider == null)
                return;
            ignoreColliderByInstance[to] = collider;
        }

        public void SetIgnore(ProjectileId instanceId, Collider collider)
        {
            if (collider == null)
                return;
            ignoreColliderByInstance[instanceId] = collider;
        }

        public void ClearIgnore(ProjectileId instanceId)
        {
            ignoreColliderByInstance.Remove(instanceId);
            pierceContactByInstance.Remove(instanceId);
            holdIgnore.Remove(instanceId);
            HitTargets?.ClearIgnore(instanceId);
        }

        public void HoldIgnore(ProjectileId instanceId)
        {
            holdIgnore.Add(instanceId);
            HitTargets?.HoldIgnore(instanceId);
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

                Vector3 point = AreaPoint(collider, center);
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

            HitTargets?.AppendSphere(instanceId, center, radius, results);
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

                Vector3 point = AreaPoint(collider, center);
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

            HitTargets?.AppendDisk(instanceId, center, radius, results);
        }

        bool TakePublishedOrMiss(
            ProjectileId instanceId,
            Vector3 origin,
            float radius,
            Collider ignore,
            int castCount,
            bool published,
            HitInfo publishedHit,
            ref HitInfo hit)
        {
            if (!published)
            {
                pierceContactByInstance.Remove(instanceId);
                RememberOrClearIgnore(instanceId, ignore, origin, radius, castCount);
                hit = default;
                return false;
            }

            HitTargets.NoteHit(instanceId, publishedHit.TargetId);
            pierceContactByInstance.Remove(instanceId);
            RememberOrClearIgnore(instanceId, ignore, origin, radius, castCount);
            hit = publishedHit;
            return true;
        }

        bool TryPickOverlap(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 incoming,
            float radius,
            int count,
            Collider ignore,
            out HitInfo hit,
            out Collider hitCollider,
            out float closeness)
        {
            hit = default;
            hitCollider = null;
            closeness = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled || collider == ignore || IsPresentation(collider))
                    continue;

                Vector3 point;
                Vector3 reported;
                if (SupportsClosestPoint(collider))
                {
                    point = collider.ClosestPoint(origin);
                    reported = origin - point;
                }
                else if (TryBackedUpRay(collider, origin, incoming, radius, out RaycastHit surface))
                {
                    point = surface.point;
                    reported = surface.normal;
                }
                else
                {
                    point = origin;
                    reported = Vector3.zero;
                }

                float distSq = (point - origin).sqrMagnitude;
                if (distSq >= closeness)
                    continue;

                closeness = distSq;
                hit = BuildHit(instanceId, collider, origin, incoming, radius, point, reported);
                hitCollider = collider;
                found = true;
            }

            return found;
        }

        bool TryPickCast(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 incoming,
            float radius,
            int count,
            Collider ignore,
            out HitInfo hit,
            out Collider hitCollider,
            out float travel)
        {
            hit = default;
            hitCollider = null;
            travel = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit rayHit = castBuffer[i];
                Collider collider = rayHit.collider;
                if (collider == null || !collider.enabled || collider == ignore || IsPresentation(collider))
                    continue;

                if (rayHit.distance >= travel)
                    continue;

                travel = rayHit.distance;
                hit = BuildHit(instanceId, collider, origin, incoming, radius, rayHit.point, rayHit.normal);
                hitCollider = collider;
                found = true;
            }

            return found;
        }

        void RememberPierceContact(ProjectileId instanceId, Collider collider, Vector3 point, Vector3 incoming)
        {
            if (collider == null)
            {
                pierceContactByInstance.Remove(instanceId);
                return;
            }

            pierceContactByInstance[instanceId] = new PierceContact(collider, point, incoming);
        }

        static bool TryExitSurface(PierceContact contact, out Vector3 surface)
        {
            Collider collider = contact.Collider;
            surface = default;
            if (collider == null || collider is TerrainCollider)
                return false;

            Vector3 dir = contact.Incoming.sqrMagnitude > 1e-12f ? contact.Incoming.normalized : Vector3.forward;
            Bounds bounds = collider.bounds;
            if (!SupportsClosestPoint(collider))
            {
                float far = Vector3.Distance(contact.Point, bounds.center) + bounds.extents.magnitude + 1f;
                var back = new Ray(contact.Point + dir * far, -dir);
                if (!collider.Raycast(back, out RaycastHit exit, far))
                    return false;

                surface = exit.point;
                return true;
            }

            float reach = bounds.extents.magnitude + 1f;
            Vector3 probe = bounds.center + dir * reach;
            surface = collider.ClosestPoint(probe);
            if ((surface - probe).sqrMagnitude < 1e-8f)
            {
                probe = bounds.center + dir * (reach + bounds.size.magnitude + 1f);
                surface = collider.ClosestPoint(probe);
            }

            return true;
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

        static bool IsPresentation(Collider collider)
        {
            return collider.GetComponentInParent<ProjectileView>() != null;
        }

        bool Overlaps(Collider collider, Vector3 origin, float radius)
        {
            if (collider == null || !collider.enabled)
                return false;

            float skin = radius + Skin;
            if (SupportsClosestPoint(collider))
            {
                Vector3 closest = collider.ClosestPoint(origin);
                return (closest - origin).sqrMagnitude <= skin * skin;
            }

            int count = Physics.OverlapSphereNonAlloc(origin, skin, skinBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (skinBuffer[i] == collider)
                    return true;
            }

            return false;
        }

        static bool SupportsClosestPoint(Collider collider)
        {
            if (collider is TerrainCollider)
                return false;
            return !(collider is MeshCollider mesh) || mesh.convex;
        }

        static bool TryBackedUpRay(Collider collider, Vector3 origin, Vector3 incoming, float radius, out RaycastHit surface)
        {
            Vector3 dir = incoming.sqrMagnitude > 1e-12f ? incoming.normalized : Vector3.forward;
            float backup = radius + Skin;
            return collider.Raycast(new Ray(origin - dir * backup, dir), out surface, backup * 2f + 1f);
        }

        static Vector3 AreaPoint(Collider collider, Vector3 center)
        {
            return SupportsClosestPoint(collider) ? collider.ClosestPoint(center) : collider.bounds.ClosestPoint(center);
        }

        static HitInfo BuildHit(
            ProjectileId instanceId,
            Collider collider,
            Vector3 origin,
            Vector3 incoming,
            float radius,
            Vector3 point,
            Vector3 reportedNormal)
        {
            var info = new HitInfo
            {
                InstanceId = instanceId,
                Point = point,
                Normal = SurfaceNormal(collider, origin, incoming, radius, point, reportedNormal)
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
            float radius,
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
                if (SupportsClosestPoint(collider))
                {
                    Vector3 fromSurface = origin - collider.ClosestPoint(origin);
                    if (fromSurface.sqrMagnitude > 1e-10f)
                        normal = fromSurface;
                }
                else if (TryBackedUpRay(collider, origin, incomingDir, radius, out RaycastHit backed))
                {
                    normal = backed.normal;
                }
            }

            if (normal.sqrMagnitude < 1e-10f)
                normal = -incomingDir;

            normal.Normalize();
            if (Vector3.Dot(normal, incomingDir) > 0f)
                normal = -normal;
            return normal;
        }

        readonly struct PierceContact
        {
            public PierceContact(Collider collider, Vector3 point, Vector3 incoming)
            {
                Collider = collider;
                Point = point;
                Incoming = incoming;
            }

            public Collider Collider { get; }
            public Vector3 Point { get; }
            public Vector3 Incoming { get; }
        }
    }
}
