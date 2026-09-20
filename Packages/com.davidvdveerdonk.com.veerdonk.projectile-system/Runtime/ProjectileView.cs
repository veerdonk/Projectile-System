using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
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

        public void Bind(Vector3 position, float radius)
        {
            transform.position = position;
            SetRadius(radius);
            gameObject.SetActive(true);
        }

        public void SetRadius(float radius)
        {
            if (!scaleToRadius)
                return;

            float diameter = Mathf.Max(radius, 0.01f) * 2f;
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        public void SetPose(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            if (velocity.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.LookRotation(velocity);
        }

        public void Release()
        {
            gameObject.SetActive(false);
        }
    }
}
