using UnityEngine;
using UnityEngine.UI;

namespace Veerdonk.ProjectileSystem.Samples
{
    /// <summary>World-space name and HP label sitting above a sample cube.</summary>
    public sealed class SandboxCubeLabel : MonoBehaviour
    {
        const string ChildName = "CubeLabel";
        const float WorldScale = 0.012f;

        [SerializeField] string title;
        Text text;
        SandboxDummyTarget dummy;

        public static void Ensure(Transform cube, string labelTitle)
        {
            if (cube == null)
                return;

            var label = cube.GetComponent<SandboxCubeLabel>();
            if (label == null)
                label = cube.gameObject.AddComponent<SandboxCubeLabel>();
            label.title = labelTitle;
            label.dummy = cube.GetComponent<SandboxDummyTarget>();
            label.BuildIfNeeded();
            label.Refresh();
        }

        void LateUpdate()
        {
            if (text == null)
                BuildIfNeeded();
            Refresh();
            Camera cam = Camera.main;
            if (cam != null && text != null)
                text.transform.rotation = cam.transform.rotation;
        }

        void BuildIfNeeded()
        {
            Transform child = transform.Find(ChildName);
            GameObject go = child != null ? child.gameObject : new GameObject(ChildName);
            if (child == null)
                go.transform.SetParent(transform, false);

            Vector3 scale = transform.lossyScale;
            if (scale.x < 1e-4f || scale.y < 1e-4f || scale.z < 1e-4f)
                scale = Vector3.one;
            go.transform.localScale = new Vector3(WorldScale / scale.x, WorldScale / scale.y, WorldScale / scale.z);
            go.transform.localPosition = new Vector3(0f, 0.5f + 0.55f / scale.y, 0f);

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
                canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(180f, 64f);

            text = go.GetComponent<Text>();
            if (text == null)
                text = go.AddComponent<Text>();
            text.font = LabelFont();
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (go.GetComponent<Shadow>() == null)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        void Refresh()
        {
            if (text == null)
                return;
            text.text = dummy != null
                ? $"{title}\nHP {dummy.Hp:0.##}/{dummy.MaxHp:0.##}"
                : title;
        }

        static Font LabelFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Arial", 28);
            return font;
        }
    }
}
