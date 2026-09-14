using Unity.Entities;
using Unity.Mathematics;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    struct Projectile : IComponentData
    {
        public int Id;
        public int RecipeIndex;
        public float Speed;
        public float Radius;
        public float RemainingLifetime;
    }

    struct ProjectilePosition : IComponentData
    {
        public float3 Value;
    }

    struct ProjectileVelocity : IComponentData
    {
        public float3 Value;
    }

    struct ProjectileCandidate : IComponentData
    {
        public float3 Value;
    }

    struct SimDeltaTime : IComponentData
    {
        public float Value;
    }
}
