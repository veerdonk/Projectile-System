using System;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
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

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModifierStageAttribute : Attribute
    {
        public ModifierStageAttribute(RecipeStage stages)
        {
            Stages = stages;
        }

        public RecipeStage Stages { get; }
    }
}
