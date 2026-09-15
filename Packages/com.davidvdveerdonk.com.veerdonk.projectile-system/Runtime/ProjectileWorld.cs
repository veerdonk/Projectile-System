using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>Default <see cref="IProjectileWorld"/>: ECS instances, kinematic move, PhysX hits, entity recycle.</summary>
    public sealed class ProjectileWorld : IProjectileWorld, IDisposable
    {
        readonly IHitQuery hitQuery;
        readonly RecipeStore recipes = new RecipeStore();
        readonly System.Collections.Generic.List<SpawnRequest> pendingSpawns = new System.Collections.Generic.List<SpawnRequest>();
        readonly ModifierContext modifierContext;
        readonly SimulationBridge bridge;
        World ecsWorld;
        ProjectilePool pool;
        SimulationSystemGroup simulationGroup;
        ProjectileSimulationSystem simulation;
        Entity deltaTimeEntity;
        EntityQuery liveQuery;
        int nextId = 1;
        bool disposed;

        public ProjectileWorld(Transform poolParent = null)
            : this(new PhysicsHitQuery(), createViews: true, poolParent)
        {
        }

        internal ProjectileWorld(IHitQuery hitQuery, bool createViews, uint seed = 1u)
            : this(hitQuery, createViews, null, seed)
        {
        }

        ProjectileWorld(IHitQuery hitQuery, bool createViews, Transform poolParent, uint seed = 1u)
        {
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            modifierContext = new ModifierContext(pendingSpawns, seed);
            ecsWorld = new World("ProjectileSimulation");
            pool = new ProjectilePool(ecsWorld, poolParent, createViews);

            EntityManager em = ecsWorld.EntityManager;
            deltaTimeEntity = em.CreateEntity(typeof(SimDeltaTime));
            em.SetComponentData(deltaTimeEntity, new SimDeltaTime { Value = 0f });
            liveQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Projectile>());

            simulationGroup = ecsWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            SystemHandle lifetime = ecsWorld.CreateSystem<LifetimeSystem>();
            simulation = ecsWorld.CreateSystemManaged<ProjectileSimulationSystem>();
            simulationGroup.AddSystemToUpdateList(lifetime);
            simulationGroup.AddSystemToUpdateList(simulation);
            simulationGroup.SortSystems();

            bridge = new SimulationBridge
            {
                HitQuery = this.hitQuery,
                Recipes = recipes,
                Pool = pool,
                ModifierContext = modifierContext,
                Hit = info => Hit?.Invoke(info),
                Expired = info => Expired?.Invoke(info)
            };
            simulation.Bridge = bridge;
        }

        public event Action<HitInfo> Hit;

        public event Action<ExpireInfo> Expired;

        public int ActiveCount
        {
            get
            {
                ThrowIfDisposed();
                return liveQuery.CalculateEntityCount();
            }
        }

        public int AvailableCount
        {
            get
            {
                ThrowIfDisposed();
                return pool.AvailableCount;
            }
        }

        /// <summary>Creates inactive pooled entities (and views when enabled) for <paramref name="recipe"/>.</summary>
        public void Prewarm(Recipe recipe, int count)
        {
            ThrowIfDisposed();
            int index = recipes.GetOrAdd(recipe);
            pool.Prewarm(index, recipe, count);
        }

        public ProjectileId Spawn(in SpawnRequest request)
        {
            ThrowIfDisposed();
            ProjectileId id = SpawnCore(request);
            FlushPendingSpawns();
            return id;
        }

        public void Tick(float dt)
        {
            ThrowIfDisposed();
            if (dt < 0f)
                dt = 0f;

            ecsWorld.EntityManager.SetComponentData(deltaTimeEntity, new SimDeltaTime { Value = dt });
            simulationGroup.Update();
            FlushPendingSpawns();
        }

        internal bool TryGetState(ProjectileId id, out Vector3 position, out Vector3 velocity, out Recipe recipe)
        {
            ThrowIfDisposed();
            EntityManager em = ecsWorld.EntityManager;
            NativeArray<Entity> entities = liveQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    Projectile projectile = em.GetComponentData<Projectile>(entity);
                    if (new ProjectileId(projectile.Id) != id)
                        continue;

                    position = ToVector3(em.GetComponentData<ProjectilePosition>(entity).Value);
                    velocity = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                    recipe = recipes.Get(projectile.RecipeIndex);
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            position = default;
            velocity = default;
            recipe = null;
            return false;
        }

        internal bool TryGetRemaining(ProjectileId id, out int remainingPierce, out int remainingBounce)
        {
            ThrowIfDisposed();
            EntityManager em = ecsWorld.EntityManager;
            NativeArray<Entity> entities = liveQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Projectile projectile = em.GetComponentData<Projectile>(entities[i]);
                    if (new ProjectileId(projectile.Id) != id)
                        continue;

                    remainingPierce = projectile.RemainingPierce;
                    remainingBounce = projectile.RemainingBounce;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            remainingPierce = 0;
            remainingBounce = 0;
            return false;
        }

        internal bool TryGetActiveAt(int index, out ProjectileId id, out Vector3 position, out Vector3 velocity, out Recipe recipe)
        {
            ThrowIfDisposed();
            if (index < 0)
            {
                id = default;
                position = default;
                velocity = default;
                recipe = null;
                return false;
            }

            EntityManager em = ecsWorld.EntityManager;
            NativeArray<Entity> entities = liveQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (index >= entities.Length)
                {
                    id = default;
                    position = default;
                    velocity = default;
                    recipe = null;
                    return false;
                }

                Entity entity = entities[index];
                Projectile projectile = em.GetComponentData<Projectile>(entity);
                id = new ProjectileId(projectile.Id);
                position = ToVector3(em.GetComponentData<ProjectilePosition>(entity).Value);
                velocity = ToVector3(em.GetComponentData<ProjectileVelocity>(entity).Value);
                recipe = recipes.Get(projectile.RecipeIndex);
                return true;
            }
            finally
            {
                entities.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            pool?.Dispose();
            pool = null;

            if (ecsWorld != null && ecsWorld.IsCreated)
            {
                liveQuery.Dispose();
                liveQuery = default;
                ecsWorld.Dispose();
            }

            simulation = null;
            simulationGroup = null;
            ecsWorld = null;
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

            int recipeIndex = recipes.GetOrAdd(recipe);
            Entity entity = pool.Rent(recipeIndex, recipe, out ProjectileView view);
            int idValue = nextId++;
            ReadHitRemaining(recipe, out int remainingPierce, out int remainingBounce);

            float3 position = new float3(request.Position.x, request.Position.y, request.Position.z);
            float3 velocity = new float3(direction.x, direction.y, direction.z) * recipe.Speed;

            EntityManager em = ecsWorld.EntityManager;
            em.SetComponentData(entity, new Projectile
            {
                Id = idValue,
                RecipeIndex = recipeIndex,
                Speed = recipe.Speed,
                Radius = recipe.Radius,
                RemainingLifetime = recipe.LifetimeSeconds,
                ElapsedSeconds = 0f,
                RemainingPierce = remainingPierce,
                RemainingBounce = remainingBounce
            });
            em.SetComponentData(entity, new ProjectilePosition { Value = position });
            em.SetComponentData(entity, new ProjectileVelocity { Value = velocity });
            em.SetComponentData(entity, new ProjectileCandidate { Value = position });

            view?.Bind(request.Position, recipe.Radius);
            hitQuery.CopyIgnore(request.InheritIgnoreFrom, new ProjectileId(idValue));
            simulation.RunSpawnStages(entity);
            return new ProjectileId(idValue);
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

        void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ProjectileWorld));
        }

        static void ReadHitRemaining(Recipe recipe, out int remainingPierce, out int remainingBounce)
        {
            remainingPierce = 0;
            remainingBounce = 0;
            IModifier[] onHit = recipe.OnHit;
            if (onHit == null)
                return;

            for (int i = 0; i < onHit.Length; i++)
            {
                switch (onHit[i])
                {
                    case Pierce pierce:
                        remainingPierce = pierce.Count > 0 ? pierce.Count : 0;
                        break;
                    case Bounce bounce:
                        remainingBounce = bounce.Count > 0 ? bounce.Count : 0;
                        break;
                }
            }
        }

        static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
