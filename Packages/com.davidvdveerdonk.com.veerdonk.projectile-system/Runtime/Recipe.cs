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

        public IModifier[] OnSpawn { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnTravel { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnTick { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnInterval { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnHit { get; set; } = Array.Empty<IModifier>();

        public IModifier[] OnExpire { get; set; } = Array.Empty<IModifier>();
    }
}
