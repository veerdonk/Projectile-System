using Veerdonk.ProjectileSystem;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Samples
{
    /// <summary>Sandbox collider marker for a Strong shield (or Basic without dummy HP).</summary>
    public sealed class SandboxProjectileProtection : MonoBehaviour, IProjectileProtection
    {
        [SerializeField] ProtectionLevel level = ProtectionLevel.Strong;

        public ProtectionLevel Level => level;

        public void Configure(ProtectionLevel protectionLevel)
        {
            level = protectionLevel;
        }
    }
}
