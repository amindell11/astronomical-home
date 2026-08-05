using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boost readiness gauge: a radial fill that charges toward full as the cooldown runs down,
    /// then recolors and pulses once at the ready edge. Polls the bound <see cref="IShipStatus"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class BoostGaugeUI : MonoBehaviour
    {
        [Header("Colors")]
        [Tooltip("Fill tint while the cooldown is running.")]
        [SerializeField] private Color coolingColor = new Color(0.25f, 0.45f, 0.6f, 0.6f);

        [Tooltip("Fill tint once boost is ready.")]
        [SerializeField] private Color readyColor = new Color(0.1f, 0.9f, 1f, 0.9f);

        [Header("Ready pulse")]
        [Tooltip("Scale pop at the ready edge.")]
        [SerializeField, Range(1f, 2f)] private float pulseScale = 1.35f;

        [Tooltip("Pulse decay time in seconds.")]
        [SerializeField] private float pulseDuration = 0.3f;

        private Image fill;
        private Vector3 baseScale;
        private IShipStatus status;
        private bool wasAvailable;
        private float pulseElapsed;

        private void Awake()
        {
            fill = GetComponent<Image>();
            baseScale = transform.localScale;
            pulseElapsed = pulseDuration;
        }

        /// <summary>Re-bindable: the persistent HUD re-Initializes when the player is rebuilt.</summary>
        public void Initialize(IShipStatus status)
        {
            this.status = status;
            wasAvailable = status != null && status.BoostAvailable;
            pulseElapsed = pulseDuration;
            transform.localScale = baseScale;
            Apply();
        }

        private void Update()
        {
            if (status == null) return;

            var available = status.BoostAvailable;
            if (available && !wasAvailable)
                pulseElapsed = 0f;
            wasAvailable = available;

            Apply();

            if (pulseElapsed >= pulseDuration) return;
            pulseElapsed += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(pulseElapsed / pulseDuration);
            transform.localScale = baseScale * Mathf.Lerp(pulseScale, 1f, k);
        }

        private void Apply()
        {
            if (!fill) return;

            if (status == null)
            {
                fill.fillAmount = 0f;
                return;
            }

            fill.fillAmount = 1f - status.BoostCooldownPct;
            fill.color = status.BoostAvailable ? readyColor : coolingColor;
        }
    }
}
