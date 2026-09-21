using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Creates a GameObject view for a planted <see cref="GroundField"/>.
    /// Overlap stays the XZ disk of the modifier radius; the mesh is display only.
    /// </summary>
    public interface IGroundFieldViewFactory
    {
        /// <summary>Creates a disk view of <paramref name="radius"/> meters. Unknown ids use the primitive cylinder.</summary>
        ProjectileView Create(float radius, string presentationId, Transform poolParent);
    }

    /// <summary>
    /// Default factory: a thin cylinder scaled to <c>2 * radius</c> on XZ.
    /// </summary>
    public sealed class PrimitiveGroundFieldViewFactory : IGroundFieldViewFactory
    {
        /// <summary>Half-height of the primitive cylinder, in meters.</summary>
        public const float DiskHalfHeight = 0.025f;

        /// <inheritdoc />
        public ProjectileView Create(float radius, string presentationId, Transform poolParent)
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
