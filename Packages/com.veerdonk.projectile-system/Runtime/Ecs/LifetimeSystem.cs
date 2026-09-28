using Unity.Burst;
using Unity.Entities;

namespace Veerdonk.ProjectileSystem
{
    [DisableAutoCreation]
    [BurstCompile]
    partial struct LifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimDeltaTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.GetSingleton<SimDeltaTime>().Value;
            new LifetimeJob { Dt = dt }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct LifetimeJob : IJobEntity
    {
        public float Dt;

        void Execute(ref Projectile projectile)
        {
            projectile.RemainingLifetime -= Dt;
            projectile.ElapsedSeconds += Dt;
            projectile.BeamPulseElapsed += Dt;
        }
    }
}
