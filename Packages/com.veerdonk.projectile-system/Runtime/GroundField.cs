using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Outline of a planted <see cref="GroundField"/> in its ground plane (ADR-062).</summary>
    public enum GroundFieldShape
    {
        /// <summary>A disk of <see cref="GroundField.Radius"/> around the plant point. The default.</summary>
        Disk,
        /// <summary>
        /// A rectangle that starts at the plant point and runs <see cref="GroundField.Length"/> along the direction,
        /// <see cref="GroundField.Width"/> wide and centred on that line.
        /// </summary>
        Rectangle,
        /// <summary>
        /// A circular sector (cone) with its apex at the plant point, radius <see cref="GroundField.Length"/>, and a total
        /// angle of <see cref="GroundField.AngleDegrees"/> centred on the direction.
        /// </summary>
        Sector
    }

    /// <summary>
    /// Plants a lingering disk, rectangle, or sector (<see cref="Shape"/>). Size, lifetime, damage, optional status, and pulse
    /// period live on this modifier. OnHit uses the contact point; OnExpire / OnTravel use the
    /// instance position. The field lies on the ground that <see cref="IProjectileWorld.GroundFieldSurface"/>
    /// returns, or on the world ground plane (Y = 0, normal up) without one.
    /// A rectangle or sector points along the instance's horizontal travel when the stage starts (its spawn or aim direction
    /// when that travel is zero or vertical; world forward, +Z, when the aim is vertical too).
    /// <see cref="Occupancy"/> replaces pulses with enter/exit while the field lives.
    /// The modifier queues a plant; it does not call Physics or spawn a Recipe.
    /// A size that is &lt;= 0 or not finite (see <see cref="Shape"/>), or lifetime &lt;= 0 or NaN, is a no-op.
    /// Interval &lt;= 0 or NaN never pulses when occupancy is false.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnTravel | RecipeStage.OnHit | RecipeStage.OnExpire)]
    public sealed class GroundField : IModifier
    {
        [SerializeField] float radius;
        [SerializeField] float lifetimeSeconds;
        [SerializeField] float damage;
        [SerializeReference] IStatusEffect status;
        [SerializeField] float intervalSeconds;
        [SerializeField] string presentationId;
        [SerializeField] bool occupancy;
        [SerializeField, Min(0f)] float heightMargin;
        [SerializeField] GroundFieldShape shape;
        [SerializeField] float length;
        [SerializeField] float width;
        [SerializeField] float angleDegrees;

        public GroundField()
        {
        }

        public GroundField(
            float radius,
            float lifetimeSeconds,
            float damage,
            IStatusEffect status = null,
            float intervalSeconds = 0f,
            string presentationId = null,
            bool occupancy = false,
            float heightMargin = 0f)
        {
            this.radius = radius;
            this.lifetimeSeconds = lifetimeSeconds;
            this.damage = damage;
            this.status = status;
            this.intervalSeconds = intervalSeconds;
            this.presentationId = presentationId;
            this.occupancy = occupancy;
            HeightMargin = heightMargin;
        }

        /// <summary>
        /// Disk radius in meters, measured in the ground plane. With <see cref="GroundFieldShape.Disk"/>, &lt;= 0, NaN, or
        /// infinite plants nothing. Rectangle and Sector fields ignore it.
        /// </summary>
        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        /// <summary>Seconds the field lives. Independent of the projectile. &lt;= 0 or NaN plants nothing; infinity lives until
        /// the world is disposed.</summary>
        public float LifetimeSeconds
        {
            get => lifetimeSeconds;
            set => lifetimeSeconds = value;
        }

        /// <summary>Area Hit damage per pulse.</summary>
        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        /// <summary>Optional status raised per Target per pulse.</summary>
        public IStatusEffect Status => status;

        /// <summary>Seconds between pulses after the plant pulse. &lt;= 0 or NaN never pulses; a positive value below 0.01 pulses
        /// every 0.01 s.</summary>
        public float IntervalSeconds
        {
            get => intervalSeconds;
            set => intervalSeconds = value;
        }

        /// <summary>
        /// Selects the GameObject view via <see cref="IGroundFieldViewFactory"/>.
        /// Unknown or empty ids use the primitive disk.
        /// </summary>
        public string PresentationId
        {
            get => presentationId;
            set => presentationId = value;
        }

        /// <summary>
        /// When true, a Target raises <see cref="HitSource.OccupancyEnter"/> once on enter (and status, if set)
        /// and <see cref="HitSource.OccupancyExit"/> with damage 0 on leave. Interval pulses are not raised.
        /// </summary>
        public bool Occupancy
        {
            get => occupancy;
            set => occupancy = value;
        }

        /// <summary>
        /// Meters a Target's foot (the bottom of its capsule axis) may lie above or below the field's plane, measured
        /// along the ground normal. 0, which existing assets read, does not test height (a collider still has to be found by
        /// the query capsule, about 1 m above and below the plane plus the slope's rise). A negative or non-finite value is stored as 0.
        /// </summary>
        public float HeightMargin
        {
            get => heightMargin;
            set => heightMargin = GroundDisk.ClampMargin(value);
        }

        /// <summary>
        /// Outline of the field. <see cref="GroundFieldShape.Disk"/> (the default, which existing assets read) uses
        /// <see cref="Radius"/>. <see cref="GroundFieldShape.Rectangle"/> needs <see cref="Length"/> and <see cref="Width"/>
        /// above 0 and finite. <see cref="GroundFieldShape.Sector"/> needs <see cref="Length"/> above 0 and finite and
        /// <see cref="AngleDegrees"/> above 0.
        /// </summary>
        public GroundFieldShape Shape
        {
            get => shape;
            set => shape = value;
        }

        /// <summary>Rectangle length, or sector radius, in meters along the ground plane from the plant point.</summary>
        public float Length
        {
            get => length;
            set => length = value;
        }

        /// <summary>Rectangle width in meters, centred on the direction. Disk and Sector fields ignore it.</summary>
        public float Width
        {
            get => width;
            set => width = value;
        }

        /// <summary>
        /// Sector total angle in degrees, centred on the direction. &lt;= 0 or NaN plants nothing; 360 or more is a full
        /// disk of <see cref="Length"/>. Disk and Rectangle fields ignore it.
        /// </summary>
        public float AngleDegrees
        {
            get => angleDegrees;
            set => angleDegrees = value;
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var size = new GroundFieldSize(Shape, Radius, Length, Width, AngleDegrees);
            if (!Plants(size, LifetimeSeconds))
                return;

            Vector3 center = context.HasHit ? context.Hit.Point : context.Position;
            context.RequestGroundField(
                center,
                context.TravelDirection,
                size,
                LifetimeSeconds,
                Damage,
                Status,
                IntervalSeconds,
                PresentationId,
                Occupancy,
                HeightMargin);
        }

        internal static bool Plants(in GroundFieldSize size, float lifetimeSeconds)
        {
            return size.IsUsable && lifetimeSeconds > 0f;
        }
    }

    /// <summary>Shape and size of a field as requested: the disk radius, or the rectangle or sector measures.</summary>
    readonly struct GroundFieldSize
    {
        public GroundFieldSize(GroundFieldShape shape, float radius, float length, float width, float angleDegrees)
        {
            Shape = shape;
            Radius = radius;
            Length = length;
            Width = width;
            AngleDegrees = angleDegrees;
        }

        public static GroundFieldSize Disk(float radius) => new GroundFieldSize(GroundFieldShape.Disk, radius, 0f, 0f, 0f);

        public GroundFieldShape Shape { get; }

        public float Radius { get; }

        public float Length { get; }

        public float Width { get; }

        public float AngleDegrees { get; }

        public bool IsUsable
        {
            get
            {
                switch (Shape)
                {
                    case GroundFieldShape.Disk:
                        return PulseClock.IsUsableRadius(Radius);
                    case GroundFieldShape.Rectangle:
                        return PulseClock.IsUsableRadius(Length) && PulseClock.IsUsableRadius(Width);
                    case GroundFieldShape.Sector:
                        return PulseClock.IsUsableRadius(Length) && AngleDegrees > 0f;
                    default:
                        return false;
                }
            }
        }

        /// <summary>Total angle used for a sector: the requested angle, capped at 360.</summary>
        public float ClampedAngleDegrees => Mathf.Min(AngleDegrees, 360f);

        /// <summary>
        /// Distance along the direction from the plant point to the point the ground is sampled at, and the radius of a circle
        /// around that point that covers the shape. A disk samples at its centre with its radius.
        /// </summary>
        public void SampleCircle(out float offset, out float radius)
        {
            switch (Shape)
            {
                case GroundFieldShape.Rectangle:
                    offset = Length * 0.5f;
                    radius = Mathf.Sqrt(offset * offset + Width * Width * 0.25f);
                    return;
                case GroundFieldShape.Sector:
                    offset = Length * 0.5f;
                    float halfAngle = ClampedAngleDegrees * 0.5f * Mathf.Deg2Rad;
                    radius = Length * Mathf.Sqrt(Mathf.Max(0.25f, 1.25f - Mathf.Cos(halfAngle)));
                    return;
                default:
                    offset = 0f;
                    radius = Radius;
                    return;
            }
        }
    }

    sealed class GroundFieldSet : IDisposable
    {
        readonly IHitQuery hitQuery;
        readonly bool createViews;
        readonly Transform poolParent;
        readonly IGroundFieldViewFactory viewFactory;
        readonly Action<HitInfo> hit;
        readonly Action<StatusInfo> statusApplied;
        readonly Action<GroundFieldInfo> planted;
        readonly Action<GroundFieldPulseInfo> pulsed;
        readonly Action<GroundFieldInfo> ended;
        readonly List<LiveField> fields = new List<LiveField>();
        readonly Dictionary<string, Stack<ProjectileView>> viewPools = new Dictionary<string, Stack<ProjectileView>>();
        readonly List<ProjectileView> allViews = new List<ProjectileView>();
        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<TargetId> seenTargets = new List<TargetId>();
        readonly GroundFieldShapeMeshes shapeMeshes = new GroundFieldShapeMeshes();
        int nextFieldId = 1;
        bool disposed;

        public IGroundFieldSurface Surface { get; set; }

        public GroundFieldSet(
            IHitQuery hitQuery,
            bool createViews,
            Transform poolParent,
            Action<HitInfo> hit,
            Action<StatusInfo> statusApplied,
            IGroundFieldViewFactory viewFactory = null,
            Action<GroundFieldInfo> planted = null,
            Action<GroundFieldPulseInfo> pulsed = null,
            Action<GroundFieldInfo> ended = null)
        {
            this.planted = planted;
            this.pulsed = pulsed;
            this.ended = ended;
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            this.createViews = createViews;
            this.poolParent = poolParent;
            this.viewFactory = createViews ? viewFactory ?? new PrimitiveGroundFieldViewFactory() : null;
            this.hit = hit;
            this.statusApplied = statusApplied;
        }

        public void Plant(ProjectileId sourceId, in GroundFieldPlantRequest request)
        {
            GroundFieldSize size = request.Size;
            if (!GroundField.Plants(size, request.LifetimeSeconds))
                return;

            Vector3 planar = GroundDisk.PlanarDirection(request.Direction);
            size.SampleCircle(out float sampleOffset, out float sampleRadius);
            Vector3 sample = request.Center + planar * sampleOffset;
            ResolveGround(sample, sampleRadius, out Vector3 sampleGround, out Vector3 normal);
            GroundDisk disk;
            if (size.Shape == GroundFieldShape.Disk)
            {
                disk = new GroundDisk(sampleGround, normal, size.Radius, request.HeightMargin, planar);
            }
            else
            {
                Vector3 center = GroundDisk.OnPlane(request.Center, sampleGround, normal);
                disk = GroundDisk.Shaped(center, normal, planar, size, request.HeightMargin);
            }
            int fieldNumber = nextFieldId++;
            var skipKey = new ProjectileId(-fieldNumber);
            hitQuery.CopyIgnoreSet(sourceId, skipKey);
            var field = new LiveField
            {
                FieldId = new GroundFieldId(fieldNumber),
                SkipKey = skipKey,
                SourceId = sourceId,
                Disk = disk,
                LifetimeSeconds = request.LifetimeSeconds,
                RemainingLifetime = request.LifetimeSeconds,
                IntervalSeconds = PulseClock.Clamp(request.IntervalSeconds),
                Damage = request.Damage,
                Status = request.Status,
                PresentationId = request.PresentationId,
                PlantedThisTick = true,
                Occupancy = request.Occupancy,
                Occupants = request.Occupancy ? new List<TargetId>() : null
            };
            if (createViews)
                field.View = RentView(disk, request.PresentationId);

            fields.Add(field);
            planted?.Invoke(ToInfo(field, GroundFieldEndReason.Expired));
            if (request.Occupancy)
                UpdateOccupancy(fields[fields.Count - 1]);
            else if (fields[fields.Count - 1].IntervalSeconds > 0f)
                Pulse(fields[fields.Count - 1]);
        }

        public void ClearPlantedThisTick()
        {
            for (int i = 0; i < fields.Count; i++)
            {
                LiveField field = fields[i];
                field.PlantedThisTick = false;
                fields[i] = field;
            }
        }

        public void Tick(float dt)
        {
            if (dt < 0f)
                dt = 0f;

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                LiveField field = fields[i];
                if (field.PlantedThisTick)
                    continue;

                field.RemainingLifetime -= dt;
                if (field.RemainingLifetime <= 0f)
                {
                    if (field.Occupancy && field.Occupants != null)
                    {
                        overlapResults.Clear();
                        OccupancyExchange.Exchange(
                            field.Occupants,
                            overlapResults,
                            seenTargets,
                            field.SourceId,
                            field.Damage,
                            0f,
                            field.Disk.Radius,
                            status: null,
                            hit,
                            statusApplied);
                    }

                    ended?.Invoke(ToInfo(field, GroundFieldEndReason.Expired));
                    hitQuery.ReleaseIgnoreSet(field.SkipKey);
                    ReturnView(field);
                    fields.RemoveAt(i);
                    continue;
                }

                if (field.Occupancy)
                {
                    fields[i] = field;
                    UpdateOccupancy(fields[i]);
                    continue;
                }

                if (!(field.IntervalSeconds > 0f))
                {
                    fields[i] = field;
                    continue;
                }

                float previousElapsed = field.ElapsedSeconds;
                field.ElapsedSeconds += dt;
                int fires = PulseClock.Crossings(previousElapsed, field.ElapsedSeconds, field.IntervalSeconds);
                field.ElapsedSeconds -= fires * field.IntervalSeconds;
                fields[i] = field;
                for (int f = 0; f < fires; f++)
                    Pulse(fields[i]);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            for (int i = 0; i < fields.Count; i++)
            {
                ended?.Invoke(ToInfo(fields[i], GroundFieldEndReason.Disposed));
                hitQuery.ReleaseIgnoreSet(fields[i].SkipKey);
            }

            for (int i = 0; i < allViews.Count; i++)
            {
                ProjectileView view = allViews[i];
                if (view == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(view.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
            }

            allViews.Clear();
            viewPools.Clear();
            fields.Clear();
            shapeMeshes.Dispose();
        }

        void UpdateOccupancy(in LiveField field)
        {
            if (field.Occupants == null)
                return;

            overlapResults.Clear();
            hitQuery.OverlapDisk(field.SourceId, field.SkipKey, field.Disk, overlapResults);
            OccupancyExchange.Exchange(
                field.Occupants,
                overlapResults,
                seenTargets,
                field.SourceId,
                field.Damage,
                0f,
                field.Disk.Radius,
                field.Status,
                hit,
                statusApplied);
        }

        void Pulse(in LiveField field)
        {
            overlapResults.Clear();
            seenTargets.Clear();
            hitQuery.OverlapDisk(field.SourceId, field.SkipKey, field.Disk, overlapResults);

            for (int i = 0; i < overlapResults.Count; i++)
            {
                HitInfo areaHit = overlapResults[i];
                if (areaHit.Kind != HitKind.Target || areaHit.Protection == ProtectionLevel.Strong)
                    continue;
                if (ContainsTarget(seenTargets, areaHit.TargetId))
                    continue;

                seenTargets.Add(areaHit.TargetId);
                areaHit.InstanceId = field.SourceId;
                areaHit.Source = HitSource.Area;
                areaHit.Damage = field.Damage;
                areaHit.CriticalChance = 0f;
                areaHit.EffectRadius = field.Disk.Radius;
                hit?.Invoke(areaHit);

                if (field.Status == null)
                    continue;

                statusApplied?.Invoke(new StatusInfo
                {
                    InstanceId = field.SourceId,
                    TargetId = areaHit.TargetId,
                    Effect = field.Status
                });
            }

            pulsed?.Invoke(new GroundFieldPulseInfo
            {
                FieldId = field.FieldId,
                SourceId = field.SourceId,
                Center = field.Disk.Center,
                Normal = field.Disk.Normal,
                Radius = field.Disk.Radius,
                TargetsHit = seenTargets.Count,
                Shape = field.Disk.Shape,
                Direction = field.Disk.Axis,
                Length = field.Disk.Length,
                Width = field.Disk.Width,
                AngleDegrees = field.Disk.AngleDegrees
            });
        }

        static GroundFieldInfo ToInfo(in LiveField field, GroundFieldEndReason reason)
        {
            return new GroundFieldInfo
            {
                FieldId = field.FieldId,
                SourceId = field.SourceId,
                Center = field.Disk.Center,
                Normal = field.Disk.Normal,
                Radius = field.Disk.Radius,
                HeightMargin = field.Disk.HeightMargin,
                LifetimeSeconds = field.LifetimeSeconds,
                IntervalSeconds = field.IntervalSeconds,
                Occupancy = field.Occupancy,
                PresentationId = field.PresentationId,
                Reason = reason,
                Shape = field.Disk.Shape,
                Direction = field.Disk.Axis,
                Length = field.Disk.Length,
                Width = field.Disk.Width,
                AngleDegrees = field.Disk.AngleDegrees
            };
        }

        ProjectileView RentView(in GroundDisk disk, string presentationId)
        {
            Stack<ProjectileView> stack = ViewStack(presentationId, disk.Shape);
            ProjectileView view = stack.Count > 0 ? stack.Pop() : CreateView(disk.Radius, presentationId);
            Transform transform = view.transform;
            transform.position = disk.Center;
            transform.rotation = disk.Shape == GroundFieldShape.Disk
                ? Quaternion.FromToRotation(Vector3.up, disk.Normal)
                : Quaternion.LookRotation(disk.Axis, disk.Normal);
            if (view.ScaleToRadius)
                ShapePrimitive(view, disk);
            view.gameObject.SetActive(true);
            return view;
        }

        void ShapePrimitive(ProjectileView view, in GroundDisk disk)
        {
            Transform transform = view.transform;
            float thickness = PrimitiveGroundFieldViewFactory.DiskHalfHeight;
            switch (disk.Shape)
            {
                case GroundFieldShape.Rectangle:
                    SetMesh(view, shapeMeshes.Box());
                    transform.position = disk.Center + disk.Axis * (disk.Length * 0.5f);
                    transform.localScale = new Vector3(disk.Width, thickness * 2f, disk.Length);
                    return;
                case GroundFieldShape.Sector:
                    SetMesh(view, shapeMeshes.Sector(disk.AngleDegrees));
                    transform.localScale = new Vector3(disk.Length, thickness * 2f, disk.Length);
                    return;
                default:
                    transform.localScale = new Vector3(disk.Radius * 2f, thickness, disk.Radius * 2f);
                    return;
            }
        }

        static void SetMesh(ProjectileView view, Mesh mesh)
        {
            var filter = view.GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = mesh;
        }

        ProjectileView CreateView(float radius, string presentationId)
        {
            ProjectileView view = viewFactory.Create(radius, presentationId, poolParent);
            if (view == null)
                throw new InvalidOperationException("IGroundFieldViewFactory.Create returned null.");

            view.gameObject.name = "GroundField";
            StripColliders(view.gameObject);
            view.Release();
            allViews.Add(view);
            return view;
        }

        void ReturnView(LiveField field)
        {
            if (field.View == null)
                return;

            field.View.Release();
            ViewStack(field.PresentationId, field.Disk.Shape).Push(field.View);
        }

        Stack<ProjectileView> ViewStack(string presentationId, GroundFieldShape shape)
        {
            string key = presentationId ?? string.Empty;
            if (shape != GroundFieldShape.Disk)
                key += "\u0001" + (int)shape;
            if (!viewPools.TryGetValue(key, out Stack<ProjectileView> stack))
            {
                stack = new Stack<ProjectileView>();
                viewPools[key] = stack;
            }

            return stack;
        }

        static void StripColliders(GameObject go)
        {
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;
                collider.enabled = false;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(collider);
                else
                    UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        void ResolveGround(Vector3 point, float radius, out Vector3 center, out Vector3 normal)
        {
            Vector3 groundPoint = default;
            Vector3 groundNormal = default;
            bool found = Surface is IGroundFieldAreaSurface area
                ? area.TryGetGround(point, radius, out groundPoint, out groundNormal)
                : Surface != null && Surface.TryGetGround(point, out groundPoint, out groundNormal);
            if (found && IsFinite(groundPoint))
            {
                center = groundPoint;
                normal = GroundDisk.SanitizeNormal(groundNormal);
                return;
            }

            center = new Vector3(point.x, 0f, point.z);
            normal = Vector3.up;
        }

        static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }

        static bool ContainsTarget(List<TargetId> ids, TargetId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }

        struct LiveField
        {
            public GroundFieldId FieldId;
            public ProjectileId SourceId;
            public ProjectileId SkipKey;
            public GroundDisk Disk;
            public float LifetimeSeconds;
            public float RemainingLifetime;
            public float ElapsedSeconds;
            public float IntervalSeconds;
            public float Damage;
            public IStatusEffect Status;
            public string PresentationId;
            public ProjectileView View;
            public bool PlantedThisTick;
            public bool Occupancy;
            public List<TargetId> Occupants;
        }
    }

    /// <summary>
    /// The area a field tests: a disk, rectangle, or sector in the ground plane through <see cref="Center"/>, and an
    /// optional height margin. A Target stands where the vertical line through it meets the plane.
    /// With normal up and margin 0 a disk is the flat XZ disk.
    /// </summary>
    readonly struct GroundDisk
    {
        const float MinNormalY = 0.1f;

        readonly float halfWidth;
        readonly float cosHalfAngle;
        readonly float sinHalfAngle;

        public GroundDisk(Vector3 center, Vector3 normal, float radius, float heightMargin)
            : this(center, normal, radius, heightMargin, Vector3.forward)
        {
        }

        public GroundDisk(Vector3 center, Vector3 normal, float radius, float heightMargin, Vector3 planarDirection)
        {
            Center = center;
            Normal = normal;
            Radius = radius;
            HeightMargin = ClampMargin(heightMargin);
            Shape = GroundFieldShape.Disk;
            Axis = AlongPlane(planarDirection, normal);
            Side = Vector3.Cross(normal, Axis).normalized;
            Length = 0f;
            Width = 0f;
            AngleDegrees = 0f;
            halfWidth = 0f;
            cosHalfAngle = -1f;
            sinHalfAngle = 0f;
        }

        GroundDisk(Vector3 center, Vector3 normal, Vector3 planarDirection, in GroundFieldSize size, float heightMargin)
        {
            Center = center;
            Normal = normal;
            HeightMargin = ClampMargin(heightMargin);
            Shape = size.Shape;
            Axis = AlongPlane(planarDirection, normal);
            Side = Vector3.Cross(normal, Axis).normalized;
            Length = size.Length;
            if (size.Shape == GroundFieldShape.Rectangle)
            {
                Width = size.Width;
                AngleDegrees = 0f;
                halfWidth = size.Width * 0.5f;
                cosHalfAngle = 1f;
                sinHalfAngle = 0f;
                Radius = Mathf.Sqrt(Length * Length + halfWidth * halfWidth);
            }
            else
            {
                Width = 0f;
                AngleDegrees = size.ClampedAngleDegrees;
                halfWidth = 0f;
                float halfAngle = AngleDegrees * 0.5f * Mathf.Deg2Rad;
                cosHalfAngle = AngleDegrees >= 360f ? -1f : Mathf.Cos(halfAngle);
                sinHalfAngle = AngleDegrees >= 360f ? 0f : Mathf.Sin(halfAngle);
                Radius = Length;
            }
        }

        public static GroundDisk Shaped(Vector3 center, Vector3 normal, Vector3 planarDirection, in GroundFieldSize size, float heightMargin)
        {
            return size.Shape == GroundFieldShape.Disk
                ? new GroundDisk(center, normal, size.Radius, heightMargin, planarDirection)
                : new GroundDisk(center, normal, planarDirection, size, heightMargin);
        }

        public static float ClampMargin(float margin)
        {
            return margin > 0f && !float.IsInfinity(margin) ? margin : 0f;
        }

        public Vector3 Center { get; }

        public Vector3 Normal { get; }

        /// <summary>Disk radius; for a rectangle or sector, the distance from <see cref="Center"/> to its farthest point.</summary>
        public float Radius { get; }

        public float HeightMargin { get; }

        public GroundFieldShape Shape { get; }

        /// <summary>Unit direction in the plane whose horizontal part is the planted direction.</summary>
        public Vector3 Axis { get; }

        /// <summary>Unit direction in the plane, square to <see cref="Axis"/>.</summary>
        public Vector3 Side { get; }

        public float Length { get; }

        public float Width { get; }

        public float AngleDegrees { get; }

        /// <summary>Unit normal facing up. Zero, non-finite, or more than about 84 degrees from up becomes up.</summary>
        public static Vector3 SanitizeNormal(Vector3 normal)
        {
            float length = normal.magnitude;
            if (!(length > 1e-6f) || float.IsInfinity(length))
                return Vector3.up;

            normal /= length;
            if (normal.y < 0f)
                normal = -normal;
            return normal.y >= MinNormalY ? normal : Vector3.up;
        }

        /// <summary>Horizontal unit part of <paramref name="direction"/>; world forward when it has none or is not finite.</summary>
        public static Vector3 PlanarDirection(Vector3 direction)
        {
            var planar = new Vector3(direction.x, 0f, direction.z);
            float length = planar.magnitude;
            return length > 1e-6f && !float.IsInfinity(length) && !float.IsNaN(length)
                ? planar / length
                : Vector3.forward;
        }

        /// <summary>Lifts a horizontal unit direction into the plane with <paramref name="normal"/> and normalizes it.</summary>
        static Vector3 AlongPlane(Vector3 planarDirection, Vector3 normal)
        {
            if (!(normal.y > 1e-6f))
                return planarDirection;

            float rise = -(planarDirection.x * normal.x + planarDirection.z * normal.z) / normal.y;
            return new Vector3(planarDirection.x, rise, planarDirection.z).normalized;
        }

        /// <summary>Where the vertical line through <paramref name="point"/> meets the plane through <paramref name="center"/>.</summary>
        public static Vector3 OnPlane(Vector3 point, Vector3 center, Vector3 normal)
        {
            float rise = Vector3.Dot(center - point, normal) / normal.y;
            return new Vector3(point.x, point.y + rise, point.z);
        }

        /// <summary>Where the vertical line through <paramref name="point"/> meets the field's plane.</summary>
        public Vector3 OnPlane(Vector3 point)
        {
            return OnPlane(point, Center, Normal);
        }

        /// <summary>
        /// True when the vertical line through <paramref name="stand"/> meets the plane inside the shape grown by
        /// <paramref name="extraRadius"/> and, with a margin, <paramref name="foot"/> lies within <see cref="HeightMargin"/>
        /// of the plane.
        /// </summary>
        public bool Contains(Vector3 stand, Vector3 foot, float extraRadius, out Vector3 inPlaneDelta)
        {
            inPlaneDelta = OnPlane(stand) - Center;
            if (HeightMargin > 0f && Mathf.Abs(Vector3.Dot(foot - Center, Normal)) > HeightMargin)
                return false;

            switch (Shape)
            {
                case GroundFieldShape.Rectangle:
                    return InRectangle(inPlaneDelta, extraRadius);
                case GroundFieldShape.Sector:
                    return InSector(inPlaneDelta, extraRadius);
                default:
                    float limit = Radius + extraRadius;
                    return inPlaneDelta.sqrMagnitude <= limit * limit;
            }
        }

        bool InRectangle(Vector3 delta, float extra)
        {
            float along = Vector3.Dot(delta, Axis);
            float side = Mathf.Abs(Vector3.Dot(delta, Side));
            float outAlong = Mathf.Max(0f, Mathf.Max(-along, along - Length));
            float outSide = Mathf.Max(0f, side - halfWidth);
            return outAlong * outAlong + outSide * outSide <= extra * extra;
        }

        bool InSector(Vector3 delta, float extra)
        {
            float along = Vector3.Dot(delta, Axis);
            float side = Mathf.Abs(Vector3.Dot(delta, Side));
            float sqrDistance = along * along + side * side;
            float reach = Length + extra;
            if (sqrDistance > reach * reach)
                return false;

            // side >= 0, so this is sin(half - angle) >= 0: the angle to the axis is at most the half angle.
            if (along * sinHalfAngle >= side * cosHalfAngle)
                return true;

            if (!(extra > 0f))
                return false;

            float t = Mathf.Clamp(along * cosHalfAngle + side * sinHalfAngle, 0f, Length);
            float dx = along - t * cosHalfAngle;
            float dy = side - t * sinHalfAngle;
            return dx * dx + dy * dy <= extra * extra;
        }
    }

    /// <summary>Meshes the world gives the primitive view of a rectangle or sector field. Destroyed with the field set.</summary>
    sealed class GroundFieldShapeMeshes : IDisposable
    {
        const int SegmentsPerFullTurn = 64;

        readonly Dictionary<int, Mesh> sectors = new Dictionary<int, Mesh>();
        Mesh box;

        /// <summary>Unit cube centred on the origin.</summary>
        public Mesh Box()
        {
            if (box != null)
                return box;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.DestroyImmediate(collider);
            cube.SetActive(false);
            box = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            return box;
        }

        /// <summary>Flat sector of radius 1 in XZ, apex at the origin, centred on +Z, facing up.</summary>
        public Mesh Sector(float angleDegrees)
        {
            int key = Mathf.RoundToInt(angleDegrees * 10f);
            if (sectors.TryGetValue(key, out Mesh mesh) && mesh != null)
                return mesh;

            float total = key / 10f * Mathf.Deg2Rad;
            int segments = Mathf.Max(1, Mathf.CeilToInt(SegmentsPerFullTurn * total / (2f * Mathf.PI)));
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = -total * 0.5f + total * i / segments;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh = new Mesh { name = "GroundFieldSector" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            sectors[key] = mesh;
            return mesh;
        }

        public void Dispose()
        {
            foreach (Mesh mesh in sectors.Values)
            {
                if (mesh != null)
                    Destroy(mesh);
            }

            sectors.Clear();
            box = null;
        }

        static void Destroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
