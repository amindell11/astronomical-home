using Game;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class ShieldUI : MonoBehaviour, IShipVisual
    {
        [Header("Timing (seconds)")]
        [SerializeField] float linger  = 0.30f;      // visible while it "shimmers"

        [Header("Shimmer")]
        [SerializeField] float shimmerFreq = 20f;    // Hz of scale flicker
        [SerializeField] float shimmerAmp  = 0.08f;  // 8 % size wobble

        [Header("Fill & Color")]
        [Tooltip("Optional gradient to tint ring based on remaining shield")]
        [SerializeField] Gradient shieldColors;

        private IDamageEvents source;   // injected: the ship whose shield flashes
        private bool subscribed;

        Image   ring;
        Color   baseColor;       // original tint without alpha
        bool flashActive;
        float flashElapsed;
        Vector3 baseScale;

        void Awake()
        {
            ring       = GetComponent<Image>();
            baseColor  = ring.color;
            baseScale  = transform.localScale;
            // start fully transparent until first event
            ring.canvasRenderer.SetAlpha(1f);
        }

        public void Bind(in ShipView view)
        {
            source = view.Damage;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (source != null) Subscribe();
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
            source.Shield.OnValueChanged += OnShieldChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || source == null) return;
            source.Shield.OnValueChanged -= OnShieldChanged;
            subscribed = false;
        }

        void LateUpdate()
        {
            if (ring && ring.canvasRenderer.GetAlpha() <= 0f) return;
            transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
        }


        void OnShieldChanged(float current, float previous, float max)
        {
            if (!ring || max <= 0f) return;

            // Update radial fill
            ring.fillAmount = current / max;
            if(current<previous) TriggerFlash();

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
