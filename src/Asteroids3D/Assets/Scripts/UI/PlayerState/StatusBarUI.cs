using Combat.Targeting;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Substrate;

namespace UI.PlayerState
{
    /// <summary>
    /// Ship-space status bar: a horizontal fill tracking one damage resource (shield or health),
    /// pinned at a fixed game-plane offset above the ship. Low hull pulses the fill without changing
    /// its quantity. Bound per ship via <see cref="IShipVisual"/> and seeded before the first damage event.
    /// </summary>
    public class StatusBarUI : MonoBehaviour, IShipVisual
    {
        public enum TrackedResource { Shield, Health }

        [Tooltip("Which damage resource this bar displays.")]
        [SerializeField] TrackedResource tracked = TrackedResource.Shield;

        [Tooltip("Fill image (child of this background).")]
        [SerializeField] Image fill;

        [Tooltip("Numeric readout beside the bar (current value, rounded up).")]
        [SerializeField] Text label;

        [Tooltip("Offset from the ship center in game-plane units.")]
        [SerializeField] Vector2 planeOffset = new Vector2(0f, 2f);

        [SerializeField, Range(0f, 1f)] private float lowHullThreshold = 0.25f;
        [SerializeField, Min(0.1f)] private float lowHullPulsePeriod = 1.4f;

        private Resource source;
        private bool subscribed;
        private LockChannel enemyLock;
        private Graphic[] graphics;
        private bool hidden;
        private Color fillColor;
        private float pulseTime;

        private void Awake()
        {
            graphics = GetComponentsInChildren<Graphic>(true);
            if (fill) fillColor = fill.color;
        }

        internal TrackedResource Tracked { get => tracked; set => tracked = value; }
        internal Image Fill { get => fill; set { fill = value; if (fill) fillColor = fill.color; } }

        public void Bind(in ShipView view)
        {
            source = view.Damage == null ? null
                : tracked == TrackedResource.Shield ? view.Damage.Shield : view.Damage.Health;
            enemyLock = view.IsPlayer ? null : view.Lock;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            if (source == null) return;
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
            pulseTime = 0f;
            if (fill) fill.color = fillColor;
        }

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.OnValueChanged += OnResourceChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || source == null) return;
            source.OnValueChanged -= OnResourceChanged;
            subscribed = false;
        }

        private void Refresh()
        {
            if (source == null) return;
            if (fill) fill.fillAmount = source.Pct;
            if (label) label.text = Mathf.CeilToInt(source.CurrentValue).ToString();
        }

        // Counter the rig rotation so bars stay aligned to the game plane.
        void LateUpdate()
        {
            transform.rotation = GamePlane.Rotation;
            transform.position = transform.parent.position + GamePlane.Rotation * (Vector3)planeOffset;
            Hide(enemyLock is { IsConcealed: true });
            if (!fill || source == null) return;
            if (tracked == TrackedResource.Health && source.Pct > 0f && source.Pct <= lowHullThreshold)
            {
                pulseTime += Time.deltaTime;
                var pulse = (1f - Mathf.Cos(2f * Mathf.PI * pulseTime / lowHullPulsePeriod)) * 0.5f;
                fill.color = new Color(Mathf.Lerp(0.73f, 0.98f, pulse), Mathf.Lerp(0.24f, 0.41f, pulse),
                    Mathf.Lerp(0.26f, 0.41f, pulse), fillColor.a);
                return;
            }
            pulseTime = 0f;
            fill.color = fillColor;
        }

        private void Hide(bool hide)
        {
            if (hide == hidden) return;
            hidden = hide;
            foreach (var graphic in graphics)
                if (graphic) graphic.enabled = !hide;
        }

        void OnResourceChanged(float current, float previous, float max)
        {
            Refresh();
        }
    }
}
