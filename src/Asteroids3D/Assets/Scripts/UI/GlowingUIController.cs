using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls glowing UI effects for Image components.
/// Allows runtime control of solid color fill and emission glow.
/// Apply this to any UI Image that uses the UI/GlowFill shader.
/// </summary>
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

    // Shader property IDs for performance
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionIntensityProperty = Shader.PropertyToID("_EmissionIntensity");

    void Awake()
    {
        image = GetComponent<Image>();
        SetupMaterial();
    }

    void Start()
    {
        basePulseIntensity = emissionIntensity;
        UpdateMaterialProperties();
    }

    void Update()
    {
        if (enableFlashing && materialInstance != null)
        {
            UpdateFlashing();
        }
        else if (enablePulsing && materialInstance != null)
        {
            UpdatePulsing();
        }
    }

    void OnDestroy()
    {
        // Clean up material instance to prevent memory leaks
        if (materialInstance != null)
        {
            DestroyImmediate(materialInstance);
        }
    }

    /// <summary>
    /// Sets up the material instance for this UI element.
    /// </summary>
    private void SetupMaterial()
    {
        if (glowMaterial != null)
        {
            // Create a material instance to avoid affecting other UI elements
            materialInstance = new Material(glowMaterial);
            image.material = materialInstance;
        }
        else if (image.material != null && image.material.shader.name == "UI/GlowFill")
        {
            // Use existing material but create instance for safe property changes
            materialInstance = new Material(image.material);
            image.material = materialInstance;
        }
        else
        {
            Debug.LogWarning($"GlowingUIController on {name}: No glowing material assigned and current material doesn't use UI/GlowFill shader.");
        }
    }

    /// <summary>
    /// Updates all material properties based on current settings.
    /// </summary>
    private void UpdateMaterialProperties()
    {
        if (materialInstance == null) return;

        materialInstance.SetColor(ColorProperty, baseColor);
        materialInstance.SetColor(EmissionColorProperty, emissionColor);
        materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
    }

    /// <summary>
    /// Updates the pulsing animation.
    /// </summary>
    private void UpdatePulsing()
    {
        float pulse = Mathf.Lerp(pulseRange.x, pulseRange.y, 
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
        
        float currentIntensity = basePulseIntensity * pulse;
        materialInstance.SetFloat(EmissionIntensityProperty, currentIntensity);
    }

    /// <summary>
    /// Updates the flashing animation (square wave on/off).
    /// </summary>
    private void UpdateFlashing()
    {
        bool onState = Mathf.FloorToInt(Time.time * flashSpeed) % 2 == 0;
        float currentIntensity = onState ? basePulseIntensity : 0f;
        materialInstance.SetFloat(EmissionIntensityProperty, currentIntensity);
    }

    /// <summary>
    /// Sets the base color at runtime.
    /// </summary>
    /// <param name="color">New base color</param>
    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (materialInstance != null)
        {
            materialInstance.SetColor(ColorProperty, baseColor);
        }
    }

    /// <summary>
    /// Sets the emission color at runtime.
    /// </summary>
    /// <param name="color">New emission color</param>
    public void SetEmissionColor(Color color)
    {
        emissionColor = color;
        if (materialInstance != null)
        {
            materialInstance.SetColor(EmissionColorProperty, emissionColor);
        }
    }

    /// <summary>
    /// Sets the emission intensity at runtime.
    /// </summary>
    /// <param name="intensity">New emission intensity</param>
    public void SetEmissionIntensity(float intensity)
    {
        emissionIntensity = intensity;
        basePulseIntensity = intensity;
        if (materialInstance != null && !enablePulsing)
        {
            materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
        }
    }

    /// <summary>
    /// Enables or disables the pulsing effect.
    /// </summary>
    /// <param name="enabled">Whether pulsing should be enabled</param>
    public void SetPulsing(bool enabled)
    {
        enablePulsing = enabled;
        if (enabled)
        {
            enableFlashing = false; // ensure only one animation mode is active
        }
        if (!enabled && materialInstance != null)
        {
            // Reset to base intensity when disabling pulsing
            materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
        }
    }

    /// <summary>
    /// Enables or disables the flashing effect. When enabled, disables pulsing.
    /// </summary>
    public void SetFlashing(bool enabled)
    {
        enableFlashing = enabled;
        if (enabled)
        {
            enablePulsing = false; // ensure only one animation mode is active
        }
        if (!enabled && materialInstance != null)
        {
            // Reset to base intensity when disabling flashing
            materialInstance.SetFloat(EmissionIntensityProperty, emissionIntensity);
        }
    }

    /// <summary>
    /// Applies a preset configuration for laser heat UI.
    /// </summary>
    public void ApplyLaserHeatPreset()
    {
        SetBaseColor(new Color(1f, 0.3f, 0.1f, 1f));
        SetEmissionColor(new Color(1f, 0.5f, 0.2f, 1f));
        SetEmissionIntensity(1.5f);
    }

    /// <summary>
    /// Applies a preset configuration for missile ammo UI.
    /// </summary>
    public void ApplyMissileAmmoPreset()
    {
        SetBaseColor(new Color(0.1f, 0.6f, 1f, 1f));
        SetEmissionColor(new Color(0.3f, 0.8f, 1f, 1f));
        SetEmissionIntensity(1.2f);
    }

    /// <summary>
    /// Gets or sets the pulse speed used by the pulsing animation (clamped between 0.1 and 10).
    /// </summary>
    public float PulseSpeed
    {
        get => pulseSpeed;
        set => pulseSpeed = Mathf.Clamp(value, 0.1f, 10f);
    }

    /// <summary>
    /// Gets or sets the flash speed (clamped between 0.1 and 20).
    /// </summary>
    public float FlashSpeed
    {
        get => flashSpeed;
        set => flashSpeed = Mathf.Clamp(value, 0.1f, 20f);
    }

    // Editor-only method for updating properties in the inspector
    void OnValidate()
    {
        if (Application.isPlaying && materialInstance != null)
        {
            basePulseIntensity = emissionIntensity;
            UpdateMaterialProperties();
        }
    }
} 