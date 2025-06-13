using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BackgroundLightController : MonoBehaviour
{
    private Material backgroundMaterial;
    [Range(0f, 1f)]
    public float ambientIntensity = 0.5f;
    public float updateInterval = 0.1f;
    
    private Color color1, color2, color3;
    private float lastUpdateTime;

    void Start()
    {
        backgroundMaterial = GetComponent<MeshRenderer>().material;
        // Cache the colors
        color1 = backgroundMaterial.GetColor("_Color1");
        color2 = backgroundMaterial.GetColor("_Color2");
        color3 = backgroundMaterial.GetColor("_Color3");
        
        UpdateAmbientLight();
    }

    void Update()
    {
        // Only update at the specified interval to save performance
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateAmbientLight();
            lastUpdateTime = Time.time;
        }
    }

    void UpdateAmbientLight()
    {
        if (!backgroundMaterial) return;

        // Check if emission is enabled
        float emissionEnabled = backgroundMaterial.GetFloat("_EmissionEnabled");
        if (emissionEnabled <= 0.5f)
        {
            // If emission is disabled, use default ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            return;
        }

        // Calculate average color from the three background colors
        Color averageColor = (color1 + color2 + color3) / 3f;
        
        // Apply the emission strength
        float emissionStrength = backgroundMaterial.GetFloat("_EmissionStrength");
        averageColor *= (emissionStrength * ambientIntensity) / 100f; // Divided by 100 since we increased the range to 200
        
        // Set ambient light settings
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = averageColor;
    }

    // Optional: Add methods to manually update the colors if they change in the material
    public void UpdateBackgroundColors()
    {
        if (backgroundMaterial)
        {
            color1 = backgroundMaterial.GetColor("_Color1");
            color2 = backgroundMaterial.GetColor("_Color2");
            color3 = backgroundMaterial.GetColor("_Color3");
            UpdateAmbientLight();
        }
    }
} 