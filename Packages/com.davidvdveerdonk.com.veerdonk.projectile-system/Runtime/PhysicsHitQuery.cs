using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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

            if (TryPickOverlap(instanceId, origin, -direction, overlapCount, ignore, out hit, out Collider hitCollider))
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

            Vector3 dir = direction.normalized;
            int castCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                dir,
                castBuffer,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (TryPickCast(instanceId, dir, castCount, ignore, out hit, out hitCollider))
            {
                ignoreColliderByInstance[instanceId] = hitCollider;
                return true;
            }

            RememberOrClearIgnore(instanceId, ignore, origin, radius, castCount);
            return false;
        }

        bool TryPickOverlap(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 fallbackNormal,
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
                hit = BuildHit(instanceId, collider, point, origin - point, fallbackNormal);
                hitCollider = collider;
                found = true;
            }

            return found;
        }

        bool TryPickCast(
            ProjectileId instanceId,
            Vector3 fallbackNormal,
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
                hit = BuildHit(instanceId, collider, rayHit.point, rayHit.normal, fallbackNormal);
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

        static HitInfo BuildHit(ProjectileId instanceId, Collider collider, Vector3 point, Vector3 normal, Vector3 fallbackNormal)
        {
            if (normal.sqrMagnitude < 1e-10f)
                normal = fallbackNormal.sqrMagnitude < 1e-10f ? Vector3.up : fallbackNormal;

            var info = new HitInfo
            {
                InstanceId = instanceId,
                Point = point,
                Normal = normal.normalized
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

            return info;
        }
    }
}
