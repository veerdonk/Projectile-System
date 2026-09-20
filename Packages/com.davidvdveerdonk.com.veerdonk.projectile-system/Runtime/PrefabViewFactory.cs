using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Instantiates a prefab for <see cref="Recipe.PresentationId"/>. Does not overwrite
    /// <c>localScale</c> — author the prefab at world scale. Unknown ids use
    /// <see cref="PrimitiveViewFactory"/>.
    /// </summary>
    public sealed class PrefabViewFactory : IProjectileViewFactory
    {
        readonly IReadOnlyDictionary<string, GameObject> prefabsById;
        readonly IProjectileViewFactory fallback;

        public PrefabViewFactory(
            IReadOnlyDictionary<string, GameObject> prefabsById,
            IProjectileViewFactory fallback = null)
        {
            this.prefabsById = prefabsById ?? throw new ArgumentNullException(nameof(prefabsById));
            this.fallback = fallback ?? new PrimitiveViewFactory();
        }

        public ProjectileView Create(Recipe recipe, Transform poolParent)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            if (string.IsNullOrEmpty(recipe.PresentationId) ||
                !prefabsById.TryGetValue(recipe.PresentationId, out GameObject prefab) ||
                prefab == null)
            {
                return fallback.Create(recipe, poolParent);
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, poolParent);
            var view = instance.GetComponent<ProjectileView>();
            if (view == null)
                view = instance.AddComponent<ProjectileView>();
            view.ScaleToRadius = false;
            return view;
        }
    }
}
