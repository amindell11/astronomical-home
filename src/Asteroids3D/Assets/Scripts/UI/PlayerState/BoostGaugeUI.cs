using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boost readiness bar: a horizontal fill that charges toward full as the cooldown runs
    /// down, recoloring once boost is ready. Polls the bound <see cref="IShipStatus"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class BoostGaugeUI : MonoBehaviour
    {
        [Header("Colors")]
        [Tooltip("Fill tint while the cooldown is running.")]
        [SerializeField] private Color coolingColor = new Color(0.25f, 0.45f, 0.6f, 0.6f);

        [Tooltip("Fill tint once boost is ready.")]
        [SerializeField] private Color readyColor = new Color(0.1f, 0.9f, 1f, 0.9f);

        private Image fill;
        private IShipStatus status;

        private void Awake()
        {
            fill = GetComponent<Image>();
        }

        /// <summary>Re-bindable: the persistent HUD re-Initializes when the player is rebuilt.</summary>
        public void Initialize(IShipStatus status)
        {
            this.status = status;
            Apply();
        }

        private void Update()
        {
            if (status == null) return;
            Apply();
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
