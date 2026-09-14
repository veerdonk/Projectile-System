using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Sandbox cube that will listen to package <c>Hit</c> events in phase 1. HP stays out of the runtime package.
    /// </summary>
    public sealed class SandboxDummyTarget : MonoBehaviour
    {
        [SerializeField] int maxHp = 10;
        [SerializeField] int hp = 10;

        public int MaxHp => maxHp;
        public int Hp => hp;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.25f, 0.2f, 0.35f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
        }
    }
}
