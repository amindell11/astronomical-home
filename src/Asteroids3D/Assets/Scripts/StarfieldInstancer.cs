using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
public class StarfieldInstancer : MonoBehaviour
{
    [Header("Star Generation")]
    [SerializeField] private int starCount = 60_000;
    [SerializeField] private float tileSize = 800f;
    [SerializeField] private int layerCount = 3;
    [SerializeField] private float parallaxStep = 0.05f;
    [SerializeField] private float parallaxMultiplier = 1f;
    [SerializeField] private Vector2 starSizeRange = new Vector2(0.02f, 0.08f);
    [SerializeField] private Color starColor = Color.white;

    [Header("Rendering")]
    [SerializeField] private Material starMaterial;

    private static Mesh quadMesh;
    private GraphicsBuffer starBuffer;
    private Bounds drawBounds;

    [StructLayout(LayoutKind.Sequential)]
    private struct StarData
    {
        public Vector3 pos;
        public float size;
        public Vector4 color;
        public float parallax;
    }

    private void OnEnable()
    {
        if (starMaterial == null)
        {
            Debug.LogError("StarfieldInstancer: Please assign a material that uses the 'Custom/InstancedStarURP' shader.", this);
            enabled = false;
            return;
        }

        InitQuadMesh();
        BuildStarBuffer();
    }

    private void OnDisable()
    {
        starBuffer?.Dispose();
        starBuffer = null;
    }

    private void Update()
    {
        if (starBuffer == null) return;

        // Update bounds to follow camera (improves culling, prevents distant tiles)
        if (Camera.main)
        {
            drawBounds.center = Camera.main.transform.position;
            drawBounds.extents = new Vector3(tileSize, 50f, tileSize);
        }

        starMaterial.SetFloat("_TileSize", tileSize);
        starMaterial.SetFloat("_ParallaxMultiplier", parallaxMultiplier);

        Graphics.DrawMeshInstancedProcedural(
            quadMesh,
            0,
            starMaterial,
            drawBounds,
            starCount
        );
    }

    private void OnValidate()
    {
        if (starMaterial != null)
        {
            // Expose array edits only; nothing else to update at runtime
        }
    }

    #region Buffer & Mesh helpers

    private void InitQuadMesh()
    {
        if (quadMesh != null) return;

        quadMesh = new Mesh { name = "Procedural Quad" };
        quadMesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        });
        quadMesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        });
        quadMesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        quadMesh.RecalculateBounds();
    }

    private void BuildStarBuffer()
    {
        starBuffer?.Dispose();
        starBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, starCount, Marshal.SizeOf<StarData>());

        var stars = new StarData[starCount];
        Random.InitState(1234);
        int perLayerCount = Mathf.Max(1, starCount / layerCount);
        int index = 0;
        for (int l = 0; l < layerCount; l++)
        {
            float parallax = (l + 1) * parallaxStep;
            for (int s = 0; s < perLayerCount && index < starCount; s++, index++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-tileSize * 0.5f, tileSize * 0.5f),
                    0f,
                    Random.Range(-tileSize * 0.5f, tileSize * 0.5f));

                float size = Random.Range(starSizeRange.x, starSizeRange.y);

                stars[index].pos = pos;
                stars[index].size = size;
                stars[index].color = starColor;
                stars[index].parallax = parallax;
            }
        }

        // Safety fill if division remainder
        for (; index < starCount; index++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-tileSize * 0.5f, tileSize * 0.5f),
                0f,
                Random.Range(-tileSize * 0.5f, tileSize * 0.5f));
            float size = Random.Range(starSizeRange.x, starSizeRange.y);
            stars[index].pos = pos;
            stars[index].size = size;
            stars[index].color = starColor;
            stars[index].parallax = layerCount * parallaxStep;
        }

        starBuffer.SetData(stars);
        starMaterial.SetBuffer("_Stars", starBuffer);
        starMaterial.SetFloat("_TileSize", tileSize);
        starMaterial.SetFloat("_ParallaxMultiplier", parallaxMultiplier);

        drawBounds = new Bounds(Vector3.zero, new Vector3(tileSize, 50f, tileSize));
    }

    #endregion
} 