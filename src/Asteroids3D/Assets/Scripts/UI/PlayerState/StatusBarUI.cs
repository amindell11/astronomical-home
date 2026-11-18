using Ships.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StatusBarUI : MaskableGraphic
    {
        public const float LowThreshold = .25f;
        private const float LowPulsePeriod = 1.4f;

        public static readonly Color ShieldTint = new(0f, .8f, 1f);
        public static readonly Color HullTint = new(.22f, 1f, .15f);

        private Resource source;
        private Vector2 size;
        private bool pulsesWhenLow;
        private bool pulsing;
        private float pulseTime;

        public static StatusBarUI Create(Transform parent, Resource resource, Color tint, Vector2 position, Vector2 size,
            bool pulsesWhenLow = false)
        {
            var bar = new GameObject("Status bar", typeof(RectTransform)).AddComponent<StatusBarUI>();
            bar.gameObject.layer = parent.gameObject.layer;
            bar.transform.SetParent(parent, false);
            bar.rectTransform.sizeDelta = size;
            bar.rectTransform.anchoredPosition = position;
            bar.size = size;
            bar.color = tint;
            bar.raycastTarget = false;
            bar.source = resource;
            bar.pulsesWhenLow = pulsesWhenLow;
            if (bar.isActiveAndEnabled) resource.OnValueChanged += bar.OnResourceChanged;
            bar.SetVerticesDirty();
            return bar;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (source == null) return;
            source.OnValueChanged += OnResourceChanged;
            SetVerticesDirty();
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

        private void OnResourceChanged(float current, float previous, float max) => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (source == null) return;
            var half = size * .5f;
            // Border and marks scale with the bar's height; an 8-tall bar keeps 1-unit lines.
            var line = size.y / 8f;
            Quad(mesh, -half.x - line, half.x + line, half.y + line, new Color(.02f, .04f, .08f, .92f));
            Quad(mesh, -half.x, half.x, half.y, new Color(.1f, .18f, .24f, 1f));
            var fill = color;
            if (pulsing)
            {
                var pulse = (1f - Mathf.Cos(2f * Mathf.PI * pulseTime / LowPulsePeriod)) * .5f;
                fill = Color.Lerp(new Color(.73f, .24f, .26f, color.a), new Color(.98f, .41f, .41f, color.a), pulse);
            }
            Quad(mesh, -half.x, -half.x + size.x * source.Pct, half.y, fill);
            for (var unit = 25; unit < source.MaxValue; unit += 25)
            {
                var x = -half.x + size.x * unit / source.MaxValue;
                Quad(mesh, x, x + (unit % 100 == 0 ? 2f : 1f) * line, half.y, new Color(.01f, .025f, .045f, .92f));
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
