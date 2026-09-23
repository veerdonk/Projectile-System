using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class HitTargetCatalog
    {
        readonly List<Volume> volumes = new List<Volume>();
        readonly Dictionary<ProjectileId, TargetId> ignored = new Dictionary<ProjectileId, TargetId>();

        public void Set(TargetId id, Vector3 position, float radius)
        {
            if (radius <= 0f)
            {
                Remove(id);
                return;
            }

            for (int i = 0; i < volumes.Count; i++)
            {
                if (!volumes[i].Id.Equals(id))
                    continue;

                volumes[i] = new Volume(id, position, radius);
                return;
            }

            volumes.Add(new Volume(id, position, radius));
        }

        public void Remove(TargetId id)
        {
            for (int i = volumes.Count - 1; i >= 0; i--)
            {
                if (volumes[i].Id.Equals(id))
                    volumes.RemoveAt(i);
            }
        }

        public void ReleaseIfSeparated(ProjectileId instanceId, Vector3 origin, float radius)
        {
            if (!ignored.TryGetValue(instanceId, out TargetId id))
                return;

            if (!TryFind(id, out Volume volume) || !Overlaps(origin, radius, volume, skin: 0.02f))
                ignored.Remove(instanceId);
        }

        public void NoteHit(ProjectileId instanceId, TargetId id)
        {
            ignored[instanceId] = id;
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            if (from == to)
                return;
            if (!ignored.TryGetValue(from, out TargetId id))
                return;

            ignored[to] = id;
        }

        public bool TrySweep(
            ProjectileId instanceId,
            Vector3 origin,
            Vector3 direction,
            float distance,
            float radius,
            out HitInfo hit,
            out float travel,
            out float closeness)
        {
            hit = default;
            travel = 0f;
            closeness = 0f;
            bool found = false;
            bool hasIgnore = ignored.TryGetValue(instanceId, out TargetId ignoredId);

            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                if (hasIgnore && volume.Id.Equals(ignoredId))
                    continue;
                if (!Sweep(origin, direction, distance, radius, volume, out float volumeTravel, out float volumeClose, out Vector3 point, out Vector3 normal))
                    continue;
                if (found && !IsNearer(volumeTravel, volumeClose, travel, closeness))
                    continue;

                found = true;
                travel = volumeTravel;
                closeness = volumeClose;
                hit = new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Protection = ProtectionLevel.None,
                    TargetId = volume.Id,
                    Point = point,
                    Normal = normal
                };
            }

            return found;
        }

        public void AppendSphere(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            if (radius <= 0f || results == null)
                return;

            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                Vector3 delta = volume.Position - center;
                float limit = radius + volume.Radius;
                if (delta.sqrMagnitude > limit * limit)
                    continue;

                results.Add(BuildOverlap(instanceId, center, volume, delta));
            }
        }

        public void AppendDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            if (radius <= 0f || results == null)
                return;

            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                float dx = volume.Position.x - center.x;
                float dz = volume.Position.z - center.z;
                float limit = radius + volume.Radius;
                if (dx * dx + dz * dz > limit * limit)
                    continue;

                Vector3 delta = new Vector3(dx, 0f, dz);
                results.Add(BuildOverlap(instanceId, center, volume, delta));
            }
        }

        bool TryFind(TargetId id, out Volume volume)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                if (!volumes[i].Id.Equals(id))
                    continue;

                volume = volumes[i];
                return true;
            }

            volume = default;
            return false;
        }

        static HitInfo BuildOverlap(ProjectileId instanceId, Vector3 center, Volume volume, Vector3 delta)
        {
            Vector3 normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up;
            Vector3 point = volume.Position - normal * volume.Radius;
            if ((point - center).sqrMagnitude > (volume.Position - center).sqrMagnitude)
                point = volume.Position;

            return new HitInfo
            {
                InstanceId = instanceId,
                Kind = HitKind.Target,
                Protection = ProtectionLevel.None,
                TargetId = volume.Id,
                Point = point,
                Normal = normal
            };
        }

        static bool Overlaps(Vector3 origin, float radius, Volume volume, float skin)
        {
            float limit = radius + volume.Radius + skin;
            return (volume.Position - origin).sqrMagnitude <= limit * limit;
        }

        static bool Sweep(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            float projectileRadius,
            Volume volume,
            out float travel,
            out float closeness,
            out Vector3 point,
            out Vector3 normal)
        {
            travel = 0f;
            closeness = 0f;
            point = volume.Position;
            normal = Vector3.up;

            Vector3 offset = volume.Position - origin;
            float combined = projectileRadius + volume.Radius;
            float distSq = offset.sqrMagnitude;
            float combinedSq = combined * combined;
            if (distSq <= combinedSq)
            {
                closeness = ContactDistanceSq(origin, volume.Position, volume.Radius, offset);
                normal = RadialNormal(origin, volume.Position, direction);
                point = volume.Position + normal * volume.Radius;
                return true;
            }

            if (maxDistance <= 1e-8f || direction.sqrMagnitude <= 1e-12f)
                return false;

            Vector3 dir = direction.normalized;
            Vector3 fromCenter = origin - volume.Position;
            float b = Vector3.Dot(fromCenter, dir);
            float c = distSq - combinedSq;
            float disc = b * b - c;
            if (disc < 0f)
                return false;

            float t = -b - Mathf.Sqrt(disc);
            if (t < 0f || t > maxDistance)
                return false;

            travel = t;
            closeness = t;
            Vector3 at = origin + dir * t;
            normal = RadialNormal(at, volume.Position, dir);
            point = volume.Position + normal * volume.Radius;
            return true;
        }

        static float ContactDistanceSq(Vector3 origin, Vector3 center, float targetRadius, Vector3 offset)
        {
            Vector3 normal = offset.sqrMagnitude > 1e-12f ? (origin - center).normalized : Vector3.back;
            Vector3 point = center + normal * targetRadius;
            return (point - origin).sqrMagnitude;
        }

        static Vector3 RadialNormal(Vector3 from, Vector3 center, Vector3 direction)
        {
            Vector3 outward = from - center;
            if (outward.sqrMagnitude > 1e-12f)
                return outward.normalized;
            if (direction.sqrMagnitude > 1e-12f)
                return -direction.normalized;
            return Vector3.back;
        }

        static bool IsNearer(float travel, float closeness, float bestTravel, float bestCloseness)
        {
            if (travel < bestTravel - 1e-4f)
                return true;
            if (travel > bestTravel + 1e-4f)
                return false;
            return closeness < bestCloseness;
        }

        struct Volume
        {
            public Volume(TargetId id, Vector3 position, float radius)
            {
                Id = id;
                Position = position;
                Radius = radius;
            }

            public TargetId Id { get; }
            public Vector3 Position { get; }
            public float Radius { get; }
        }
    }
}
