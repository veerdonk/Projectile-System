using System;
using System.Reflection;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Authored definition: base stats plus six ordered modifier lists.
    /// <see cref="Clone"/>, <see cref="Append(RecipeStage, IModifier)"/>, and <see cref="Compose"/> build a fire-time
    /// variant from a base plus <see cref="ModifierKit"/>s. Each composition needs its own <see cref="Id"/>.
    /// </summary>
    public sealed class Recipe
    {
        /// <summary>
        /// Store key. A later <see cref="IProjectileWorld.Spawn"/> with the same id replaces that slot,
        /// so each <see cref="Compose"/> result needs its own id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// How the shot is delivered. Default Straight keeps existing Recipes level.
        /// Arc applies gravity and, when the aim point is set, lofts at <see cref="IProjectileWorld.Spawn"/>.
        /// Hitscan is one polyline query inside Spawn. Homing <c>n</c> on Hitscan and Beam is degrees of kink per segment.
        /// Beam stays at the spawn pose and pulses that polyline until lifetime or <see cref="IProjectileWorld.Despawn"/>.
        /// </summary>
        public Delivery Delivery { get; set; }

        /// <summary>
        /// Seconds between Beam pulses. Default 0.1. Values &lt;= 0 pulse on every <see cref="IProjectileWorld.Tick"/>
        /// as well as the pulse inside <see cref="IProjectileWorld.Spawn"/>. Not the <see cref="Interval"/> clock.
        /// </summary>
        public float BeamPulseSeconds { get; set; } = 0.1f;

        /// <summary>
        /// Caps the total polyline length in meters of each Beam pulse and each Hitscan query. Default 0;
        /// values &lt;= 0 keep the 10 km default. Pierce, Bounce, and homing curve segments share the one
        /// budget. The last SphereCast stops at the cap, so reach is <c>MaxRange + Radius</c>. The Beam view
        /// ends at the cap; a Hitscan that reaches it expires as <see cref="ExpireReason.HitscanEnd"/>.
        /// Straight and Arc ignore it. No modifier scales it.
        /// </summary>
        public float MaxRange { get; set; }

        /// <summary>Launch speed in meters per second. Copied onto the instance at spawn.</summary>
        public float Speed { get; set; }

        /// <summary>SphereCast radius in meters. Default 0.25. <see cref="Size"/> scales the instance copy.</summary>
        public float Radius { get; set; } = 0.25f;

        /// <summary>Seconds until expire if the instance is still live. Copied onto the instance at spawn.</summary>
        public float LifetimeSeconds { get; set; }

        /// <summary>
        /// Downward acceleration in m/s². Default 0. Copied onto the instance at spawn.
        /// Burst integrates it each tick only when <see cref="Delivery"/> is Arc,
        /// using world down (0,-1,0), not <c>Physics.gravity</c>.
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

        /// <summary>
        /// Independent copy of stats and lists. <see cref="Id"/> is copied until the caller changes it.
        /// Modifiers are cloned with the same path as <see cref="RecipeAsset.ToRecipe"/>.
        /// </summary>
        public Recipe Clone()
        {
            return new Recipe
            {
                Id = Id,
                Delivery = Delivery,
                BeamPulseSeconds = BeamPulseSeconds,
                MaxRange = MaxRange,
                Speed = Speed,
                Radius = Radius,
                LifetimeSeconds = LifetimeSeconds,
                Gravity = Gravity,
                Damage = Damage,
                CriticalChance = CriticalChance,
                ManaCost = ManaCost,
                EffectRadius = EffectRadius,
                PresentationId = PresentationId,
                MulticastGenerations = MulticastGenerations,
                MultiplyGenerations = MultiplyGenerations,
                OnSpawn = RecipeAsset.CloneModifierList(OnSpawn),
                OnTravel = RecipeAsset.CloneModifierList(OnTravel),
                OnTick = RecipeAsset.CloneModifierList(OnTick),
                OnInterval = RecipeAsset.CloneModifierList(OnInterval),
                OnHit = RecipeAsset.CloneModifierList(OnHit),
                OnExpire = RecipeAsset.CloneModifierList(OnExpire)
            };
        }

        /// <summary>
        /// Appends <paramref name="modifier"/> to the one list named by <paramref name="stage"/>.
        /// Throws if <paramref name="modifier"/> is null, <paramref name="stage"/> is not exactly one of the six
        /// lists, or the modifier's <see cref="ModifierStageAttribute"/> does not include that list.
        /// Does not insert <see cref="Interval"/>.
        /// </summary>
        public void Append(RecipeStage stage, IModifier modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            Append(stage, new[] { modifier });
        }

        /// <summary>Appends each modifier, in order, after the same checks as <see cref="Append(RecipeStage, IModifier)"/>.</summary>
        public void Append(RecipeStage stage, IModifier[] modifiers)
        {
            if (modifiers == null)
                throw new ArgumentNullException(nameof(modifiers));

            EnsureSingleStage(stage);
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] == null)
                    throw new ArgumentNullException(nameof(modifiers));
                EnsureAllowed(stage, modifiers[i]);
            }

            if (modifiers.Length == 0)
                return;

            SetList(stage, Join(List(stage), modifiers));
        }

        /// <summary>
        /// Clones <paramref name="basis"/>, sets <see cref="Id"/> to <paramref name="id"/>, and applies
        /// <paramref name="kits"/> in order. Throws if <paramref name="basis"/> is null or <paramref name="id"/> is
        /// null or empty. A null or empty kit is skipped. Reusing <paramref name="id"/> on <see cref="IProjectileWorld.Spawn"/>
        /// replaces the stored Recipe, so each composition needs its own id.
        /// An <see cref="OnInterval"/> extra does nothing unless <see cref="Interval"/> is already on the base
        /// or on an <see cref="RecipeStage.OnSpawn"/> layer. This method does not insert one.
        /// </summary>
        public static Recipe Compose(Recipe basis, string id, params ModifierKit[] kits)
        {
            if (basis == null)
                throw new ArgumentNullException(nameof(basis));
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Recipe.Id is required.", nameof(id));

            Recipe copy = basis.Clone();
            copy.Id = id;
            if (kits == null)
                return copy;

            for (int i = 0; i < kits.Length; i++)
            {
                if (kits[i] == null)
                    continue;
                kits[i].ApplyTo(copy);
            }

            return copy;
        }

        IModifier[] List(RecipeStage stage)
        {
            switch (stage)
            {
                case RecipeStage.OnSpawn: return OnSpawn;
                case RecipeStage.OnTravel: return OnTravel;
                case RecipeStage.OnTick: return OnTick;
                case RecipeStage.OnInterval: return OnInterval;
                case RecipeStage.OnHit: return OnHit;
                case RecipeStage.OnExpire: return OnExpire;
                default:
                    throw new ArgumentException("Stage must be exactly one of the six Recipe lists.", nameof(stage));
            }
        }

        void SetList(RecipeStage stage, IModifier[] list)
        {
            switch (stage)
            {
                case RecipeStage.OnSpawn: OnSpawn = list; break;
                case RecipeStage.OnTravel: OnTravel = list; break;
                case RecipeStage.OnTick: OnTick = list; break;
                case RecipeStage.OnInterval: OnInterval = list; break;
                case RecipeStage.OnHit: OnHit = list; break;
                case RecipeStage.OnExpire: OnExpire = list; break;
                default:
                    throw new ArgumentException("Stage must be exactly one of the six Recipe lists.", nameof(stage));
            }
        }

        static void EnsureSingleStage(RecipeStage stage)
        {
            int value = (int)stage;
            bool oneBit = value > 0 && (value & (value - 1)) == 0;
            if (!oneBit || !Enum.IsDefined(typeof(RecipeStage), stage))
                throw new ArgumentException("Stage must be exactly one of the six Recipe lists.", nameof(stage));
        }

        static void EnsureAllowed(RecipeStage stage, IModifier modifier)
        {
            ModifierStageAttribute attribute = modifier.GetType().GetCustomAttribute<ModifierStageAttribute>();
            if (attribute == null || (attribute.Stages & stage) == 0)
                throw new ArgumentException($"{modifier.GetType().Name} cannot be appended to {stage}.", nameof(modifier));
        }

        static IModifier[] Join(IModifier[] source, IModifier[] extra)
        {
            if (source == null || source.Length == 0)
            {
                var only = new IModifier[extra.Length];
                Array.Copy(extra, only, extra.Length);
                return only;
            }

            var next = new IModifier[source.Length + extra.Length];
            Array.Copy(source, next, source.Length);
            Array.Copy(extra, 0, next, source.Length, extra.Length);
            return next;
        }
    }
}
