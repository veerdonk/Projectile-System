using System;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Named gameplay condition requested on a Target. The package does not simulate it.</summary>
    public interface IStatusEffect
    {
        /// <summary>Stable name the game HUD can show. v1 dummy is <c>dummy</c>.</summary>
        string Id { get; }
    }

    /// <summary>v1 placeholder effect. <see cref="Id"/> is <c>dummy</c>.</summary>
    [Serializable]
    public sealed class DummyStatusEffect : IStatusEffect
    {
        /// <summary>Value of <see cref="Id"/>.</summary>
        public const string EffectId = "dummy";

        /// <inheritdoc />
        public string Id => EffectId;
    }
}
