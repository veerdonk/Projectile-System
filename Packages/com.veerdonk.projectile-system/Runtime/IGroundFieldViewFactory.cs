using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Creates a GameObject view for a planted <see cref="GroundField"/>. The mesh is display only.
    /// The world places a disk's view at its center, turned up to the ground normal. A rectangle's or sector's view is
    /// placed at its anchor (<see cref="GroundFieldInfo.Center"/>) with forward along <see cref="GroundFieldInfo.Direction"/>
    /// and up along the normal; views are pooled per presentation id and shape.
    /// </summary>
    public interface IGroundFieldViewFactory
    {
        /// <summary>
        /// Creates a view for a field of <paramref name="radius"/> meters (<see cref="GroundFieldInfo.Radius"/>, the reach of a
        /// rectangle or sector). Unknown ids use the primitive.
        /// </summary>
        ProjectileView Create(float radius, string presentationId, Transform poolParent);
    }

    /// <summary>
    /// Default factory: a thin cylinder scaled to <c>2 * radius</c> on XZ. For a rectangle or sector field the world swaps
    /// its mesh for a thin box or a flat sector and scales it to the shape.
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
