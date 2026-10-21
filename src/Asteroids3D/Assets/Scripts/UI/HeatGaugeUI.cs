using UnityEngine;
using UnityEngine.UI;
using Combat.Weapons.Conditions;

namespace UI
{
    /// <summary>
    /// Heat readout widget: a vertical fill bar with an overheat flash. One is generated per
    /// heat-carrying weapon by <see cref="WeaponReadoutBuilder"/>.
    /// </summary>
    public sealed class HeatGaugeUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image component whose FillAmount represents heat (0-1). Should use a Vertical fill method.")]
        [SerializeField] private Image fillImage;

        [Header("Overheat Flash")]
        [Tooltip("Optional animator that has a bool parameter named 'overheated'.")]
        [SerializeField] private Animator animator;

        [Header("Glow Settings")]
        [Tooltip("Optional glowing UI controller on the same object. If assigned, the emission color will turn red and pulse when overheated.")]
        [SerializeField] private GlowingUIController glowController;

        [Tooltip("Emission color under normal operation.")]
        [SerializeField, ColorUsage(true, true)] private Color normalGlowColor = new Color(1f, 0.5f, 0.2f, 1f);

        [Tooltip("Emission color while overheated.")]
        [SerializeField, ColorUsage(true, true)] private Color overheatGlowColor = Color.red;

        [Tooltip("Flash speed while overheated (higher = faster).")]
        [SerializeField, Range(0.1f, 20f)] private float overheatFlashSpeed = 8f;

        private bool wasOverheated;
        private float defaultFlashSpeed = 4f;
        private IHeatReadout heat;

        private void Awake()
        {
            if (!fillImage) fillImage = GetComponentInChildren<Image>();
            if (!glowController && fillImage)
                glowController = fillImage.GetComponent<GlowingUIController>();
            if (!glowController)
                glowController = GetComponent<GlowingUIController>();
            if (!glowController) return;
            defaultFlashSpeed = glowController.FlashSpeed;
            glowController.SetEmissionColor(normalGlowColor);
            glowController.SetFlashing(false);
        }

        public void Initialize(IHeatReadout heat)
        {
            if (this.heat != null)
                Unsubscribe();

            this.heat = heat;
            if (this.heat == null)
            {
                ApplyHeatVisuals(0f, lockedOut: false);
                return;
            }

            if (isActiveAndEnabled)
                Subscribe();
            ApplyHeatVisuals(this.heat.HeatPct, this.heat.Overheated);
        }

        private void Subscribe()
        {
            heat.OnHeatChanged += OnHeatChanged;
            heat.OnOverheat += OnLockoutChanged;
            heat.OnCooldownStart += OnLockoutChanged;
        }

        private void Unsubscribe()
        {
            heat.OnHeatChanged -= OnHeatChanged;
            heat.OnOverheat -= OnLockoutChanged;
            heat.OnCooldownStart -= OnLockoutChanged;
        }

        private void OnEnable()
        {
            if (heat == null) return;
            Subscribe();
            ApplyHeatVisuals(heat.HeatPct, heat.Overheated);
        }

        private void OnDisable()
        {
            if (heat != null)
                Unsubscribe();
            ApplyHeatVisuals(0f, lockedOut: false);
        }

        private void OnDestroy()
        {
            if (heat != null)
                Unsubscribe();
        }

        private void OnHeatChanged(float current, float max)
        {
            var pct = max > 0f ? current / max : 0f;
            ApplyHeatVisuals(pct, heat.Overheated);
        }

        private void OnLockoutChanged() => ApplyHeatVisuals(heat.HeatPct, heat.Overheated);

        // Flash keys on lockout, not bar fullness: the bar drains while fire stays locked.
        private void ApplyHeatVisuals(float pct, bool lockedOut)
        {
            if (fillImage)
                fillImage.fillAmount = pct;

            if (animator)
            {
                animator.SetBool("overheated", lockedOut);
                animator.SetFloat("heat", pct);
            }

            if (glowController)
            {
                if (lockedOut && !wasOverheated)
                {
                    glowController.SetEmissionColor(overheatGlowColor);
                    glowController.SetFlashing(true);
                    glowController.FlashSpeed = overheatFlashSpeed;
                }
                else if (!lockedOut && wasOverheated)
                {
                    glowController.SetFlashing(false);
                    glowController.SetEmissionColor(normalGlowColor);
                    glowController.FlashSpeed = defaultFlashSpeed;
                }
                wasOverheated = lockedOut;
            }
        }
    }
}
