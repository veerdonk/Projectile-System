using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class PhysicsHitQuery : IHitQuery
    {
        const int BufferSize = 16;
        const int MaxBufferSize = 1024;
        const float Skin = 0.01f;

        Collider[] overlapBuffer = new Collider[BufferSize];
        Collider[] skinBuffer = new Collider[BufferSize * 4];
        RaycastHit[] castBuffer = new RaycastHit[BufferSize];
        readonly Dictionary<ProjectileId, Collider> ignoreColliderByInstance = new Dictionary<ProjectileId, Collider>();
        readonly Dictionary<ProjectileId, PierceContact> pierceContactByInstance = new Dictionary<ProjectileId, PierceContact>();
        readonly Dictionary<ProjectileId, PierceContact> concaveExitByInstance = new Dictionary<ProjectileId, PierceContact>();
        readonly HashSet<ProjectileId> holdIgnore = new HashSet<ProjectileId>();
        readonly Dictionary<ProjectileId, List<Collider>> ignoreSetByInstance = new Dictionary<ProjectileId, List<Collider>>();
        readonly Stack<List<Collider>> ignoreSetPool = new Stack<List<Collider>>();

        public HitTargetCatalog HitTargets { get; set; }

        public bool TryCast(ProjectileId instanceId, Vector3 origin, Vector3 direction, float distance, float radius, out HitInfo hit)
        {
            bool hold = holdIgnore.Remove(instanceId);
            HitTargets?.ReleaseIfSeparated(instanceId, origin, radius);
            if (!hold
                && ignoreColliderByInstance.TryGetValue(instanceId, out Collider stored)
                && (stored == null || !stored.enabled || !StillTouching(instanceId, stored, origin, direction, radius)))
                ignoreColliderByInstance.Remove(instanceId);

            ignoreColliderByInstance.TryGetValue(instanceId, out Collider ignore);
            ignoreSetByInstance.TryGetValue(instanceId, out List<Collider> ignoreSet);

            int overlapCount = OverlapSphere(origin, radius, ~0, ref overlapBuffer);

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

            if (TryPickOverlap(instanceId, origin, incoming, radius, overlapCount, ignore, ignoreSet, out hit, out Collider hitCollider, out float overlapClose)
                && (!published || publishedTravel > 1e-4f || overlapClose <= publishedClose))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                RememberPierceContact(instanceId, hitCollider, hit.Point, incoming);
                return true;
            }

            if (distance <= 1e-8f || direction.sqrMagnitude <= 1e-12f)
                return TakePublishedOrMiss(instanceId, origin, direction, radius, ignore, castCount: 0, published, publishedHit, ref hit);

            int castCount = SphereCast(origin, radius, incoming, distance);

            if (TryPickCast(instanceId, origin, incoming, radius, castCount, ignore, ignoreSet, out hit, out hitCollider, out float castTravel)
                && (!published || castTravel <= publishedTravel))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                RememberPierceContact(instanceId, hitCollider, hit.Point, incoming);
                return true;
            }

            return TakePublishedOrMiss(instanceId, origin, direction, radius, ignore, castCount, published, publishedHit, ref hit);
        }

        public bool TryPierceExit(ProjectileId instanceId, Vector3 travel, out Vector3 surface)
        {
            if (pierceContactByInstance.TryGetValue(instanceId, out PierceContact contact))
            {
                bool found = TryExitSurface(contact, out surface);
                if (found && !SupportsClosestPoint(contact.Collider))
                    concaveExitByInstance[instanceId] = new PierceContact(contact.Collider, surface, contact.Incoming);
                else
                    concaveExitByInstance.Remove(instanceId);
                return found;
            }

            if (HitTargets != null && HitTargets.TryPierceExit(instanceId, travel, out surface))
                return true;

            surface = default;
            return false;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            HitTargets?.CopyIgnore(from, to);
            CopyIgnoreSet(from, to);
            if (from == to)
                return;
            if (!ignoreColliderByInstance.TryGetValue(from, out Collider collider) || collider == null)
                return;
            ignoreColliderByInstance[to] = collider;
        }

        public void SetIgnoreSet(ProjectileId instanceId, IReadOnlyList<Collider> colliders)
        {
            if (colliders == null || colliders.Count == 0)
                return;

            List<Collider> set = RentSet(instanceId);
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                    set.Add(collider);
            }
        }

        public void CopyIgnoreSet(ProjectileId from, ProjectileId to)
        {
            if (from == to || !ignoreSetByInstance.TryGetValue(from, out List<Collider> source) || source.Count == 0)
                return;

            List<Collider> set = RentSet(to);
            for (int i = 0; i < source.Count; i++)
                set.Add(source[i]);
        }

        public void ReleaseIgnoreSet(ProjectileId instanceId)
        {
            if (!ignoreSetByInstance.TryGetValue(instanceId, out List<Collider> set))
                return;

            ignoreSetByInstance.Remove(instanceId);
            set.Clear();
            ignoreSetPool.Push(set);
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
            concaveExitByInstance.Remove(instanceId);
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

            int count = OverlapSphere(center, radius, ~0, ref overlapBuffer);

            ignoreSetByInstance.TryGetValue(instanceId, out List<Collider> ignoreSet);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled || InSet(ignoreSet, collider))
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

        public void OverlapDisk(ProjectileId instanceId, ProjectileId ignoreSetOwner, Vector3 center, float radius, List<HitInfo> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (radius <= 0f)
                return;

            const float halfHeight = 1f;
            Vector3 point0 = center + Vector3.up * halfHeight;
            Vector3 point1 = center - Vector3.up * halfHeight;
            int count = OverlapCapsule(point0, point1, radius);

            ignoreSetByInstance.TryGetValue(ignoreSetOwner, out List<Collider> ignoreSet);
            float radiusSq = radius * radius;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || !collider.enabled || InSet(ignoreSet, collider))
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
            Vector3 direction,
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
                RememberOrClearIgnore(instanceId, ignore, origin, direction, radius, castCount);
                hit = default;
                return false;
            }

            HitTargets.NoteHit(instanceId, publishedHit.TargetId, publishedHit.Point);
            pierceContactByInstance.Remove(instanceId);
            RememberOrClearIgnore(instanceId, ignore, origin, direction, radius, castCount);
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
            List<Collider> ignoreSet,
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
                if (collider == null || !collider.enabled || collider == ignore || InSet(ignoreSet, collider) || IsPresentation(collider))
                    continue;

                Vector3 point;
                Vector3 reported;
                float distSq;
                if (SupportsClosestPoint(collider))
                {
                    point = collider.ClosestPoint(origin);
                    reported = origin - point;
                    distSq = (point - origin).sqrMagnitude;
                }
                else if (TryBackedUpRay(collider, origin, incoming, radius, out RaycastHit surface))
                {
                    point = surface.point;
                    reported = surface.normal;
                    distSq = (point - origin).sqrMagnitude;
                }
                else
                {
                    point = origin;
                    reported = Vector3.zero;
                    distSq = radius * radius;
                }

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
            List<Collider> ignoreSet,
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
                if (collider == null || !collider.enabled || collider == ignore || InSet(ignoreSet, collider) || IsPresentation(collider))
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
                if (!collider.Raycast(back, out RaycastHit exit, far + Skin))
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

        void RememberOrClearIgnore(ProjectileId instanceId, Collider ignore, Vector3 origin, Vector3 direction, float radius, int castCount)
        {
            if (ignore == null)
            {
                ignoreColliderByInstance.Remove(instanceId);
                return;
            }

            if (StillTouching(instanceId, ignore, origin, direction, radius) || CastIncludes(castCount, ignore))
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

        List<Collider> RentSet(ProjectileId instanceId)
        {
            if (ignoreSetByInstance.TryGetValue(instanceId, out List<Collider> existing))
            {
                existing.Clear();
                return existing;
            }

            List<Collider> set = ignoreSetPool.Count > 0 ? ignoreSetPool.Pop() : new List<Collider>();
            ignoreSetByInstance[instanceId] = set;
            return set;
        }

        static bool InSet(List<Collider> set, Collider collider)
        {
            if (set == null)
                return false;

            for (int i = 0; i < set.Count; i++)
            {
                if (set[i] == collider)
                    return true;
            }

            return false;
        }

        static bool IsPresentation(Collider collider)
        {
            return collider.GetComponentInParent<ProjectileView>() != null;
        }

        bool StillTouching(ProjectileId instanceId, Collider collider, Vector3 origin, Vector3 direction, float radius)
        {
            return Overlaps(collider, origin, radius) || InsideConcavePierce(instanceId, collider, origin, direction, radius);
        }

        bool InsideConcavePierce(ProjectileId instanceId, Collider collider, Vector3 origin, Vector3 direction, float radius)
        {
            if (!concaveExitByInstance.TryGetValue(instanceId, out PierceContact exit))
                return false;

            float skin = radius + Skin;
            Vector3 dir = exit.Incoming.sqrMagnitude > 1e-12f ? exit.Incoming.normalized : Vector3.forward;
            bool headingOut = direction.sqrMagnitude <= 1e-12f || Vector3.Dot(direction, dir) > 0f;
            Bounds bounds = collider.bounds;
            bounds.Expand(skin * 2f);
            if (exit.Collider == collider
                && headingOut
                && Vector3.Dot(origin - exit.Point, dir) < skin
                && bounds.Contains(origin))
                return true;

            concaveExitByInstance.Remove(instanceId);
            return false;
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

            int count = OverlapSphere(origin, skin, 1 << collider.gameObject.layer, ref skinBuffer);
            for (int i = 0; i < count; i++)
            {
                if (skinBuffer[i] == collider)
                    return true;
            }

            return false;
        }

        static int OverlapSphere(Vector3 center, float radius, int mask, ref Collider[] buffer)
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(center, radius, buffer, mask, QueryTriggerInteraction.Ignore)) == buffer.Length
                   && Grow(ref buffer))
            {
            }

            return count;
        }

        int OverlapCapsule(Vector3 point0, Vector3 point1, float radius)
        {
            int count;
            while ((count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore)) == overlapBuffer.Length
                   && Grow(ref overlapBuffer))
            {
            }

            return count;
        }

        int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance)
        {
            int count;
            while ((count = Physics.SphereCastNonAlloc(origin, radius, direction, castBuffer, distance, ~0, QueryTriggerInteraction.Ignore)) == castBuffer.Length
                   && Grow(ref castBuffer))
            {
            }

            return count;
        }

        static bool Grow<T>(ref T[] buffer)
        {
            if (buffer.Length >= MaxBufferSize)
                return false;
            Array.Resize(ref buffer, Math.Min(buffer.Length * 2, MaxBufferSize));
            return true;
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
            return collider.Raycast(new Ray(origin - dir * backup, dir), out surface, backup * 2f);
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
