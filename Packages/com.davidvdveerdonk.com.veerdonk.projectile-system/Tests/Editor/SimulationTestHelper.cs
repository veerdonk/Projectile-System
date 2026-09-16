using System;
using System.Collections.Generic;
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
            IModifier[] onTravel = null,
            IModifier[] onTick = null,
            IModifier[] onHit = null,
            IModifier[] onInterval = null,
            IModifier[] onExpire = null)
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = radius,
                LifetimeSeconds = lifetime,
                PresentationId = "sphere",
                OnTravel = onTravel ?? Array.Empty<IModifier>(),
                OnTick = onTick ?? Array.Empty<IModifier>(),
                OnHit = onHit ?? Array.Empty<IModifier>(),
                OnInterval = onInterval ?? Array.Empty<IModifier>(),
                OnExpire = onExpire ?? Array.Empty<IModifier>()
            };
        }

        public static List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)> CollectActive(
            ProjectileWorld world)
        {
            var results = new List<(ProjectileId id, Vector3 position, Vector3 velocity, Recipe recipe)>();
            for (int i = 0; ; i++)
            {
                if (!world.TryGetActiveAt(i, out ProjectileId id, out Vector3 position, out Vector3 velocity, out Recipe recipe))
                    break;
                results.Add((id, position, velocity, recipe));
            }

            return results;
        }

        public static Vector3 OffSurface(Vector3 point, Vector3 direction, float radius)
        {
            Vector3 along = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            return point + along * (radius + 0.01f);
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
