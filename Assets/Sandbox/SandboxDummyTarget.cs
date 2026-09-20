using System.Collections;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Sandbox cube that implements <see cref="IDamageable"/>, <see cref="IStatusReceiver"/>,
    /// and optional <see cref="IProjectileProtection"/>.
    /// HP subtracts <see cref="HitInfo.Damage"/> from <see cref="IProjectileWorld.Hit"/> when
    /// <see cref="HitInfo.Protection"/> is None; status comes from the lab mapping
    /// <see cref="IProjectileWorld.StatusApplied"/>.
    /// </summary>
    public sealed class SandboxDummyTarget : MonoBehaviour, IDamageable, IStatusReceiver, IProjectileProtection
    {
        static readonly Color StatusFlashColor = new Color(0.15f, 0.9f, 0.28f);
        const float StatusFlashSeconds = 0.6f;

        [SerializeField] float maxHp = 10f;
        [SerializeField] float hp = 10f;
        [SerializeField] int targetId = 1;
        [SerializeField] ProtectionLevel protection = ProtectionLevel.None;

        IProjectileWorld subscribed;
        string lastStatusId = "";
        Renderer cachedRenderer;
        Color restColor;
        bool hasRestColor;
        Coroutine flashRoutine;

        public float MaxHp => maxHp;
        public float Hp => hp;
        public string LastStatusId => lastStatusId;
        public TargetId TargetId => new TargetId(targetId);

        public ProtectionLevel Level => protection;

        public void Configure(int id, float maxHpValue = 10f, ProtectionLevel protectionLevel = ProtectionLevel.None, bool restoreHp = true)
        {
            targetId = id;
            maxHp = maxHpValue;
            protection = protectionLevel;
            if (restoreHp)
                hp = maxHpValue;
        }

        public void Restore()
        {
            hp = maxHp;
            lastStatusId = "";
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            if (hasRestColor)
                SetColor(restColor);
        }

        public void Bind(IProjectileWorld world)
        {
            Unbind();
            subscribed = world;
            if (subscribed == null)
                return;

            subscribed.Hit += OnHit;
            subscribed.SetHomingCandidate(TargetId, transform.position);
        }

        public void Unbind()
        {
            if (subscribed == null)
                return;

            subscribed.Hit -= OnHit;
            subscribed.RemoveHomingCandidate(TargetId);
            subscribed = null;
        }

        void Start()
        {
            CaptureRestColor();
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
            if (hit.Protection != ProtectionLevel.None)
                return;

            hp = Mathf.Max(0f, hp - hit.Damage);
        }

        public void Apply(IStatusEffect effect)
        {
            if (effect == null)
                return;

            lastStatusId = string.IsNullOrEmpty(lastStatusId)
                ? effect.Id
                : lastStatusId + ", " + effect.Id;
            if (!isActiveAndEnabled)
                return;

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashStatus());
        }

        IEnumerator FlashStatus()
        {
            if (!hasRestColor)
                CaptureRestColor();

            float elapsed = 0f;
            while (elapsed < StatusFlashSeconds)
            {
                float t = elapsed / StatusFlashSeconds;
                SetColor(Color.Lerp(StatusFlashColor, restColor, t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetColor(restColor);
            flashRoutine = null;
        }

        void CaptureRestColor()
        {
            if (!TryGetTintMaterial(out Material mat))
                return;

            restColor = ReadColor(mat);
            hasRestColor = true;
        }

        void SetColor(Color color)
        {
            if (!TryGetTintMaterial(out Material mat))
                return;

            WriteColor(mat, color);
        }

        bool TryGetTintMaterial(out Material mat)
        {
            if (cachedRenderer == null)
                cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null)
            {
                mat = null;
                return false;
            }

            Material source = cachedRenderer.sharedMaterial;
            bool alreadyInstanced = source != null && source.name.IndexOf("(Instance)", System.StringComparison.Ordinal) >= 0;
            mat = alreadyInstanced
                ? source
                : source != null
                    ? new Material(source)
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cachedRenderer.sharedMaterial = mat;
            return mat != null;
        }

        static Color ReadColor(Material mat)
        {
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        static void WriteColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.25f, 0.2f, 0.35f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
        }
    }
}
