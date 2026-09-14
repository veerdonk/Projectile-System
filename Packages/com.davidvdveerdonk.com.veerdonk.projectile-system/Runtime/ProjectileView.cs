using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class ProjectileView : MonoBehaviour
    {
        public void Bind(Vector3 position, float radius)
        {
            transform.position = position;
            float diameter = Mathf.Max(radius, 0.01f) * 2f;
            transform.localScale = new Vector3(diameter, diameter, diameter);
            gameObject.SetActive(true);
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
