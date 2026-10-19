using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    sealed class HitTargetCatalog
    {
        readonly List<Volume> volumes = new List<Volume>();
        readonly Dictionary<TargetId, int> indexById = new Dictionary<TargetId, int>();
        readonly Dictionary<ProjectileId, IgnoredContact> ignored = new Dictionary<ProjectileId, IgnoredContact>();
        readonly HashSet<ProjectileId> holdRelease = new HashSet<ProjectileId>();
        readonly Dictionary<ProjectileId, ShooterSkip> shooters = new Dictionary<ProjectileId, ShooterSkip>();
        bool hasDeadSlots;

        public int Count => indexById.Count;

        public void Set(TargetId id, Vector3 position, float radius, ProtectionLevel protection = ProtectionLevel.None)
        {
            if (radius <= 0f)
            {
                Remove(id);
                return;
            }

            Store(new Volume(id, position, position, radius, position, protection));
        }

        public void SetCapsule(
            TargetId id,
            Vector3 bottom,
            float radius,
            float height,
            float homingHeight,
            ProtectionLevel protection = ProtectionLevel.None)
        {
            if (radius <= 0f)
            {
                Remove(id);
                return;
            }

            float total = Mathf.Max(height, 2f * radius);
            Vector3 low = bottom + Vector3.up * radius;
            Vector3 high = bottom + Vector3.up * (total - radius);
            Vector3 aim = bottom + Vector3.up * Mathf.Clamp(homingHeight, 0f, total);
            Store(new Volume(id, low, high, radius, aim, protection));
        }

        void Store(Volume volume)
        {
            TargetId id = volume.Id;
            if (indexById.TryGetValue(id, out int index))
            {
                volumes[index] = volume;
                return;
            }

            indexById.Add(id, volumes.Count);
            volumes.Add(volume);
        }

        public bool TryGet(int index, out TargetId id, out Vector3 position)
        {
            Compact();
            if (index < 0 || index >= volumes.Count)
            {
                id = default;
                position = default;
                return false;
            }

            Volume volume = volumes[index];
            id = volume.Id;
            position = volume.Aim;
            return true;
        }

        public bool TryGetPosition(TargetId id, out Vector3 position)
        {
            if (!TryFind(id, out Volume volume))
            {
                position = default;
                return false;
            }

            position = volume.Aim;
            return true;
        }

        public bool Contains(TargetId id)
        {
            return TryFind(id, out _);
        }

        public void Remove(TargetId id)
        {
            if (indexById.Remove(id))
                hasDeadSlots = true;
        }

        public void HoldIgnore(ProjectileId instanceId)
        {
            holdRelease.Add(instanceId);
        }

        public void SetShooter(ProjectileId instanceId, TargetId id)
        {
            shooters[instanceId] = new ShooterSkip(id, active: true);
        }

        public bool TryGetShooter(ProjectileId instanceId, out TargetId id)
        {
            bool found = shooters.TryGetValue(instanceId, out ShooterSkip shooter);
            id = shooter.Id;
            return found;
        }

        public void ReleaseShooter(ProjectileId instanceId)
        {
            shooters.Remove(instanceId);
        }

        public void ReleaseIfSeparated(ProjectileId instanceId, Vector3 origin, float radius)
        {
            if (shooters.TryGetValue(instanceId, out ShooterSkip shooter)
                && shooter.Active
                && (!TryFind(shooter.Id, out Volume shooterVolume) || !Overlaps(origin, radius, shooterVolume, skin: 0.02f)))
                shooters[instanceId] = new ShooterSkip(shooter.Id, active: false);

            if (holdRelease.Remove(instanceId))
                return;

            if (!ignored.TryGetValue(instanceId, out IgnoredContact contact))
                return;

            if (!TryFind(contact.Id, out Volume volume) || !Overlaps(origin, radius, volume, skin: 0.02f))
                ignored.Remove(instanceId);
        }

        public void NoteHit(ProjectileId instanceId, TargetId id, Vector3 point)
        {
            ignored[instanceId] = new IgnoredContact(id, point);
        }

        public bool TryPierceExit(ProjectileId instanceId, Vector3 travel, out Vector3 surface)
        {
            surface = default;
            if (!ignored.TryGetValue(instanceId, out IgnoredContact contact))
                return false;
            if (!TryFind(contact.Id, out Volume volume))
                return false;

            Vector3 dir = travel.sqrMagnitude > 1e-12f ? travel.normalized : Vector3.forward;
            surface = ClosestOnAxis(volume, contact.Point) + dir * volume.Radius;
            return true;
        }

        public void ClearIgnore(ProjectileId instanceId)
        {
            ignored.Remove(instanceId);
            holdRelease.Remove(instanceId);
            if (shooters.TryGetValue(instanceId, out ShooterSkip shooter))
                shooters[instanceId] = new ShooterSkip(shooter.Id, active: true);
        }

        public void CopyIgnore(ProjectileId from, ProjectileId to)
        {
            if (from == to)
                return;
            if (shooters.TryGetValue(from, out ShooterSkip shooter))
                shooters[to] = shooter;
            if (!ignored.TryGetValue(from, out IgnoredContact contact))
                return;

            ignored[to] = contact;
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
            Compact();
            hit = default;
            travel = 0f;
            closeness = 0f;
            bool found = false;
            bool hasIgnore = ignored.TryGetValue(instanceId, out IgnoredContact ignoredContact);
            TargetId ignoredId = ignoredContact.Id;
            bool hasShooter = shooters.TryGetValue(instanceId, out ShooterSkip shooter) && shooter.Active;

            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                if (hasIgnore && volume.Id.Equals(ignoredId))
                    continue;
                if (hasShooter && volume.Id.Equals(shooter.Id))
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
                    Protection = volume.Protection,
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

            Compact();
            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                if (volume.Protection == ProtectionLevel.Strong)
                    continue;

                Vector3 axis = ClosestOnAxis(volume, center);
                Vector3 delta = axis - center;
                float limit = radius + volume.Radius;
                if (delta.sqrMagnitude > limit * limit)
                    continue;

                results.Add(BuildOverlap(instanceId, center, volume, axis, delta));
            }
        }

        public void AppendDisk(ProjectileId instanceId, Vector3 center, float radius, List<HitInfo> results)
        {
            if (radius <= 0f || results == null)
                return;

            Compact();
            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                if (volume.Protection == ProtectionLevel.Strong)
                    continue;

                Vector3 axis = ClosestOnAxis(volume, center);
                float dx = axis.x - center.x;
                float dz = axis.z - center.z;
                float limit = radius + volume.Radius;
                if (dx * dx + dz * dz > limit * limit)
                    continue;

                Vector3 delta = new Vector3(dx, 0f, dz);
                results.Add(BuildOverlap(instanceId, center, volume, axis, delta));
            }
        }

        bool TryFind(TargetId id, out Volume volume)
        {
            if (!indexById.TryGetValue(id, out int index))
            {
                volume = default;
                return false;
            }

            volume = volumes[index];
            return true;
        }

        void Compact()
        {
            if (!hasDeadSlots)
                return;

            int write = 0;
            for (int read = 0; read < volumes.Count; read++)
            {
                Volume volume = volumes[read];
                // A slot is live only while the index points at it; a removed and re-set id owns a later slot.
                if (!indexById.TryGetValue(volume.Id, out int index) || index != read)
                    continue;

                if (write != read)
                {
                    volumes[write] = volume;
                    indexById[volume.Id] = write;
                }

                write++;
            }

            volumes.RemoveRange(write, volumes.Count - write);
            hasDeadSlots = false;
        }

        static HitInfo BuildOverlap(ProjectileId instanceId, Vector3 center, Volume volume, Vector3 axis, Vector3 delta)
        {
            Vector3 normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector3.up;
            Vector3 point = axis - normal * volume.Radius;
            if ((point - center).sqrMagnitude > (axis - center).sqrMagnitude)
                point = axis;

            return new HitInfo
            {
                InstanceId = instanceId,
                Kind = HitKind.Target,
                Protection = volume.Protection,
                TargetId = volume.Id,
                Point = point,
                Normal = normal
            };
        }

        static bool Overlaps(Vector3 origin, float radius, Volume volume, float skin)
        {
            float limit = radius + volume.Radius + skin;
            return (ClosestOnAxis(volume, origin) - origin).sqrMagnitude <= limit * limit;
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
            normal = Vector3.up;

            Vector3 axis = ClosestOnAxis(volume, origin);
            point = axis;
            Vector3 offset = axis - origin;
            float combined = projectileRadius + volume.Radius;
            float combinedSq = combined * combined;
            if (offset.sqrMagnitude <= combinedSq)
            {
                closeness = ContactDistanceSq(origin, axis, volume.Radius, offset);
                normal = RadialNormal(origin, axis, direction);
                point = axis + normal * volume.Radius;
                return true;
            }

            if (maxDistance <= 1e-8f || direction.sqrMagnitude <= 1e-12f)
                return false;

            Vector3 dir = direction.normalized;
            if (!TryEnterCapsule(origin, dir, volume.Low, volume.High, combined, out float t) || t > maxDistance)
                return false;

            travel = t;
            closeness = t;
            Vector3 at = origin + dir * t;
            axis = ClosestOnAxis(volume, at);
            normal = RadialNormal(at, axis, dir);
            point = axis + normal * volume.Radius;
            return true;
        }

        // Origin is outside the capsule; the first entry is the earliest of the side wall and the two end spheres.
        static bool TryEnterCapsule(Vector3 origin, Vector3 dir, Vector3 low, Vector3 high, float radius, out float travel)
        {
            travel = float.PositiveInfinity;
            Vector3 axis = high - low;
            float axisSq = axis.sqrMagnitude;
            if (axisSq > 1e-12f)
            {
                Vector3 fromLow = origin - low;
                float along = Vector3.Dot(axis, dir);
                float a = axisSq - along * along;
                if (a > 1e-10f)
                {
                    float axisOffset = Vector3.Dot(axis, fromLow);
                    float b = axisSq * Vector3.Dot(dir, fromLow) - axisOffset * along;
                    float c = axisSq * fromLow.sqrMagnitude - axisOffset * axisOffset - radius * radius * axisSq;
                    float disc = b * b - a * c;
                    if (disc >= 0f)
                    {
                        float t = (-b - Mathf.Sqrt(disc)) / a;
                        float y = axisOffset + t * along;
                        if (t >= 0f && y > 0f && y < axisSq)
                            travel = t;
                    }
                }

                if (TryEnterSphere(origin, dir, high, radius, out float top) && top < travel)
                    travel = top;
            }

            if (TryEnterSphere(origin, dir, low, radius, out float bottom) && bottom < travel)
                travel = bottom;

            return !float.IsPositiveInfinity(travel);
        }

        static bool TryEnterSphere(Vector3 origin, Vector3 dir, Vector3 center, float radius, out float travel)
        {
            Vector3 fromCenter = origin - center;
            float b = Vector3.Dot(fromCenter, dir);
            float c = fromCenter.sqrMagnitude - radius * radius;
            float disc = b * b - c;
            travel = disc < 0f ? -1f : -b - Mathf.Sqrt(disc);
            return travel >= 0f;
        }

        static Vector3 ClosestOnAxis(Volume volume, Vector3 point)
        {
            Vector3 axis = volume.High - volume.Low;
            float axisSq = axis.sqrMagnitude;
            if (axisSq <= 1e-12f)
                return volume.Low;

            float t = Mathf.Clamp01(Vector3.Dot(point - volume.Low, axis) / axisSq);
            return volume.Low + axis * t;
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

        readonly struct IgnoredContact
        {
            public IgnoredContact(TargetId id, Vector3 point)
            {
                Id = id;
                Point = point;
            }

            public TargetId Id { get; }
            public Vector3 Point { get; }
        }

        readonly struct ShooterSkip
        {
            public ShooterSkip(TargetId id, bool active)
            {
                Id = id;
                Active = active;
            }

            public TargetId Id { get; }
            public bool Active { get; }
        }

        // A sphere is a capsule whose Low and High are the same point.
        struct Volume
        {
            public Volume(TargetId id, Vector3 low, Vector3 high, float radius, Vector3 aim, ProtectionLevel protection)
            {
                Id = id;
                Low = low;
                High = high;
                Radius = radius;
                Aim = aim;
                Protection = protection;
            }

            public TargetId Id { get; }
            public Vector3 Low { get; }
            public Vector3 High { get; }
            public float Radius { get; }
            public Vector3 Aim { get; }
            public ProtectionLevel Protection { get; }
        }
    }
}
