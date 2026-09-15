using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Sandbox cube that implements <see cref="IDamageable"/> and applies HP from <see cref="IProjectileWorld.Hit"/>.
    /// </summary>
    public sealed class SandboxDummyTarget : MonoBehaviour, IDamageable
    {
        [SerializeField] int maxHp = 10;
        [SerializeField] int hp = 10;
        [SerializeField] int targetId = 1;

        IProjectileWorld subscribed;

        public int MaxHp => maxHp;
        public int Hp => hp;
        public TargetId TargetId => new TargetId(targetId);

        public void Configure(int id, int maxHpValue = 10)
        {
            targetId = id;
            maxHp = maxHpValue;
            hp = maxHpValue;
        }

        public void Bind(IProjectileWorld world)
        {
            Unbind();
            subscribed = world;
            if (subscribed != null)
                subscribed.Hit += OnHit;
        }

        public void Unbind()
        {
            if (subscribed == null)
                return;

            subscribed.Hit -= OnHit;
            subscribed = null;
        }

        void OnDestroy()
        {
            Unbind();
        }

        void OnHit(HitInfo hit)
        {
            if (hit.Kind != HitKind.Target)
                return;
            if (hit.TargetId != TargetId)
                return;

            hp = Mathf.Max(0, hp - 1);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.25f, 0.2f, 0.35f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
        }
    }
}
