using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Visible muzzle for the firing lane. Phase 0 only records pose; the package world is phase 1.
    /// </summary>
    public sealed class SandboxSpawnPoint : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public Vector3 Direction => transform.forward;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawRay(transform.position, transform.forward * 2.5f);
        }
    }
}
