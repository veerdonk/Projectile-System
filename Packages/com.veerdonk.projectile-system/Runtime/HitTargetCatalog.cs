using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    // Queries test volumes in list order whatever the broad phase picks, so the nearest-hit tie-break and the
    // order of overlap results never depend on the grid (ADR-063).
    sealed class HitTargetCatalog
    {
        // A power of two, so a cell index is the same in float and double.
        const float CellSize = 2f;
        const float MaxCellCoordinate = 1e6f;
        const long OversizeCell = long.MaxValue;
        const long NoCell = long.MinValue;

        readonly List<Volume> volumes = new List<Volume>();
        readonly List<CellLink> links = new List<CellLink>();
        readonly Dictionary<TargetId, int> indexById = new Dictionary<TargetId, int>();
        readonly Dictionary<long, int> cellHeads = new Dictionary<long, int>();
        readonly Dictionary<ProjectileId, IgnoredContact> ignored = new Dictionary<ProjectileId, IgnoredContact>();
        readonly HashSet<ProjectileId> holdRelease = new HashSet<ProjectileId>();
        readonly Dictionary<ProjectileId, ShooterSkip> shooters = new Dictionary<ProjectileId, ShooterSkip>();
        readonly bool broadPhase;
        int[] candidates = new int[64];
        int candidateCount;
        float maxExtent;
        int deadSlots;

        public HitTargetCatalog(bool broadPhase = true)
        {
            this.broadPhase = broadPhase;
        }

        public int Count => indexById.Count;

        internal int SlotCount => volumes.Count;

        // Bumps when an id is added or removed, not when a published id moves.
        internal int MembershipVersion { get; private set; }

        internal int NarrowPhaseTests { get; private set; }

        public void Set(TargetId id, Vector3 position, float radius, ProtectionLevel protection = ProtectionLevel.None)
        {
            if (!PulseClock.IsUsableRadius(radius))
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
            if (!PulseClock.IsUsableRadius(radius))
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
            long cell = CellOf(volume, out float extent);
            if (indexById.TryGetValue(id, out int index))
            {
                volumes[index] = volume;
                if (links[index].Cell != cell)
                {
                    Unlink(index);
                    Link(index, cell);
                }
            }
            else
            {
                if (deadSlots > indexById.Count)
                    Compact();

                index = volumes.Count;
                indexById.Add(id, index);
                volumes.Add(volume);
                links.Add(CellLink.Unlinked);
                Link(index, cell);
                MembershipVersion++;
            }

            if (cell != OversizeCell && extent > maxExtent)
                maxExtent = extent;
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
            if (!indexById.TryGetValue(id, out int index))
                return;

            indexById.Remove(id);
            Unlink(index);
            deadSlots++;
            MembershipVersion++;
        }

        // Compacts once so AimAt reads slots 0..Count-1 in TryGet order without a check per read.
        internal int PrepareScan()
        {
            Compact();
            return volumes.Count;
        }

        internal void AimAt(int slot, out TargetId id, out Vector3 position)
        {
            Volume volume = volumes[slot];
            id = volume.Id;
            position = volume.Aim;
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

        public void Release(ProjectileId instanceId)
        {
            shooters.Remove(instanceId);
            ignored.Remove(instanceId);
            holdRelease.Remove(instanceId);
        }

        public void ReleaseIfSeparated(ProjectileId instanceId, Vector3 origin, float radius)
        {
            if (shooters.TryGetValue(instanceId, out ShooterSkip shooter)
                && shooter.Active
                && TryFind(shooter.Id, out Volume shooterVolume)
                && !Overlaps(origin, radius, shooterVolume, skin: 0.02f))
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

            GatherSweep(origin, direction, distance, radius);
            for (int c = 0; c < candidateCount; c++)
            {
                Volume volume = volumes[candidates[c]];
                NarrowPhaseTests++;
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
            if (!PulseClock.IsUsableRadius(radius) || results == null)
                return;

            Compact();
            if (!GatherCells(center.x, center.z, center.x, center.z, radius))
                GatherAll();

            for (int c = 0; c < candidateCount; c++)
            {
                Volume volume = volumes[candidates[c]];
                NarrowPhaseTests++;
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

        public void AppendDisk(ProjectileId instanceId, in GroundDisk disk, List<HitInfo> results)
        {
            if (!PulseClock.IsUsableRadius(disk.Radius) || results == null)
                return;

            Compact();
            Vector3 center = disk.Center;
            if (!TryDiskReach(disk, out float reach) || !GatherCells(center.x, center.z, center.x, center.z, reach))
                GatherAll();

            for (int c = 0; c < candidateCount; c++)
            {
                Volume volume = volumes[candidates[c]];
                NarrowPhaseTests++;
                if (volume.Protection == ProtectionLevel.Strong)
                    continue;

                if (!disk.Contains(volume.Low, volume.Low, volume.Radius, out Vector3 delta))
                    continue;

                Vector3 axis = ClosestOnAxis(volume, center);
                results.Add(BuildOverlap(instanceId, center, volume, axis, delta, disk.Normal));
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
            if (deadSlots == 0)
                return;

            int write = 0;
            float liveExtent = 0f;
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
                    MoveLink(read, write);
                }

                if (CellOf(volume, out float extent) != OversizeCell && extent > liveExtent)
                    liveExtent = extent;
                write++;
            }

            volumes.RemoveRange(write, volumes.Count - write);
            links.RemoveRange(write, links.Count - write);
            maxExtent = liveExtent;
            deadSlots = 0;
        }

        void GatherAll()
        {
            candidateCount = 0;
            for (int i = 0; i < volumes.Count; i++)
                AddCandidate(i);
        }

        void GatherSweep(Vector3 origin, Vector3 direction, float distance, float radius)
        {
            if (!IsFinite(origin) || !IsFinite(direction) || !float.IsFinite(distance) || !float.IsFinite(radius))
            {
                GatherAll();
                return;
            }

            Vector3 end = origin;
            if (distance > 1e-8f && direction.sqrMagnitude > 1e-12f)
                end = origin + direction.normalized * distance;

            // The narrow phase squares the radius sum, so a negative shot radius reaches as far as its size.
            if (!GatherCells(origin.x, origin.z, end.x, end.z, Mathf.Abs(radius)))
                GatherAll();
        }

        // Fills candidates, in slot order, with every volume whose XZ centre is within reach plus the largest stored
        // extent of the XZ segment a-b. Each row of cells takes the cells around the part of the segment within reach
        // of that row. False (and the caller scans every volume) when the inputs are unusable or the walk would visit
        // more cells than there are volumes.
        bool GatherCells(float ax, float az, float bx, float bz, float reach)
        {
            candidateCount = 0;
            if (!broadPhase)
                return false;

            double maxAbs = Math.Max(Math.Max(Math.Abs((double)ax), Math.Abs((double)az)), Math.Max(Math.Abs((double)bx), Math.Abs((double)bz)));
            double grow = (double)reach + maxExtent;
            // Covers float rounding in the narrow phase and in the stored centres and extents.
            grow += 1e-3 + 1e-5 * (maxAbs + grow);
            if (!(maxAbs + grow < MaxCellCoordinate))
                return false;

            double dx = (double)bx - ax;
            double dz = (double)bz - az;
            int rowFirst = (int)Math.Floor((Math.Min(az, bz) - grow) / CellSize);
            int rowLast = (int)Math.Floor((Math.Max(az, bz) + grow) / CellSize);
            long budget = indexById.Count;
            long visited = 0;
            for (int row = rowFirst; row <= rowLast; row++)
            {
                double low = row * (double)CellSize - grow;
                double high = (row + 1) * (double)CellSize + grow;
                double t0 = 0d;
                double t1 = 1d;
                if (Math.Abs(dz) > 1e-12)
                {
                    double ta = (low - az) / dz;
                    double tb = (high - az) / dz;
                    t0 = Math.Max(0d, Math.Min(ta, tb));
                    t1 = Math.Min(1d, Math.Max(ta, tb));
                    if (t0 > t1)
                        continue;
                }
                else if (az < low || az > high)
                {
                    continue;
                }

                double x0 = ax + dx * t0;
                double x1 = ax + dx * t1;
                int columnFirst = (int)Math.Floor((Math.Min(x0, x1) - grow) / CellSize);
                int columnLast = (int)Math.Floor((Math.Max(x0, x1) + grow) / CellSize);
                visited += columnLast - columnFirst + 1L;
                if (visited > budget)
                    return false;

                for (int column = columnFirst; column <= columnLast; column++)
                    AppendCell(PackCell(column, row));
            }

            AppendCell(OversizeCell);
            SortCandidates();
            return true;
        }

        void AppendCell(long cell)
        {
            if (!cellHeads.TryGetValue(cell, out int slot))
                return;

            while (slot >= 0)
            {
                AddCandidate(slot);
                slot = links[slot].Next;
            }
        }

        void AddCandidate(int slot)
        {
            if (candidateCount == candidates.Length)
                Array.Resize(ref candidates, candidates.Length * 2);
            candidates[candidateCount++] = slot;
        }

        // List<int>.Sort allocates under Unity's Mono; insertion sort for a handful, heapsort beyond.
        void SortCandidates()
        {
            int[] items = candidates;
            int count = candidateCount;
            if (count <= 16)
            {
                for (int i = 1; i < count; i++)
                {
                    int value = items[i];
                    int j = i - 1;
                    while (j >= 0 && items[j] > value)
                    {
                        items[j + 1] = items[j];
                        j--;
                    }

                    items[j + 1] = value;
                }

                return;
            }

            for (int start = count / 2 - 1; start >= 0; start--)
                SiftDown(items, start, count);

            for (int end = count - 1; end > 0; end--)
            {
                int top = items[0];
                items[0] = items[end];
                items[end] = top;
                SiftDown(items, 0, end);
            }
        }

        static void SiftDown(int[] items, int root, int count)
        {
            int value = items[root];
            while (true)
            {
                int child = root * 2 + 1;
                if (child >= count)
                    break;
                if (child + 1 < count && items[child + 1] > items[child])
                    child++;
                if (items[child] <= value)
                    break;

                items[root] = items[child];
                root = child;
            }

            items[root] = value;
        }

        // Contains is given the volume's foot and radius; the in-plane offset to a contained foot is at most the
        // shape's reach plus that radius, and its XZ part (exact on the vertical line) no longer.
        static bool TryDiskReach(in GroundDisk disk, out float reach)
        {
            reach = disk.Radius * 1.01f;
            if (!IsFinite(disk.Center))
                return false;
            if (disk.Shape == GroundFieldShape.Disk)
                return true;

            Vector3 normal = disk.Normal;
            Vector3 axis = disk.Axis;
            Vector3 side = disk.Side;
            return IsFinite(normal)
                && normal.y >= 0.01f
                && disk.Length >= 0f
                && disk.Width >= 0f
                && Mathf.Abs(axis.sqrMagnitude - 1f) <= 1e-3f
                && Mathf.Abs(side.sqrMagnitude - 1f) <= 1e-3f
                && Mathf.Abs(Vector3.Dot(axis, side)) <= 1e-3f
                && Mathf.Abs(Vector3.Dot(axis, normal)) <= 1e-3f * normal.magnitude
                && Mathf.Abs(Vector3.Dot(side, normal)) <= 1e-3f * normal.magnitude;
        }

        // The cell of the XZ midpoint of the axis; extent is the volume's radius plus half the axis's XZ length.
        static long CellOf(in Volume volume, out float extent)
        {
            Vector3 low = volume.Low;
            Vector3 high = volume.High;
            float halfX = (high.x - low.x) * 0.5f;
            float halfZ = (high.z - low.z) * 0.5f;
            extent = volume.Radius + Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
            float midX = (low.x + high.x) * 0.5f;
            float midZ = (low.z + high.z) * 0.5f;
            if (!(extent <= CellSize) || !(Mathf.Abs(midX) < MaxCellCoordinate) || !(Mathf.Abs(midZ) < MaxCellCoordinate))
                return OversizeCell;

            return PackCell(Mathf.FloorToInt(midX / CellSize), Mathf.FloorToInt(midZ / CellSize));
        }

        static long PackCell(int column, int row)
        {
            return ((long)column << 32) | (uint)row;
        }

        void Link(int slot, long cell)
        {
            int head = cellHeads.TryGetValue(cell, out int first) ? first : -1;
            links[slot] = new CellLink(cell, -1, head);
            if (head >= 0)
                SetPrev(head, slot);
            cellHeads[cell] = slot;
        }

        void Unlink(int slot)
        {
            CellLink link = links[slot];
            if (link.Prev >= 0)
                SetNext(link.Prev, link.Next);
            else if (link.Next >= 0)
                cellHeads[link.Cell] = link.Next;
            else
                cellHeads.Remove(link.Cell);

            if (link.Next >= 0)
                SetPrev(link.Next, link.Prev);
            links[slot] = CellLink.Unlinked;
        }

        void MoveLink(int from, int to)
        {
            CellLink link = links[from];
            links[to] = link;
            links[from] = CellLink.Unlinked;
            if (link.Prev >= 0)
                SetNext(link.Prev, to);
            else
                cellHeads[link.Cell] = to;

            if (link.Next >= 0)
                SetPrev(link.Next, to);
        }

        void SetNext(int slot, int next)
        {
            CellLink link = links[slot];
            links[slot] = new CellLink(link.Cell, link.Prev, next);
        }

        void SetPrev(int slot, int prev)
        {
            CellLink link = links[slot];
            links[slot] = new CellLink(link.Cell, prev, link.Next);
        }

        static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }

        static HitInfo BuildOverlap(ProjectileId instanceId, Vector3 center, Volume volume, Vector3 axis, Vector3 delta)
        {
            return BuildOverlap(instanceId, center, volume, axis, delta, Vector3.up);
        }

        static HitInfo BuildOverlap(ProjectileId instanceId, Vector3 center, Volume volume, Vector3 axis, Vector3 delta, Vector3 fallbackNormal)
        {
            Vector3 normal = delta.sqrMagnitude > 1e-10f ? delta.normalized : fallbackNormal;
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

        // A slot's place in its cell's doubly linked list; Prev and Next are slot indices, -1 at the ends.
        readonly struct CellLink
        {
            public static readonly CellLink Unlinked = new CellLink(NoCell, -1, -1);

            public CellLink(long cell, int prev, int next)
            {
                Cell = cell;
                Prev = prev;
                Next = next;
            }

            public long Cell { get; }
            public int Prev { get; }
            public int Next { get; }
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
