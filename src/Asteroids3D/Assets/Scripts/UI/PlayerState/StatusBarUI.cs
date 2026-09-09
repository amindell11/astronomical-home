using Game;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>
    /// Ship-space status bar: a horizontal fill tracking one damage resource (shield or health),
    /// pinned at a fixed game-plane offset above the ship. Scaffolding stand-in for the parked
    /// status rings (stashed on task/player-state). Bound per ship via <see cref="IShipVisual"/>;
    /// fill seeds from the bound resource so the bar reads correctly before the first damage event.
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

        private Resource source;
        private bool subscribed;

        internal TrackedResource Tracked { get => tracked; set => tracked = value; }
        internal Image Fill { get => fill; set => fill = value; }

        public void Bind(in ShipView view)
        {
            source = view.Damage == null ? null
                : tracked == TrackedResource.Shield ? view.Damage.Shield : view.Damage.Health;
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

        // The rig canvas rotates with the ship; re-pin the bar plane-aligned above it every frame.
        void LateUpdate()
        {
            transform.rotation = GamePlane.Rotation;
            transform.position = transform.parent.position + GamePlane.Rotation * (Vector3)planeOffset;
        }

        void OnResourceChanged(float current, float previous, float max)
        {
            Refresh();
        }
    }
}
