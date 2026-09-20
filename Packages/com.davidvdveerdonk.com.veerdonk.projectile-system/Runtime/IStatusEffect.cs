using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>Named gameplay condition requested on a Target. The package does not simulate it.</summary>
    public interface IStatusEffect
    {
        string Id { get; }
    }

    /// <summary>v1 placeholder effect. <see cref="Id"/> is <c>dummy</c>.</summary>
    [Serializable]
    public sealed class DummyStatusEffect : IStatusEffect
    {
        public const string EffectId = "dummy";

        public string Id => EffectId;
    }
}
