using System;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Which of the six Recipe lists a modifier may appear on.</summary>
    [Flags]
    public enum RecipeStage
    {
        OnSpawn = 1,
        OnTravel = 2,
        OnTick = 4,
        OnInterval = 8,
        OnHit = 16,
        OnExpire = 32
    }

    /// <summary>
    /// Declares the Recipe lists that may add this <see cref="IModifier"/> type.
    /// Types with no attribute do not appear in any Add menu.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModifierStageAttribute : Attribute
    {
        /// <summary>Stages whose Add menus list this type.</summary>
        public ModifierStageAttribute(RecipeStage stages)
        {
            Stages = stages;
        }

        /// <summary>Bit flags of allowed lists.</summary>
        public RecipeStage Stages { get; }
    }
}
