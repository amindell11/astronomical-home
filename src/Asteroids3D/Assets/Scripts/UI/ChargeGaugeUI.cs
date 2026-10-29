using UnityEngine;
using UnityEngine.UI;
using Combat.Weapons.Conditions;

namespace UI
{
    public sealed class ChargeGaugeUI : MonoBehaviour
    {
        [Tooltip("Image whose FillAmount represents charge (0-1).")]
        [SerializeField] private Image fillImage;

        private IChargeReadout charge;

        private void Awake()
        {
            if (!fillImage) fillImage = GetComponentInChildren<Image>();
        }

        public void Initialize(IChargeReadout charge)
        {
            if (this.charge != null)
                this.charge.OnChargeChanged -= OnChargeChanged;

            this.charge = charge;
            OnChargeChanged(this.charge?.ChargePct ?? 0f);
            if (this.charge != null)
                this.charge.OnChargeChanged += OnChargeChanged;
        }

        private void OnDestroy()
        {
            if (charge != null)
                charge.OnChargeChanged -= OnChargeChanged;
        }

        private void OnChargeChanged(float pct)
        {
            if (fillImage)
                fillImage.fillAmount = pct;
        }
    }
}
