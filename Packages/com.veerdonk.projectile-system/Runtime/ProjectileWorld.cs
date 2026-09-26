using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Default <see cref="IProjectileWorld"/>: ECS instances, kinematic move, PhysX hits, entity recycle.</summary>
    public sealed class ProjectileWorld : IProjectileWorld, IDisposable
    {
        const int MaxPrewarmChainDepth = 8;
        const int MaxPrewarmPerRecipe = 512;

        readonly IHitQuery hitQuery;
        readonly HitTargetCatalog hitTargets = new HitTargetCatalog();
        readonly RecipeStore recipes = new RecipeStore();
        readonly System.Collections.Generic.List<SpawnRequest> pendingSpawns = new System.Collections.Generic.List<SpawnRequest>();
        readonly HomingCatalog homingCatalog = new HomingCatalog();
        readonly ModifierContext modifierContext;
        readonly SimulationBridge bridge;
        readonly GroundFieldSet groundFields;
        World ecsWorld;
        ProjectilePool pool;
        SimulationSystemGroup simulationGroup;
        ProjectileSimulationSystem simulation;
        Entity deltaTimeEntity;
        EntityQuery liveQuery;
        int nextId = 1;
        bool disposed;

        /// <summary>
        /// Owns an isolated Entities world, the instance pool, and lingering ground fields.
        /// Pass factories to choose meshes; omit them for the primitive sphere / disk.
        /// </summary>
        public ProjectileWorld(
            Transform poolParent = null,
            IProjectileViewFactory viewFactory = null,
            IGroundFieldViewFactory groundFieldViewFactory = null)
            : this(new PhysicsHitQuery(), createViews: true, poolParent, seed: 1u, viewFactory, groundFieldViewFactory)
        {
        }

        internal ProjectileWorld(IHitQuery hitQuery, bool createViews, uint seed = 1u)
            : this(hitQuery, createViews, null, seed)
        {
        }

        internal ProjectileWorld(
            IHitQuery hitQuery,
            bool createViews,
            IGroundFieldViewFactory groundFieldViewFactory,
            uint seed = 1u)
            : this(hitQuery, createViews, null, seed, null, groundFieldViewFactory)
        {
        }

        ProjectileWorld(
            IHitQuery hitQuery,
            bool createViews,
            Transform poolParent,
            uint seed = 1u,
            IProjectileViewFactory viewFactory = null,
            IGroundFieldViewFactory groundFieldViewFactory = null)
        {
            this.hitQuery = hitQuery ?? throw new ArgumentNullException(nameof(hitQuery));
            this.hitQuery.HitTargets = hitTargets;
            modifierContext = new ModifierContext(pendingSpawns, homingCatalog, seed);
            modifierContext.HitTargets = hitTargets;
            groundFields = new GroundFieldSet(
                this.hitQuery,
                createViews,
                poolParent,
                info => Hit?.Invoke(info),
                info => StatusApplied?.Invoke(info),
                groundFieldViewFactory);
            ecsWorld = new World("ProjectileSimulation");
            pool = new ProjectilePool(ecsWorld, poolParent, createViews, viewFactory);

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
                Expired = info => Expired?.Invoke(info),
                StatusApplied = info => StatusApplied?.Invoke(info),
                GroundFields = groundFields
            };
            simulation.Bridge = bridge;
        }

        /// <inheritdoc />
        public event Action<HitInfo> Hit;

        /// <inheritdoc />
        public event Action<ExpireInfo> Expired;

        /// <inheritdoc />
        public event Action<StatusInfo> StatusApplied;

        /// <summary>Live instances (not Disabled pool entities).</summary>
        public int ActiveCount
        {
            get
            {
                ThrowIfDisposed();
                return liveQuery.CalculateEntityCount();
            }
        }

        /// <summary>Pooled inactive entities ready to rent.</summary>
        public int AvailableCount
        {
            get
            {
                ThrowIfDisposed();
                return pool.AvailableCount;
            }
        }

        /// <summary>
        /// Creates inactive pooled entities (and views when enabled) for <paramref name="recipe"/> and for the Recipes
        /// its spawn modifiers chain into, following the multicast and multiply generations those Recipes allow.
        /// Child counts estimate how many are in the air at once; the pool still creates on demand beyond them.
        /// </summary>
        public void Prewarm(Recipe recipe, int count)
        {
            ThrowIfDisposed();
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            PrewarmChain(
                recipe,
                count,
                ChainBudget.Start(recipe.MulticastGenerations),
                ChainBudget.Start(recipe.MultiplyGenerations),
                depth: 0);
        }

        /// <inheritdoc />
        public ProjectileId Spawn(in SpawnRequest request)
        {
            ThrowIfDisposed();
            ProjectileId id = SpawnCore(request);
            FlushPendingSpawns();
            return id;
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ThrowIfDisposed();
            if (dt < 0f)
                dt = 0f;

            ecsWorld.EntityManager.SetComponentData(deltaTimeEntity, new SimDeltaTime { Value = dt });
            groundFields.ClearPlantedThisTick();
            simulationGroup.Update();
            FlushPendingSpawns();
            groundFields.Tick(dt);
        }

        /// <inheritdoc />
        public void SetHomingCandidate(TargetId id, Vector3 position)
        {
            ThrowIfDisposed();
            homingCatalog.Set(id, position);
        }

        /// <inheritdoc />
        public void RemoveHomingCandidate(TargetId id)
        {
            ThrowIfDisposed();
            homingCatalog.Remove(id);
        }

        /// <inheritdoc />
        public void SetHitTarget(TargetId id, Vector3 position, float radius, ProtectionLevel protection = ProtectionLevel.None)
        {
            ThrowIfDisposed();
            hitTargets.Set(id, position, radius, protection);
        }

        /// <inheritdoc />
        public void RemoveHitTarget(TargetId id)
        {
            ThrowIfDisposed();
            hitTargets.Remove(id);
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

        /// <summary>
        /// Instance mana-cost copy after spawn and any <see cref="Mana"/> scales. The Recipe field is unchanged.
        /// </summary>
        public bool TryGetManaCost(ProjectileId id, out float manaCost)
        {
            return TryGetCombatStats(id, out _, out _, out manaCost, out _);
        }

        /// <summary>
        /// Instance radius after spawn and any <see cref="Size"/> scales. The Recipe field is unchanged.
        /// </summary>
        public bool TryGetRadius(ProjectileId id, out float radius)
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

                    radius = projectile.Radius;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            radius = 0f;
            return false;
        }

        internal bool TryGetGravity(ProjectileId id, out float gravity)
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

                    gravity = projectile.Gravity;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            gravity = 0f;
            return false;
        }

        internal bool TryGetSpread(ProjectileId id, out float spread)
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

                    spread = projectile.Spread;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            spread = 0f;
            return false;
        }

        internal bool TryGetCombatStats(
            ProjectileId id,
            out float damage,
            out float criticalChance,
            out float manaCost,
            out float effectRadius)
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

                    damage = projectile.Damage;
                    criticalChance = projectile.CriticalChance;
                    manaCost = projectile.ManaCost;
                    effectRadius = projectile.EffectRadius;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            damage = 0f;
            criticalChance = 0f;
            manaCost = 0f;
            effectRadius = 0f;
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

        internal bool TryGetHomingTarget(ProjectileId id, out HomingTarget target)
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

                    target = projectile.HomingHasTarget
                        ? HomingTarget.Assigned(projectile.HomingTargetId)
                        : default;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            target = default;
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

        /// <summary>Disposes the Entities world, pool views, and ground-field views.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            groundFields?.Dispose();
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
            int multicastLeft = request.MulticastChain.Resolve(recipe.MulticastGenerations);
            int multiplyLeft = request.MultiplyChain.Resolve(recipe.MultiplyGenerations);

            float3 position = new float3(request.Position.x, request.Position.y, request.Position.z);
            float3 velocity = new float3(direction.x, direction.y, direction.z) * recipe.Speed;

            EntityManager em = ecsWorld.EntityManager;
            em.SetComponentData(entity, new Projectile
            {
                Id = idValue,
                RecipeIndex = recipeIndex,
                Speed = recipe.Speed,
                Gravity = recipe.Gravity,
                Radius = recipe.Radius,
                Damage = recipe.Damage,
                CriticalChance = recipe.CriticalChance,
                ManaCost = recipe.ManaCost,
                EffectRadius = recipe.EffectRadius,
                Spread = ReadMultiplySpread(recipe),
                RemainingLifetime = recipe.LifetimeSeconds,
                ElapsedSeconds = 0f,
                IntervalSeconds = 0f,
                RemainingPierce = remainingPierce,
                RemainingBounce = remainingBounce,
                MultiplyGenerationsLeft = multiplyLeft,
                HomingHasTarget = request.HomingTarget.IsAssigned,
                HomingTargetId = request.HomingTarget.Id,
                HomingHasIgnoredTarget = false,
                HomingIgnoredTargetId = default,
                HomingAcquirePointSet = request.HomingAcquirePointSet,
                HomingAcquirePoint = request.HomingAcquirePointSet
                    ? new float3(request.HomingAcquirePoint.x, request.HomingAcquirePoint.y, request.HomingAcquirePoint.z)
                    : default,
                HomingAimPointUsed = false
            });
            em.SetComponentData(entity, new ProjectilePosition { Value = position });
            em.SetComponentData(entity, new ProjectileVelocity { Value = velocity });
            em.SetComponentData(entity, new ProjectileCandidate { Value = position });

            hitQuery.CopyIgnore(request.InheritIgnoreFrom, new ProjectileId(idValue));
            simulation.RunSpawnStages(entity, multicastLeft);
            if (view != null)
            {
                Projectile spawned = em.GetComponentData<Projectile>(entity);
                view.Bind(request.Position, spawned.Radius);
            }

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

        void PrewarmChain(Recipe recipe, int count, int multicastLeft, int multiplyLeft, int depth)
        {
            if (recipe == null || count <= 0)
                return;
            if (count > MaxPrewarmPerRecipe)
                count = MaxPrewarmPerRecipe;

            int index = recipes.GetOrAdd(recipe);
            pool.Prewarm(index, recipe, count);

            if (depth >= MaxPrewarmChainDepth)
                return;

            if (multicastLeft > 0)
                PrewarmMulticastExtras(recipe, count, multicastLeft, depth);

            if (multiplyLeft > 0)
            {
                PrewarmMultiplyChildren(recipe, recipe.OnInterval, count, multiplyLeft, depth, onInterval: true);
                PrewarmMultiplyChildren(recipe, recipe.OnHit, count, multiplyLeft, depth, onInterval: false);
                PrewarmMultiplyChildren(recipe, recipe.OnExpire, count, multiplyLeft, depth, onInterval: false);
            }
        }

        void PrewarmMulticastExtras(Recipe recipe, int count, int multicastLeft, int depth)
        {
            IModifier[] onSpawn = recipe.OnSpawn;
            if (onSpawn == null)
                return;

            for (int i = 0; i < onSpawn.Length; i++)
            {
                if (onSpawn[i] is not Multicast multicast || multicast.Count <= 1)
                    continue;

                Recipe extra = multicast.ExtraRecipe;
                PrewarmChain(
                    extra,
                    count * (multicast.Count - 1),
                    multicastLeft - 1,
                    ChainBudget.Start(extra.MultiplyGenerations),
                    depth + 1);
            }
        }

        void PrewarmMultiplyChildren(Recipe recipe, IModifier[] modifiers, int count, int multiplyLeft, int depth, bool onInterval)
        {
            if (modifiers == null)
                return;

            float intervalSeconds = onInterval ? ReadIntervalSeconds(recipe) : 0f;
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] is not Multiply multiply || multiply.Count <= 0)
                    continue;

                Recipe child = multiply.ChildRecipe;
                int perParent = onInterval
                    ? multiply.Count * IntervalSpawnsInFlight(recipe, child, intervalSeconds)
                    : multiply.Count;
                PrewarmChain(
                    child,
                    count * perParent,
                    ChainBudget.Start(child.MulticastGenerations),
                    multiplyLeft - 1,
                    depth + 1);
            }
        }

        /// <summary>Interval children one parent has in the air at once: spawns over a child lifetime, capped by the parent's.</summary>
        static int IntervalSpawnsInFlight(Recipe parent, Recipe child, float intervalSeconds)
        {
            if (intervalSeconds <= 0f)
                return 1;

            float window = Mathf.Min(parent.LifetimeSeconds, child.LifetimeSeconds);
            int spawns = Mathf.CeilToInt(window / intervalSeconds);
            return spawns > 1 ? spawns : 1;
        }

        static float ReadMultiplySpread(Recipe recipe)
        {
            float spread = 0f;
            spread = LastMultiplySpread(recipe.OnInterval, spread);
            spread = LastMultiplySpread(recipe.OnHit, spread);
            spread = LastMultiplySpread(recipe.OnExpire, spread);
            return spread;
        }

        static float LastMultiplySpread(IModifier[] modifiers, float current)
        {
            if (modifiers == null)
                return current;

            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] is Multiply multiply)
                    current = multiply.Spread;
            }

            return current;
        }

        /// <summary>The period an instance of <paramref name="recipe"/> will carry, from <see cref="Interval"/> on its OnSpawn list.</summary>
        static float ReadIntervalSeconds(Recipe recipe)
        {
            IModifier[] onSpawn = recipe.OnSpawn;
            if (onSpawn == null)
                return 0f;

            float seconds = 0f;
            for (int i = 0; i < onSpawn.Length; i++)
            {
                if (onSpawn[i] is Interval interval)
                    seconds = interval.Seconds > 0f ? interval.Seconds : 0f;
            }

            return seconds;
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
