using Combat.Conditions;
using Combat.Weapons;
using UnityEngine;
using UnityEngine.UI;
namespace UI
{
    /// <summary>
    /// Displays the current heat level of a LaserGun using an Image with Fill mode.
    /// Place the UI as a vertical bar near the aiming reticle and assign the references
    /// in the inspector. Works in both world-space and screen-space canvases.
    /// </summary>
    public sealed class LaserHeatUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("LaserGun whose heat we visualise.")]
        [SerializeField] private WeaponLaser laserGun;

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
        private Heat heat;

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

        public void Initialize(Heat heat)
        {
            if (this.heat)
                this.heat.OnHeatChanged -= OnHeatChanged;

            this.heat = heat;
            if (!this.heat)
            {
                ApplyHeatVisuals(0f);
                return;
            }

            if (isActiveAndEnabled)
                this.heat.OnHeatChanged += OnHeatChanged;
            ApplyHeatVisuals(this.heat.HeatPct);
        }

        private void OnEnable()
        {
            if (heat)
                heat.OnHeatChanged += OnHeatChanged;
        }

        private void OnDisable()
        {
            if (heat)
                heat.OnHeatChanged -= OnHeatChanged;
            ApplyHeatVisuals(0f);
        }

        private void OnDestroy()
        {
            if (heat)
                heat.OnHeatChanged -= OnHeatChanged;
        }

        private void OnHeatChanged(float current, float max)
        {
            var pct = max > 0f ? current / max : 0f;
            ApplyHeatVisuals(pct);
        }

        private void ApplyHeatVisuals(float pct)
        {
            if (fillImage)
                fillImage.fillAmount = pct;

            if (animator)
            {
                animator.SetBool("overheated", pct >= 1f);
                animator.SetFloat("heat", pct);
            }

            // Handle glow controller behaviour
            if (glowController)
            {
                var isOverheated = pct >= 1f;
                if (isOverheated && !wasOverheated)
                {
                    glowController.SetEmissionColor(overheatGlowColor);
                    glowController.SetFlashing(true);
                    glowController.FlashSpeed = overheatFlashSpeed;
                }
                else if (!isOverheated && wasOverheated)
                {
                    glowController.SetFlashing(false);
                    glowController.SetEmissionColor(normalGlowColor);
                    glowController.FlashSpeed = defaultFlashSpeed;
                }
                wasOverheated = isOverheated;
            }
        }
    }
}
