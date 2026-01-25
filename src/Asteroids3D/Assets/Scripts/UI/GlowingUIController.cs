using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public sealed class GlowingUIController : MonoBehaviour
    {
        [Header("Material Settings")]
        [Tooltip("The glowing UI material to apply. Leave empty to use current material.")]
        [SerializeField] private Material glowMaterial;

        [Header("Color Settings")]
        [Tooltip("Base color for the UI element (solid fill).")]
        [SerializeField] private Color baseColor = Color.white;
    
        [Tooltip("Emission color for the glow effect.")]
        [SerializeField, ColorUsage(true, true)] private Color emissionColor = Color.cyan; // HDR supported
    
        [Tooltip("Intensity of the emission glow (0 = no glow, higher = brighter).")]    
        [SerializeField, Range(0f, 20f)] private float emissionIntensity = 1.0f; // Match shader range

        [Header("Animation")]
        [Tooltip("Enable pulsing glow animation.")]
        [SerializeField] private bool enablePulsing = false;
    
        [Tooltip("Speed of the pulsing animation.")]
        [SerializeField, Range(0.1f, 10f)] private float pulseSpeed = 2f;
    
        [Tooltip("Range of intensity variation for pulsing (min, max).")]
        [SerializeField] private Vector2 pulseRange = new Vector2(0.5f, 1.5f);

        [Header("Flashing")]
        [Tooltip("Enable flashing animation (on/off). Overrides pulsing when active.")]
        [SerializeField] private bool enableFlashing = false;

        [Tooltip("Flashes per second.")]
        [SerializeField, Range(0.1f, 20f)] private float flashSpeed = 4f;

        private Image image;
        private Material materialInstance;
        private float basePulseIntensity;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionIntensityProperty = Shader.PropertyToID("_EmissionIntensity");

        private void Awake()
        {
            image = GetComponent<Image>();
            SetupMaterial();
        }

        private void Start()
        {
            basePulseIntensity = emissionIntensity;
            UpdateMaterialProperties();
        }

        private void Update()
        {
            if (enableFlashing && materialInstance)
            {
                UpdateFlashing();
            }
            else if (enablePulsing && materialInstance)
            {
                UpdatePulsing();
            }
        }

        private void OnDestroy()
        {
            if (materialInstance) DestroyImmediate(materialInstance);
        }
        
        private void SetupMaterial()
        {
            if (glowMaterial)
            {
                materialInstance = new Material(glowMaterial);
                image.material = materialInstance;
            }
            else if (image.material && image.material.shader.name == "UI/GlowFill")
            {
                materialInstance = new Material(image.material);
                image.material = materialInstance;
            }
        }
        private void UpdateMaterialProperties()
        {
            if (!materialInstance) return;

            materialInstance.SetColor(ColorProperty, baseColor);
            materialInstance.SetColor(EmissionColorProperty, emissionColor);
            materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
        }
        
        private void UpdatePulsing()
        {
            var pulse = Mathf.Lerp(pulseRange.x, pulseRange.y, 
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
        
            var currentIntensity = basePulseIntensity * pulse;
            materialInstance.SetFloat(EmissionIntensityProperty, currentIntensity);
        }
        private void UpdateFlashing()
        {
            var onState = Mathf.FloorToInt(Time.time * flashSpeed) % 2 == 0;
            var currentIntensity = onState ? basePulseIntensity : 0f;
            materialInstance.SetFloat(EmissionIntensityProperty, currentIntensity);
        }
        public void SetBaseColor(Color color)
        {
            baseColor = color;
            if (materialInstance)
            {
                materialInstance.SetColor(ColorProperty, baseColor);
            }
        }
        public void SetEmissionColor(Color color)
        {
            emissionColor = color;
            if (materialInstance)
            {
                materialInstance.SetColor(EmissionColorProperty, emissionColor);
            }
        }
        public void SetEmissionIntensity(float intensity)
        {
            emissionIntensity = intensity;
            basePulseIntensity = intensity;
            if (materialInstance && !enablePulsing)
            {
                materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
            }
        }

        public void SetPulsing(bool enabled)
        {
            enablePulsing = enabled;
            switch (enabled)
            {
                case true:
                    enableFlashing = false;
                    break;
                case false when materialInstance:
                    materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
                    break;
            }
        }
        
        public void SetFlashing(bool enabled)
        {
            enableFlashing = enabled;
            switch (enabled)
            {
                case true:
                    enablePulsing = false;
                    break;
                case false when materialInstance:
                    materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
                    break;
            }
        }
        
        public void ApplyLaserHeatPreset()
        {
            SetBaseColor(new Color(1f, 0.3f, 0.1f, 1f));
            SetEmissionColor(new Color(1f, 0.5f, 0.2f, 1f));
            SetEmissionIntensity(1.5f);
        }
        
        public void ApplyMissileAmmoPreset()
        {
            SetBaseColor(new Color(0.1f, 0.6f, 1f, 1f));
            SetEmissionColor(new Color(0.3f, 0.8f, 1f, 1f));
            SetEmissionIntensity(1.2f);
        }
        
        public float PulseSpeed
        {
            get => pulseSpeed;
            set => pulseSpeed = Mathf.Clamp(value, 0.1f, 10f);
        }

        public float FlashSpeed
        {
            get => flashSpeed;
            set => flashSpeed = Mathf.Clamp(value, 0.1f, 20f);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !materialInstance) return;
            basePulseIntensity = emissionIntensity;
            UpdateMaterialProperties();
        }
    }
} 