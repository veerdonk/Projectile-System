using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>Default <see cref="IProjectileWorld"/>: kinematic move, PhysX hits, pool rent/return.</summary>
    public sealed class ProjectileWorld : IProjectileWorld
    {
        readonly IHitQuery hitQuery;
        readonly ProjectilePool pool;
        readonly List<LiveProjectile> active = new List<LiveProjectile>();
        readonly List<SpawnRequest> pendingSpawns = new List<SpawnRequest>();
        readonly ModifierContext modifierContext;
        int nextId = 1;

        public ProjectileWorld(Transform poolParent = null)
            : this(new PhysicsHitQuery(), new ProjectilePool(poolParent, createViews: true))
        {
        }

        internal ProjectileWorld(IHitQuery hitQuery, bool createViews)
            : this(hitQuery, new ProjectilePool(null, createViews))
        {
        }

        internal ProjectileWorld(IHitQuery hitQuery, ProjectilePool pool)
        {
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            modifierContext = new ModifierContext(pendingSpawns);
        }

        public event Action<HitInfo> Hit;

        public event Action<ExpireInfo> Expired;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].Alive)
                        count++;
                }

                return count;
            }
        }

        public int AvailableCount => pool.AvailableCount;

        /// <summary>Creates inactive pooled views (or logical slots) for <paramref name="recipe"/>.</summary>
        public void Prewarm(Recipe recipe, int count)
        {
            pool.Prewarm(recipe, count);
        }

        public ProjectileId Spawn(in SpawnRequest request)
        {
            ProjectileId id = SpawnCore(request);
            FlushPendingSpawns();
            return id;
        }

        public void Tick(float dt)
        {
            if (dt < 0f)
                dt = 0f;

            for (int i = 0; i < active.Count; i++)
                TickOne(active[i], dt);

            CompactDead();
            FlushPendingSpawns();
        }

        internal bool TryGetState(ProjectileId id, out Vector3 position, out Vector3 velocity, out Recipe recipe)
        {
            for (int i = 0; i < active.Count; i++)
            {
                LiveProjectile projectile = active[i];
                if (!projectile.Alive || projectile.Id != id)
                    continue;

                position = projectile.Position;
                velocity = projectile.Velocity;
                recipe = projectile.Recipe;
                return true;
            }

            position = default;
            velocity = default;
            recipe = null;
            return false;
        }

        internal bool TryGetActiveAt(int index, out ProjectileId id, out Vector3 position, out Vector3 velocity, out Recipe recipe)
        {
            int n = 0;
            for (int i = 0; i < active.Count; i++)
            {
                LiveProjectile projectile = active[i];
                if (!projectile.Alive)
                    continue;

                if (n == index)
                {
                    id = projectile.Id;
                    position = projectile.Position;
                    velocity = projectile.Velocity;
                    recipe = projectile.Recipe;
                    return true;
                }

                n++;
            }

            id = default;
            position = default;
            velocity = default;
            recipe = null;
            return false;
        }

        ProjectileId SpawnCore(in SpawnRequest request)
        {
            Recipe recipe = request.Recipe;
            if (recipe == null)
                throw new ArgumentException("SpawnRequest.Recipe is required.", nameof(request));
            if (string.IsNullOrEmpty(recipe.Id))
                throw new ArgumentException("Recipe.Id is required.", nameof(request));

            Vector3 direction = request.Direction;
            if (direction.sqrMagnitude < 1e-12f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            var projectile = new LiveProjectile
            {
                Id = new ProjectileId(nextId++),
                Recipe = recipe,
                Position = request.Position,
                Velocity = direction * recipe.Speed,
                Speed = recipe.Speed,
                Radius = recipe.Radius,
                RemainingLifetime = recipe.LifetimeSeconds,
                Alive = true,
                View = pool.Rent(recipe)
            };

            projectile.View?.Bind(projectile.Position, projectile.Radius);
            active.Add(projectile);

            RunStage(recipe.OnSpawn, projectile, hasHit: false, default);
            RunStage(recipe.OnTravel, projectile, hasHit: false, default);
            return projectile.Id;
        }

        void TickOne(LiveProjectile projectile, float dt)
        {
            if (!projectile.Alive)
                return;

            projectile.RemainingLifetime -= dt;
            if (projectile.RemainingLifetime <= 0f)
            {
                Despawn(projectile, runOnHit: false, default);
                return;
            }

            RunStage(projectile.Recipe.OnTick, projectile, hasHit: false, default);

            Vector3 from = projectile.Position;
            Vector3 delta = projectile.Velocity * dt;
            float distance = delta.magnitude;
            Vector3 direction = distance > 1e-8f ? delta / distance : projectile.Velocity;

            if (hitQuery.TryCast(projectile.Id, from, direction, distance, projectile.Radius, out HitInfo hit))
            {
                Hit?.Invoke(hit);
                Despawn(projectile, runOnHit: true, hit);
                return;
            }

            projectile.Position = from + delta;
            projectile.View?.SetPose(projectile.Position, projectile.Velocity);
        }

        void Despawn(LiveProjectile projectile, bool runOnHit, in HitInfo hit)
        {
            if (!projectile.Alive)
                return;

            if (runOnHit)
            {
                RunStage(projectile.Recipe.OnHit, projectile, hasHit: true, hit);
                if (modifierContext.CancelDespawn)
                {
                    projectile.View?.SetPose(projectile.Position, projectile.Velocity);
                    return;
                }
            }

            RunStage(projectile.Recipe.OnExpire, projectile, hasHit: false, default);

            projectile.Alive = false;
            pool.Return(projectile.Recipe.Id, projectile.View);
            projectile.View = null;
            Expired?.Invoke(new ExpireInfo { InstanceId = projectile.Id });
        }

        void RunStage(IModifier[] modifiers, LiveProjectile projectile, bool hasHit, in HitInfo hit)
        {
            if (modifiers == null || modifiers.Length == 0)
                return;

            modifierContext.Bind(projectile, hasHit, hit);
            for (int i = 0; i < modifiers.Length; i++)
            {
                IModifier modifier = modifiers[i];
                if (modifier == null)
                    continue;
                modifier.Apply(modifierContext);
            }

            modifierContext.ApplyTo(projectile);
        }

        void CompactDead()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (!active[i].Alive)
                    active.RemoveAt(i);
            }
        }

        void FlushPendingSpawns()
        {
            while (pendingSpawns.Count > 0)
            {
                SpawnRequest request = pendingSpawns[0];
                pendingSpawns.RemoveAt(0);
                SpawnCore(request);
            }
        }
    }
}
