using System;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Authored definition: base stats plus six ordered modifier lists.</summary>
    public sealed class Recipe
    {
        /// <summary>Game identifier. Not used by the simulation.</summary>
        public string Id { get; set; }

        /// <summary>Launch speed in meters per second. Copied onto the instance at spawn.</summary>
        public float Speed { get; set; }

        /// <summary>SphereCast radius in meters. Default 0.25. <see cref="Size"/> scales the instance copy.</summary>
        public float Radius { get; set; } = 0.25f;

        /// <summary>Seconds until expire if the instance is still live. Copied onto the instance at spawn.</summary>
        public float LifetimeSeconds { get; set; }

        /// <summary>
        /// Downward acceleration in m/s². Default 0 is a straight shot. Copied onto the instance at spawn.
        /// Burst integrates it each tick using world down (0,-1,0), not <c>Physics.gravity</c>.
        /// Values &lt;= 0 keep the straight <c>position + velocity * dt</c> path.
        /// </summary>
        public float Gravity { get; set; }

        /// <summary>Hit amount copied onto the instance and onto <see cref="HitInfo"/>. The package never applies HP.</summary>
        public float Damage { get; set; } = 1f;

        /// <summary>Probability 0–1. The package never rolls a crit.</summary>
        public float CriticalChance { get; set; }

        /// <summary>
        /// Authored fire-time cost. Copied onto the instance at spawn so <see cref="Mana"/> can scale
        /// that copy; this field stays the authored value. The package never spends mana.
        /// </summary>
        public float ManaCost { get; set; }

        /// <summary>
        /// Radius of the <see cref="AreaEffect"/> overlap sphere. Distinct from <see cref="Radius"/>.
        /// Copied onto the instance at spawn so the EffectRadius modifier can scale that copy.
        /// </summary>
        public float EffectRadius { get; set; }

        /// <summary>Selects the GameObject view via <see cref="IProjectileViewFactory"/>. Unknown ids use the primitive sphere.</summary>
        public string PresentationId { get; set; }

        /// <summary>
        /// How many further generations of <see cref="Multicast"/> the extras of this Recipe may run.
        /// 0 (the default) skips <see cref="Multicast"/> on extras, so a spawn expands once. 1 lets each extra
        /// multicast once more; their own extras then skip. Counting down like this is what keeps a Recipe whose
        /// <see cref="Multicast"/> spawns itself from expanding forever.
        /// </summary>
        public int MulticastGenerations { get; set; }

        /// <summary>
        /// The same budget for <see cref="Multiply"/>: how many further generations the children of this Recipe may
        /// multiply. 0 (the default) means children do not inherit multiply, whatever their own Recipe lists.
        /// </summary>
        public int MultiplyGenerations { get; set; }

        /// <summary>Runs once at spawn. Multicast and Interval live here.</summary>
        public IModifier[] OnSpawn { get; set; } = Array.Empty<IModifier>();

        /// <summary>Runs once before the first move. Start scales, OnTravel AreaEffect / GroundField.</summary>
        public IModifier[] OnTravel { get; set; } = Array.Empty<IModifier>();

        /// <summary>Runs every tick while live. Homing lives here.</summary>
        public IModifier[] OnTick { get; set; } = Array.Empty<IModifier>();

        /// <summary>Runs on each Interval crossing. Multiply, scales, AreaEffect aura.</summary>
        public IModifier[] OnInterval { get; set; } = Array.Empty<IModifier>();

        /// <summary>Runs on a classified hit. Pierce, Bounce, Multiply, ApplyStatus, AreaEffect, GroundField.</summary>
        public IModifier[] OnHit { get; set; } = Array.Empty<IModifier>();

        /// <summary>Runs when the instance returns to the pool, except Strong protection (no OnExpire).</summary>
        public IModifier[] OnExpire { get; set; } = Array.Empty<IModifier>();
    }
}
