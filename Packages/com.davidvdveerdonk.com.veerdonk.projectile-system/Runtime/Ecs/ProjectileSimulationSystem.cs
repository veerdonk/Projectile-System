using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class SimulationBridge
    {
        public IHitQuery HitQuery;
        public RecipeStore Recipes;
        public ProjectilePool Pool;
        public ModifierContext ModifierContext;
        public System.Action<HitInfo> Hit;
        public System.Action<ExpireInfo> Expired;
    }

    [DisableAutoCreation]
    partial class ProjectileSimulationSystem : SystemBase
    {
        public SimulationBridge Bridge;

        protected override void OnUpdate()
        {
            Dependency.Complete();
            if (Bridge == null)
                return;

            float dt = SystemAPI.GetSingleton<SimDeltaTime>().Value;
            EntityManager em = EntityManager;

            var expire = new NativeList<Entity>(Allocator.Temp);
            foreach (var (projectile, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
            {
                if (projectile.ValueRO.RemainingLifetime <= 0f)
                    expire.Add(entity);
            }

            for (int i = 0; i < expire.Length; i++)
                Despawn(expire[i], runOnHit: false, default);
            expire.Dispose();

            var tickList = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
                tickList.Add(entity);

            for (int i = 0; i < tickList.Length; i++)
            {
                Entity entity = tickList[i];
                Projectile projectile = em.GetComponentData<Projectile>(entity);
                Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
                RunStage(recipe.OnTick, entity, hasHit: false, default);
            }

            tickList.Dispose();

            new IntegrateJob { Dt = dt }.ScheduleParallel();
            Dependency.Complete();

            var moveList = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
                moveList.Add(entity);

            for (int i = 0; i < moveList.Length; i++)
                TryMoveOrHit(moveList[i]);
            moveList.Dispose();
        }

        internal void RunSpawnStages(Entity entity)
        {
            Projectile projectile = EntityManager.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);
            RunStage(recipe.OnSpawn, entity, hasHit: false, default);
            RunStage(recipe.OnTravel, entity, hasHit: false, default);
        }

        void TryMoveOrHit(Entity entity)
        {
            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectilePosition position = em.GetComponentData<ProjectilePosition>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
            ProjectileCandidate candidate = em.GetComponentData<ProjectileCandidate>(entity);

            Vector3 from = ToVector3(position.Value);
            Vector3 to = ToVector3(candidate.Value);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            Vector3 direction = distance > 1e-8f ? delta / distance : ToVector3(velocity.Value);

            if (Bridge.HitQuery.TryCast(
                    new ProjectileId(projectile.Id),
                    from,
                    direction,
                    distance,
                    projectile.Radius,
                    out HitInfo hit))
            {
                Bridge.Hit?.Invoke(hit);
                Despawn(entity, runOnHit: true, hit);
                return;
            }

            em.SetComponentData(entity, new ProjectilePosition { Value = candidate.Value });
            if (Bridge.Pool.TryGetView(entity, out ProjectileView view))
                view.SetPose(to, ToVector3(velocity.Value));
        }

        void Despawn(Entity entity, bool runOnHit, in HitInfo hit)
        {
            EntityManager em = EntityManager;
            if (!em.Exists(entity) || !em.IsEnabled(entity))
                return;

            Projectile projectile = em.GetComponentData<Projectile>(entity);
            Recipe recipe = Bridge.Recipes.Get(projectile.RecipeIndex);

            if (runOnHit)
            {
                RunStage(recipe.OnHit, entity, hasHit: true, hit);
                if (Bridge.ModifierContext.CancelDespawn)
                {
                    if (Bridge.Pool.TryGetView(entity, out ProjectileView view))
                    {
                        ProjectilePosition position = em.GetComponentData<ProjectilePosition>(entity);
                        ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);
                        view.SetPose(ToVector3(position.Value), ToVector3(velocity.Value));
                    }

                    return;
                }
            }

            RunStage(recipe.OnExpire, entity, hasHit: false, default);

            int id = em.GetComponentData<Projectile>(entity).Id;
            int recipeIndex = em.GetComponentData<Projectile>(entity).RecipeIndex;
            Bridge.Pool.Return(recipeIndex, entity);
            Bridge.Expired?.Invoke(new ExpireInfo { InstanceId = new ProjectileId(id) });
        }

        void RunStage(IModifier[] modifiers, Entity entity, bool hasHit, in HitInfo hit)
        {
            if (modifiers == null || modifiers.Length == 0)
                return;

            EntityManager em = EntityManager;
            Projectile projectile = em.GetComponentData<Projectile>(entity);
            ProjectilePosition position = em.GetComponentData<ProjectilePosition>(entity);
            ProjectileVelocity velocity = em.GetComponentData<ProjectileVelocity>(entity);

            Bridge.ModifierContext.Bind(
                new ProjectileId(projectile.Id),
                ToVector3(position.Value),
                ToVector3(velocity.Value),
                projectile.Speed,
                projectile.RemainingLifetime,
                projectile.Radius,
                hasHit,
                hit);

            for (int i = 0; i < modifiers.Length; i++)
            {
                IModifier modifier = modifiers[i];
                if (modifier == null)
                    continue;
                modifier.Apply(Bridge.ModifierContext);
            }

            Bridge.ModifierContext.ApplyTo(ref projectile, ref velocity);
            em.SetComponentData(entity, projectile);
            em.SetComponentData(entity, velocity);
        }

        static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }

    [BurstCompile]
    partial struct IntegrateJob : IJobEntity
    {
        public float Dt;

        void Execute(in ProjectilePosition position, in ProjectileVelocity velocity, ref ProjectileCandidate candidate)
        {
            candidate.Value = position.Value + velocity.Value * Dt;
        }
    }
}
