using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Creates a GameObject view for a Recipe. Hits stay SphereCast of <see cref="Recipe.Radius"/>;
    /// the mesh is display only.
    /// </summary>
    public interface IProjectileViewFactory
    {
        /// <summary>Creates or configures a view for <paramref name="recipe"/>. Parent under <paramref name="poolParent"/> when set.</summary>
        ProjectileView Create(Recipe recipe, Transform poolParent);
    }

    /// <summary>
    /// Default factory: a sphere primitive scaled to <c>2 * Radius</c>, same as the original pool.
    /// </summary>
    public sealed class PrimitiveViewFactory : IProjectileViewFactory
    {
        /// <inheritdoc />
        public ProjectileView Create(Recipe recipe, Transform poolParent)
        {
            if (recipe == null)
                throw new System.ArgumentNullException(nameof(recipe));

            if (recipe.Delivery == Delivery.Beam)
            {
                var root = new GameObject("Beam");
                if (poolParent != null)
                    root.transform.SetParent(poolParent, false);

                var beamView = root.AddComponent<ProjectileView>();
                beamView.ScaleToRadius = false;
                return beamView;
            }

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
