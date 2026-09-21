using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Instantiates a prefab for <see cref="GroundField.PresentationId"/>. Does not overwrite
    /// <c>localScale</c> — author the prefab at world scale. Unknown ids use
    /// <see cref="PrimitiveGroundFieldViewFactory"/>.
    /// </summary>
    public sealed class PrefabGroundFieldViewFactory : IGroundFieldViewFactory
    {
        readonly IReadOnlyDictionary<string, GameObject> prefabsById;
        readonly IGroundFieldViewFactory fallback;

        /// <summary>Maps <see cref="GroundField.PresentationId"/> to prefabs. Missing ids use <paramref name="fallback"/>.</summary>
        public PrefabGroundFieldViewFactory(
            IReadOnlyDictionary<string, GameObject> prefabsById,
            IGroundFieldViewFactory fallback = null)
        {
            this.prefabsById = prefabsById ?? throw new ArgumentNullException(nameof(prefabsById));
            this.fallback = fallback ?? new PrimitiveGroundFieldViewFactory();
        }

        /// <inheritdoc />
        public ProjectileView Create(float radius, string presentationId, Transform poolParent)
        {
            if (string.IsNullOrEmpty(presentationId) ||
                !prefabsById.TryGetValue(presentationId, out GameObject prefab) ||
                prefab == null)
            {
                return fallback.Create(radius, presentationId, poolParent);
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
