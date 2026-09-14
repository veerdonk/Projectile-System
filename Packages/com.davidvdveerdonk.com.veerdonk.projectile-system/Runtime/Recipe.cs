using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>Authored definition: base stats plus six ordered modifier lists.</summary>
    public sealed class Recipe
    {
        public string Id { get; set; }

        public float Speed { get; set; }

        public float Radius { get; set; } = 0.25f;

        public float LifetimeSeconds { get; set; }

        public string PresentationId { get; set; }

        public IModifier[] OnSpawn { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnTravel { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnTick { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnInterval { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnHit { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnExpire { get; set; } = Array.Empty<IModifier>();
    }
}
