using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Plants a lingering horizontal disk. Size, lifetime, damage, optional status, and pulse
    /// period live on this modifier. OnHit uses the contact point; OnExpire / OnTravel use the
    /// instance position. The disk sits on the world ground plane (Y = 0).
    /// <see cref="Occupancy"/> replaces pulses with enter/exit while the disk lives.
    /// The modifier queues a plant; it does not call Physics or spawn a Recipe.
    /// Radius or lifetime &lt;= 0 is a no-op. Interval &lt;= 0 never pulses when occupancy is false.
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
            bool occupancy = false)
        {
            this.radius = radius;
            this.lifetimeSeconds = lifetimeSeconds;
            this.damage = damage;
            this.status = status;
            this.intervalSeconds = intervalSeconds;
            this.presentationId = presentationId;
            this.occupancy = occupancy;
        }

        /// <summary>XZ disk radius in meters. &lt;= 0 plants nothing.</summary>
        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        /// <summary>Seconds the disk lives. Independent of the projectile. &lt;= 0 plants nothing.</summary>
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

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Radius <= 0f || LifetimeSeconds <= 0f)
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
                Occupancy);
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
        readonly List<LiveField> fields = new List<LiveField>();
        readonly Dictionary<string, Stack<ProjectileView>> viewPools = new Dictionary<string, Stack<ProjectileView>>();
        readonly List<ProjectileView> allViews = new List<ProjectileView>();
        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<TargetId> seenTargets = new List<TargetId>();
        bool disposed;

        public GroundFieldSet(
            IHitQuery hitQuery,
            bool createViews,
            Transform poolParent,
            Action<HitInfo> hit,
            Action<StatusInfo> statusApplied,
            IGroundFieldViewFactory viewFactory = null)
        {
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            this.createViews = createViews;
            this.poolParent = poolParent;
            this.viewFactory = createViews ? viewFactory ?? new PrimitiveGroundFieldViewFactory() : null;
            this.hit = hit;
            this.statusApplied = statusApplied;
        }

        public void Plant(ProjectileId sourceId, in GroundFieldPlantRequest request)
        {
            if (request.Radius <= 0f || request.LifetimeSeconds <= 0f)
                return;

            Vector3 center = ToGround(request.Center);
            var field = new LiveField
            {
                SourceId = sourceId,
                Center = center,
                Radius = request.Radius,
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
                field.View = RentView(center, request.Radius, request.PresentationId);

            fields.Add(field);
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
                            field.Radius,
                            status: null,
                            hit,
                            statusApplied);
                    }

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

                if (field.IntervalSeconds <= 0f)
                {
                    fields[i] = field;
                    continue;
                }

                float previousElapsed = field.ElapsedSeconds;
                field.ElapsedSeconds += dt;
                int fires = IntervalCrossings(previousElapsed, field.ElapsedSeconds, field.IntervalSeconds);
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
            hitQuery.OverlapDisk(field.SourceId, field.Center, field.Radius, overlapResults);
            OccupancyExchange.Exchange(
                field.Occupants,
                overlapResults,
                seenTargets,
                field.SourceId,
                field.Damage,
                0f,
                field.Radius,
                field.Status,
                hit,
                statusApplied);
        }

        void Pulse(in LiveField field)
        {
            overlapResults.Clear();
            seenTargets.Clear();
            hitQuery.OverlapDisk(field.SourceId, field.Center, field.Radius, overlapResults);

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
                areaHit.EffectRadius = field.Radius;
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
        }

        ProjectileView RentView(Vector3 center, float radius, string presentationId)
        {
            Stack<ProjectileView> stack = ViewStack(presentationId);
            ProjectileView view = stack.Count > 0 ? stack.Pop() : CreateView(radius, presentationId);
            Transform transform = view.transform;
            transform.position = center;
            transform.rotation = Quaternion.identity;
            if (view.ScaleToRadius)
                transform.localScale = new Vector3(
                    radius * 2f,
                    PrimitiveGroundFieldViewFactory.DiskHalfHeight,
                    radius * 2f);
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

        static Vector3 ToGround(Vector3 point)
        {
            return new Vector3(point.x, 0f, point.z);
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
            public ProjectileId SourceId;
            public Vector3 Center;
            public float Radius;
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
}
