using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem.Editor.Tests
{
    static class SimulationTestHelper
    {
        public static Recipe CreateRecipe(
            string id,
            float speed = 10f,
            float lifetime = 10f,
            float radius = 0.25f,
            IModifier[] onHit = null)
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = radius,
                LifetimeSeconds = lifetime,
                PresentationId = "sphere",
                OnHit = onHit ?? Array.Empty<IModifier>()
            };
        }

        public static (ProjectileWorld world, ScriptedHitQuery hits) CreateWorld(Recipe recipe, int prewarm = 4)
        {
            var hits = new ScriptedHitQuery();
            var world = new ProjectileWorld(hits, createViews: false);
            world.Prewarm(recipe, prewarm);
            return (world, hits);
        }
    }
}
