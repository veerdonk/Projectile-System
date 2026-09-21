using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// GameObject presentation for one instance. Prefabs may already carry this;
    /// the factory adds it if missing. Does not write simulation state.
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        bool scaleToRadius = true;

        internal bool ScaleToRadius
        {
            get => scaleToRadius;
            set => scaleToRadius = value;
        }

        /// <summary>Activates the view at <paramref name="position"/>. Primitive factories also apply radius scale.</summary>
        public void Bind(Vector3 position, float radius)
        {
            transform.position = position;
            SetRadius(radius);
            gameObject.SetActive(true);
        }

        /// <summary>Scales the primitive mesh to <c>2 * radius</c>. Prefab views ignore this.</summary>
        public void SetRadius(float radius)
        {
            if (!scaleToRadius)
                return;

            float diameter = Mathf.Max(radius, 0.01f) * 2f;
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        /// <summary>Moves the view and looks along <paramref name="velocity"/> when it is non-zero.</summary>
        public void SetPose(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            if (velocity.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.LookRotation(velocity);
        }

        /// <summary>Hides the view for return to the pool. Does not Destroy.</summary>
        public void Release()
        {
            gameObject.SetActive(false);
        }
    }
}
