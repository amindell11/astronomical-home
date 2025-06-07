using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpaceBackgroundController : MonoBehaviour
{
    [Header("Material Reference")]
    [SerializeField] private Material backgroundMaterial;
    
    [Header("Color Settings")]
    public Color color1 = new Color(0.04f, 0.05f, 0.10f, 1f);
    public Color color2 = new Color(0.10f, 0.13f, 0.22f, 1f);
    public Color color3 = new Color(0.12f, 0.09f, 0.16f, 1f);
    
    [Header("Effect Settings")]
    [Range(0.1f, 10f)]
    public float noiseScale = 2.5f;
    [Range(0f, 5f)]
    public float noiseStrength = 0.8f;
    [Range(0f, 5f)]
    public float scrollSpeed = 0.15f;
    [Range(0f, 5f)]
    public float distortion = 0.3f;

    private void Start()
    {
        // Get the SpriteRenderer
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        
        // If no material is assigned, create one
        if (backgroundMaterial == null)
        {
            backgroundMaterial = new Material(Shader.Find("Custom/SpaceBackground"));
            renderer.material = backgroundMaterial;
        }
        else
        {
            // Use the assigned material
            renderer.material = backgroundMaterial;
        }
        
        // Initialize the material properties
        UpdateMaterialProperties();
    }

    private void Update()
    {
        UpdateMaterialProperties();
    }

    private void UpdateMaterialProperties()
    {
        if (backgroundMaterial != null)
        {
            backgroundMaterial.SetColor("_Color1", color1);
            backgroundMaterial.SetColor("_Color2", color2);
            backgroundMaterial.SetColor("_Color3", color3);
            backgroundMaterial.SetFloat("_NoiseScale", noiseScale);
            backgroundMaterial.SetFloat("_NoiseStrength", noiseStrength);
            backgroundMaterial.SetFloat("_ScrollSpeed", scrollSpeed);
            backgroundMaterial.SetFloat("_Distortion", distortion);
        }
    }
} 