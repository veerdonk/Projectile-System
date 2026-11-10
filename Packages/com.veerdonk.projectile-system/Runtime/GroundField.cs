using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Plants a lingering disk. Size, lifetime, damage, optional status, and pulse
    /// period live on this modifier. OnHit uses the contact point; OnExpire / OnTravel use the
    /// instance position. The disk lies on the ground that <see cref="IProjectileWorld.GroundFieldSurface"/>
    /// returns for that point, or on the world ground plane (Y = 0, normal up) without one.
    /// <see cref="Occupancy"/> replaces pulses with enter/exit while the disk lives.
    /// The modifier queues a plant; it does not call Physics or spawn a Recipe.
    /// Radius &lt;= 0 or not finite, or lifetime &lt;= 0 or NaN, is a no-op. Interval &lt;= 0 or NaN never pulses when
    /// occupancy is false.
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

        /// <summary>Disk radius in meters, measured in the ground plane. &lt;= 0, NaN, or infinite plants nothing.</summary>
        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        /// <summary>Seconds the disk lives. Independent of the projectile. &lt;= 0 or NaN plants nothing; infinity lives until
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

        /// <summary>Seconds between pulses after the plant pulse. &lt;= 0 never pulses.</summary>
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
        /// Meters a Target's foot (the bottom of its capsule axis) may lie above or below the disk's plane, measured
        /// along the ground normal. 0, which existing assets read, does not test height (a collider still has to be found by
        /// the query capsule, about 1 m above and below the plane plus the slope's rise). A negative or non-finite value is stored as 0.
        /// </summary>
        public float HeightMargin
        {
            get => heightMargin;
            set => heightMargin = GroundDisk.ClampMargin(value);
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!Plants(Radius, LifetimeSeconds))
                return;

            Vector3 center = context.HasHit ? context.Hit.Point : context.Position;
            context.RequestGroundField(
                center,
                Radius,
                LifetimeSeconds,
                Damage,
                Status,
                IntervalSeconds,
                PresentationId,
                Occupancy,
                HeightMargin);
        }

        internal static bool Plants(float radius, float lifetimeSeconds)
        {
            return float.IsFinite(radius) && radius > 0f && lifetimeSeconds > 0f;
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
            if (!GroundField.Plants(request.Radius, request.LifetimeSeconds))
                return;

            ResolveGround(request.Center, request.Radius, out Vector3 center, out Vector3 normal);
            var disk = new GroundDisk(center, normal, request.Radius, request.HeightMargin);
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
                IntervalSeconds = request.IntervalSeconds,
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
            else if (request.IntervalSeconds > 0f)
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
                int fires = IntervalCrossings(previousElapsed, field.ElapsedSeconds, field.IntervalSeconds);
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
                TargetsHit = seenTargets.Count
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
                Reason = reason
            };
        }

        ProjectileView RentView(in GroundDisk disk, string presentationId)
        {
            Stack<ProjectileView> stack = ViewStack(presentationId);
            ProjectileView view = stack.Count > 0 ? stack.Pop() : CreateView(disk.Radius, presentationId);
            Transform transform = view.transform;
            transform.position = disk.Center;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, disk.Normal);
            if (view.ScaleToRadius)
                transform.localScale = new Vector3(
                    disk.Radius * 2f,
                    PrimitiveGroundFieldViewFactory.DiskHalfHeight,
                    disk.Radius * 2f);
            view.gameObject.SetActive(true);
            return view;
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
            ViewStack(field.PresentationId).Push(field.View);
        }

        Stack<ProjectileView> ViewStack(string presentationId)
        {
            string key = presentationId ?? string.Empty;
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

        static int IntervalCrossings(float previousElapsed, float elapsed, float interval)
        {
            if (interval <= 0f || elapsed <= previousElapsed)
                return 0;

            int before = Mathf.FloorToInt(previousElapsed / interval);
            int after = Mathf.FloorToInt(elapsed / interval);
            int fires = after - before;
            return fires > 0 ? fires : 0;
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
    /// The disk a field tests: a center on the ground, the ground normal, a radius in that plane, and an optional
    /// height margin. A Target stands where the vertical line through it meets the plane.
    /// With normal up and margin 0 this is the flat XZ disk.
    /// </summary>
    readonly struct GroundDisk
    {
        const float MinNormalY = 0.1f;

        public GroundDisk(Vector3 center, Vector3 normal, float radius, float heightMargin)
        {
            Center = center;
            Normal = normal;
            Radius = radius;
            HeightMargin = ClampMargin(heightMargin);
        }

        public static float ClampMargin(float margin)
        {
            return margin > 0f && !float.IsInfinity(margin) ? margin : 0f;
        }

        public Vector3 Center { get; }

        public Vector3 Normal { get; }

        public float Radius { get; }

        public float HeightMargin { get; }

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

        /// <summary>Where the vertical line through <paramref name="point"/> meets the disk's plane.</summary>
        public Vector3 OnPlane(Vector3 point)
        {
            float rise = Vector3.Dot(Center - point, Normal) / Normal.y;
            return new Vector3(point.x, point.y + rise, point.z);
        }

        /// <summary>
        /// True when the vertical line through <paramref name="stand"/> meets the plane within
        /// <see cref="Radius"/> plus <paramref name="extraRadius"/> of the center and, with a margin,
        /// <paramref name="foot"/> lies within <see cref="HeightMargin"/> of the plane.
        /// </summary>
        public bool Contains(Vector3 stand, Vector3 foot, float extraRadius, out Vector3 inPlaneDelta)
        {
            inPlaneDelta = OnPlane(stand) - Center;
            if (HeightMargin > 0f && Mathf.Abs(Vector3.Dot(foot - Center, Normal)) > HeightMargin)
                return false;

            float limit = Radius + extraRadius;
            return inPlaneDelta.sqrMagnitude <= limit * limit;
        }
    }
}
