using System;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Instantiates a prefab for every planted field. Does not overwrite
    /// <c>localScale</c> — author the prefab at world scale. A null prefab uses
    /// <see cref="PrimitiveGroundFieldViewFactory"/>.
    /// </summary>
    public sealed class PrefabGroundFieldViewFactory : IGroundFieldViewFactory
    {
        readonly GameObject prefab;
        readonly IGroundFieldViewFactory fallback;

        public PrefabGroundFieldViewFactory(
            GameObject prefab,
            IGroundFieldViewFactory fallback = null)
        {
            this.prefab = prefab;
            this.fallback = fallback ?? new PrimitiveGroundFieldViewFactory();
        }

        public ProjectileView Create(float radius, Transform poolParent)
        {
            if (prefab == null)
                return fallback.Create(radius, poolParent);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, poolParent);
            var view = instance.GetComponent<ProjectileView>();
            if (view == null)
                view = instance.AddComponent<ProjectileView>();
            view.ScaleToRadius = false;
            return view;
        }
    }
}
