using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class LiveProjectile
    {
        public ProjectileId Id;
        public Recipe Recipe;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Speed;
        public float Radius;
        public float RemainingLifetime;
        public bool Alive;
        public ProjectileView View;
    }
}
