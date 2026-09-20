using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Creates a GameObject view for a Recipe. Hits stay SphereCast of <see cref="Recipe.Radius"/>;
    /// the mesh is display only.
    /// </summary>
    public interface IProjectileViewFactory
    {
        ProjectileView Create(Recipe recipe, Transform poolParent);
    }

    /// <summary>
    /// Default factory: a sphere primitive scaled to <c>2 * Radius</c>, same as the original pool.
    /// </summary>
    public sealed class PrimitiveViewFactory : IProjectileViewFactory
    {
        public ProjectileView Create(Recipe recipe, Transform poolParent)
        {
            if (recipe == null)
                throw new System.ArgumentNullException(nameof(recipe));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (poolParent != null)
                go.transform.SetParent(poolParent, false);

            var view = go.GetComponent<ProjectileView>();
            if (view == null)
                view = go.AddComponent<ProjectileView>();
            view.ScaleToRadius = true;
            view.SetRadius(recipe.Radius);
            return view;
        }
    }
}
