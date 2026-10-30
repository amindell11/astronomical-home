using UnityEngine;
using UnityEngine.UI;
using Combat.Weapons.Conditions;

namespace UI.Weapons
{
    public sealed class HeatGaugeUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image component whose FillAmount represents heat (0-1). Should use a Vertical fill method.")]
        [SerializeField] private Image fillImage;

        [Header("Overheat Animation")]
        [Tooltip("Optional animator that has a bool parameter named 'overheated'.")]
        [SerializeField] private Animator animator;

        [Header("Lockout")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color overheatColor = new(.98f, .41f, .41f);
        [Tooltip("Brightness pulses per second while firing is locked out.")]
        [SerializeField, Range(.1f, 3f)] private float overheatPulseRate = 1.5f;

        private bool wasOverheated;
        private IHeatReadout heat;

        private void Awake()
        {
            if (!fillImage) fillImage = GetComponentInChildren<Image>();
        }

        private void Update()
        {
            if (wasOverheated) ApplyColor();
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

        // Lockout persists while the heat bar drains.
        private void ApplyHeatVisuals(float pct, bool lockedOut)
        {
            if (fillImage)
                fillImage.fillAmount = pct;

            if (animator)
            {
                animator.SetBool("overheated", lockedOut);
                animator.SetFloat("heat", pct);
            }

            wasOverheated = lockedOut;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (!fillImage) return;
            var brightness = .825f + .175f * Mathf.Sin(Time.unscaledTime * overheatPulseRate * Mathf.PI * 2f);
            var color = wasOverheated ? overheatColor * brightness : normalColor;
            color.a = wasOverheated ? overheatColor.a : normalColor.a;
            fillImage.color = color;
        }
    }
}
