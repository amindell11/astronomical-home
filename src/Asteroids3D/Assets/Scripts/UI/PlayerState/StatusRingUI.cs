using Game;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Ship-space status ring: a radial fill tracking one damage resource (shield or health),
    /// with a shimmer flash on loss. Bound per ship via <see cref="IShipVisual"/>; fill seeds
    /// from the bound resource so the ring reads correctly before the first damage event.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class StatusRingUI : MonoBehaviour, IShipVisual
    {
        public enum TrackedResource { Shield, Health }

        [Tooltip("Which damage resource this ring displays.")]
        [SerializeField] TrackedResource tracked = TrackedResource.Shield;

        [Header("Timing (seconds)")]
        [SerializeField] float linger  = 0.30f;      // visible while it "shimmers"

        [Header("Shimmer")]
        [SerializeField] float shimmerFreq = 20f;    // Hz of scale flicker
        [SerializeField] float shimmerAmp  = 0.08f;  // 8 % size wobble

        private Resource source;
        private bool subscribed;

        Image   ring;
        bool flashActive;
        float flashElapsed;
        Vector3 baseScale;

        internal TrackedResource Tracked { get => tracked; set => tracked = value; }

        void Awake()
        {
            ring      = GetComponent<Image>();
            baseScale = transform.localScale;
        }

        public void Bind(in ShipView view)
        {
            source = view.Damage == null ? null
                : tracked == TrackedResource.Shield ? view.Damage.Shield : view.Damage.Health;
            if (isActiveAndEnabled) Subscribe();
            SeedFill();
        }

        private void OnEnable()
        {
            if (source == null) return;
            Subscribe();
            SeedFill();
        }

        void OnDisable()
        {
            Unsubscribe();
            flashActive = false;
            transform.localScale = baseScale;
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

        private void SeedFill()
        {
            if (ring && source != null)
                ring.fillAmount = source.Pct;
        }

        void LateUpdate()
        {
            transform.rotation = GamePlane.Rotation;
        }

        void OnResourceChanged(float current, float previous, float max)
        {
            if (!ring || max <= 0f) return;

            ring.fillAmount = current / max;
            if (current < previous) TriggerFlash();
        }

        void TriggerFlash()
        {
            flashActive = true;
            flashElapsed = 0f;
        }

        void Update()
        {
            if (!flashActive) return;

            flashElapsed += Time.unscaledDeltaTime;
            if (flashElapsed >= linger)
            {
                flashActive = false;
                transform.localScale = baseScale;
                return;
            }

            var wobble = 1f + Mathf.Sin(flashElapsed * shimmerFreq * Mathf.PI * 2) * shimmerAmp;
            transform.localScale = baseScale * wobble;
        }
    }
}
