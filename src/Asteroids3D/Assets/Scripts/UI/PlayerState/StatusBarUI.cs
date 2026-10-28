using Ships.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StatusBarUI : MaskableGraphic
    {
        private const float LowThreshold = .25f;
        private const float LowPulsePeriod = 1.4f;

        private Resource source;
        private Text label;
        private bool pulsesWhenLow;
        private bool pulsing;
        private float pulseTime;

        public static StatusBarUI Create(Transform parent, Resource resource, Font font, Color tint, float y,
            bool pulsesWhenLow = false)
        {
            var bar = new GameObject("Status bar", typeof(RectTransform)).AddComponent<StatusBarUI>();
            bar.gameObject.layer = parent.gameObject.layer;
            bar.transform.SetParent(parent, false);
            bar.rectTransform.sizeDelta = new Vector2(80f, 8f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, y);
            bar.color = tint;
            bar.raycastTarget = false;
            bar.source = resource;
            bar.pulsesWhenLow = pulsesWhenLow;

            bar.label = new GameObject("Current / capacity", typeof(RectTransform)).AddComponent<Text>();
            bar.label.gameObject.layer = parent.gameObject.layer;
            bar.label.transform.SetParent(bar.transform, false);
            bar.label.rectTransform.pivot = new Vector2(0f, .5f);
            bar.label.rectTransform.anchoredPosition = new Vector2(45f, 0f);
            bar.label.rectTransform.sizeDelta = new Vector2(66f, 18f);
            bar.label.font = font;
            bar.label.fontSize = 12;
            bar.label.fontStyle = FontStyle.Bold;
            bar.label.alignment = TextAnchor.MiddleLeft;
            bar.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            bar.label.verticalOverflow = VerticalWrapMode.Overflow;
            bar.label.raycastTarget = false;
            var shadow = bar.label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .95f);
            shadow.effectDistance = new Vector2(1f, -1f);
            if (bar.isActiveAndEnabled) resource.OnValueChanged += bar.OnResourceChanged;
            bar.Refresh();
            return bar;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (source == null) return;
            source.OnValueChanged += OnResourceChanged;
            Refresh();
        }

        protected override void OnDisable()
        {
            if (source != null) source.OnValueChanged -= OnResourceChanged;
            base.OnDisable();
        }

        private void Update()
        {
            var low = pulsesWhenLow && source.Pct > 0f && source.Pct <= LowThreshold;
            if (!low && !pulsing) return;
            pulsing = low;
            pulseTime = low ? pulseTime + Time.deltaTime : 0f;
            SetVerticesDirty();
        }

        private void OnResourceChanged(float current, float previous, float max) => Refresh();

        private void Refresh()
        {
            label.text = $"{Mathf.CeilToInt(source.CurrentValue)}/{Mathf.CeilToInt(source.MaxValue)}";
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (source == null) return;
            Quad(mesh, -41f, 41f, 5f, new Color(.02f, .04f, .08f, .92f));
            Quad(mesh, -40f, 40f, 4f, new Color(.1f, .18f, .24f, 1f));
            var fill = color;
            if (pulsing)
            {
                var pulse = (1f - Mathf.Cos(2f * Mathf.PI * pulseTime / LowPulsePeriod)) * .5f;
                fill = Color.Lerp(new Color(.73f, .24f, .26f, color.a), new Color(.98f, .41f, .41f, color.a), pulse);
            }
            Quad(mesh, -40f, -40f + 80f * source.Pct, 4f, fill);
            for (var unit = 25; unit < source.MaxValue; unit += 25)
            {
                var x = -40f + 80f * unit / source.MaxValue;
                Quad(mesh, x, x + (unit % 100 == 0 ? 2f : 1f), 4f, new Color(.01f, .025f, .045f, .92f));
            }
        }

        private static void Quad(VertexHelper mesh, float left, float right, float halfHeight, Color tint)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, -halfHeight), tint, Vector2.zero);
            mesh.AddVert(new Vector3(left, halfHeight), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, halfHeight), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, -halfHeight), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
