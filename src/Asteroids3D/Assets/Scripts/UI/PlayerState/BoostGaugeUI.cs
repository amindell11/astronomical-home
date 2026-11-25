using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
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
