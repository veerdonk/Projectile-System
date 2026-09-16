using System.Collections;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    /// <summary>
    /// Sandbox cube that implements <see cref="IDamageable"/> and <see cref="IStatusReceiver"/>.
    /// HP comes from <see cref="IProjectileWorld.Hit"/>; status comes from the lab mapping <see cref="IProjectileWorld.StatusApplied"/>.
    /// </summary>
    public sealed class SandboxDummyTarget : MonoBehaviour, IDamageable, IStatusReceiver
    {
        static readonly Color StatusFlashColor = new Color(0.15f, 0.9f, 0.28f);
        const float StatusFlashSeconds = 0.6f;

        [SerializeField] int maxHp = 10;
        [SerializeField] int hp = 10;
        [SerializeField] int targetId = 1;

        IProjectileWorld subscribed;
        string lastStatusId = "";
        Renderer cachedRenderer;
        Color restColor;
        bool hasRestColor;
        Coroutine flashRoutine;

        public int MaxHp => maxHp;
        public int Hp => hp;
        public string LastStatusId => lastStatusId;
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

            hp = Mathf.Max(0, hp - 1);
        }

        public void Apply(IStatusEffect effect)
        {
            lastStatusId = effect != null ? effect.Id : "";
            if (effect == null || !isActiveAndEnabled)
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
