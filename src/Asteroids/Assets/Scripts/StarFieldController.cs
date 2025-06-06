using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class StarFieldController : MonoBehaviour
{
    [Header("Material Reference")]
    [SerializeField] private Material starFieldMaterial;

    [Header("Star Properties")]
    [Range(0, 1)]
    public float starDensity = 0.5f;
    [Range(0, 0.1f)]
    public float starSize = 0.01f;
    [Range(0, 1f)]
    public float sizeVariation = 0.5f;
    [Range(0, 10f)]
    public float twinkleSpeed = 1f;
    public Color starColor = Color.white;
    public float cullDistance = 50f;
    [Range(0.01f, 1f)]
    public float gridDensity = 0.2f;
    [Range(0, 1f)]
    public float parallaxStrength = 0.5f;

    private void Start()
    {
        // Get the SpriteRenderer
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        
        // If no material is assigned, create one
        if (starFieldMaterial == null)
        {
            starFieldMaterial = new Material(Shader.Find("Custom/StarField"));
            renderer.material = starFieldMaterial;
        }
        else
        {
            // Use the assigned material
            renderer.material = starFieldMaterial;
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
        if (starFieldMaterial != null)
        {
            starFieldMaterial.SetFloat("_StarDensity", starDensity);
            starFieldMaterial.SetFloat("_StarSize", starSize);
            starFieldMaterial.SetFloat("_SizeVariation", sizeVariation);
            starFieldMaterial.SetFloat("_TwinkleSpeed", twinkleSpeed);
            starFieldMaterial.SetColor("_StarColor", starColor);
            starFieldMaterial.SetFloat("_CullDistance", cullDistance);
            starFieldMaterial.SetFloat("_GridDensity", gridDensity);
            starFieldMaterial.SetFloat("_ParallaxStrength", parallaxStrength);
        }
    }
} 