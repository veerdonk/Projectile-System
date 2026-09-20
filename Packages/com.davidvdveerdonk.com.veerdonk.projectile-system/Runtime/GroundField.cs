using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Plants a lingering horizontal disk. Size, lifetime, damage, optional status, and pulse
    /// period live on this modifier. OnHit uses the contact point; OnExpire / OnTravel use the
    /// instance position. The modifier queues a plant; it does not call Physics or spawn a Recipe.
    /// Radius or lifetime &lt;= 0 is a no-op. Interval &lt;= 0 never pulses.
    /// </summary>
    public sealed class GroundField : IModifier
    {
        public GroundField(
            float radius,
            float lifetimeSeconds,
            float damage,
            IStatusEffect status = null,
            float intervalSeconds = 0f)
        {
            Radius = radius;
            LifetimeSeconds = lifetimeSeconds;
            Damage = damage;
            Status = status;
            IntervalSeconds = intervalSeconds;
        }

        public float Radius { get; }

        public float LifetimeSeconds { get; }

        public float Damage { get; }

        public IStatusEffect Status { get; }

        public float IntervalSeconds { get; }

        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Radius <= 0f || LifetimeSeconds <= 0f)
                return;

            Vector3 center = context.HasHit ? context.Hit.Point : context.Position;
            context.RequestGroundField(center, Radius, LifetimeSeconds, Damage, Status, IntervalSeconds);
        }
    }

    sealed class GroundFieldSet : IDisposable
    {
        const float DiskHalfHeight = 0.025f;

        readonly IHitQuery hitQuery;
        readonly bool createViews;
        readonly Transform poolParent;
        readonly Action<HitInfo> hit;
        readonly Action<StatusInfo> statusApplied;
        readonly List<LiveField> fields = new List<LiveField>();
        readonly Stack<ProjectileView> viewPool = new Stack<ProjectileView>();
        readonly List<ProjectileView> allViews = new List<ProjectileView>();
        readonly List<HitInfo> overlapResults = new List<HitInfo>();
        readonly List<TargetId> seenTargets = new List<TargetId>();
        bool disposed;

        public GroundFieldSet(
            IHitQuery hitQuery,
            bool createViews,
            Transform poolParent,
            Action<HitInfo> hit,
            Action<StatusInfo> statusApplied)
        {
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            this.createViews = createViews;
            this.poolParent = poolParent;
            this.hit = hit;
            this.statusApplied = statusApplied;
        }

        public void Plant(ProjectileId sourceId, in GroundFieldPlantRequest request)
        {
            if (request.Radius <= 0f || request.LifetimeSeconds <= 0f)
                return;

            var field = new LiveField
            {
                SourceId = sourceId,
                Center = request.Center,
                Radius = request.Radius,
                RemainingLifetime = request.LifetimeSeconds,
                IntervalSeconds = request.IntervalSeconds,
                Damage = request.Damage,
                Status = request.Status,
                PlantedThisTick = true
            };
            if (createViews)
                field.View = RentView(request.Center, request.Radius);

            fields.Add(field);
            if (request.IntervalSeconds > 0f)
                Pulse(field);
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
                    ReturnView(field);
                    fields.RemoveAt(i);
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
            viewPool.Clear();
            fields.Clear();
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

        ProjectileView RentView(Vector3 center, float radius)
        {
            ProjectileView view = viewPool.Count > 0 ? viewPool.Pop() : CreateView();
            Transform transform = view.transform;
            transform.position = center;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(radius * 2f, DiskHalfHeight, radius * 2f);
            view.gameObject.SetActive(true);
            return view;
        }

        ProjectileView CreateView()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GroundField";
            if (poolParent != null)
                go.transform.SetParent(poolParent, false);

            var view = go.GetComponent<ProjectileView>();
            if (view == null)
                view = go.AddComponent<ProjectileView>();
            view.ScaleToRadius = false;
            StripColliders(go);
            view.Release();
            allViews.Add(view);
            return view;
        }

        void ReturnView(LiveField field)
        {
            if (field.View == null)
                return;

            field.View.Release();
            viewPool.Push(field.View);
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
            public ProjectileView View;
            public bool PlantedThisTick;
        }
    }
}
