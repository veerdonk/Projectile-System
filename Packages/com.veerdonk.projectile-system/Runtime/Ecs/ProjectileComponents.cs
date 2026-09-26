using Unity.Entities;
using Unity.Mathematics;

namespace Veerdonk.ProjectileSystem
{
    struct Projectile : IComponentData
    {
        public int Id;
        public int RecipeIndex;
        public float Speed;
        public float Gravity;
        public float Radius;
        public float Damage;
        public float CriticalChance;
        public float ManaCost;
        public float EffectRadius;
        public float Spread;
        public float RemainingLifetime;
        public float ElapsedSeconds;
        public float IntervalSeconds;
        public int RemainingPierce;
        public int RemainingBounce;
        public int MultiplyGenerationsLeft;
        public bool HomingHasTarget;
        public TargetId HomingTargetId;
        public bool HomingHasIgnoredTarget;
        public TargetId HomingIgnoredTargetId;
        public bool HomingAcquirePointSet;
        public float3 HomingAcquirePoint;
        public bool HomingAimPointUsed;
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
