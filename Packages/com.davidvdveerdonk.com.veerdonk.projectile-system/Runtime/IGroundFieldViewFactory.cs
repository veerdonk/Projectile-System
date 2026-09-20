using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Creates a GameObject view for a planted <see cref="GroundField"/>.
    /// Overlap stays the XZ disk of the modifier radius; the mesh is display only.
    /// </summary>
    public interface IGroundFieldViewFactory
    {
        ProjectileView Create(float radius, Transform poolParent);
    }

    /// <summary>
    /// Default factory: a thin cylinder scaled to <c>2 * radius</c> on XZ.
    /// </summary>
    public sealed class PrimitiveGroundFieldViewFactory : IGroundFieldViewFactory
    {
        public const float DiskHalfHeight = 0.025f;

        public ProjectileView Create(float radius, Transform poolParent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (poolParent != null)
                go.transform.SetParent(poolParent, false);

            var view = go.GetComponent<ProjectileView>();
            if (view == null)
                view = go.AddComponent<ProjectileView>();
            view.ScaleToRadius = true;
            go.transform.localScale = new Vector3(radius * 2f, DiskHalfHeight, radius * 2f);
            return view;
        }
    }
}
