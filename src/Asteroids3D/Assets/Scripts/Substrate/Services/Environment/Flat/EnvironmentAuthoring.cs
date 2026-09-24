using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment.Flat
{
    [DisallowMultipleComponent]
    public sealed class EnvironmentAuthoring : MonoBehaviour
    {
        [SerializeField] private FlatBackgroundAsset background;
        [SerializeField] private Shader backgroundShader;
        [SerializeField, Min(1)] private float repeatDistance = 20000;
        [SerializeField, Range(0.1f, 4)] private float viewHeightInTiles = 0.5f;
        [SerializeField] private Light[] nativeLights = Array.Empty<Light>();
        [SerializeField] private PaletteOverride baseColor;
        [SerializeField] private PaletteOverride primaryColor;
        [SerializeField] private PaletteOverride secondaryColor;
        [SerializeField] private PaletteOverride accentColor;
        private bool[] lightStates;
        private Mesh triangle;
        private Material material;
        private MaterialPropertyBlock properties;
        private FlatBackgroundAsset candidate;
        private bool previewing;

        public FlatBackgroundAsset Background => background;
        public FlatBackgroundAsset DisplayedBackground => previewing ? candidate : background;
        public float RepeatDistance => repeatDistance;
        public float ViewHeightInTiles => viewHeightInTiles;
        public bool IsActiveLocale => gameObject.scene == SceneManager.GetActiveScene();
        public Color BaseColor => baseColor.Resolve(DisplayedBackground.BaseColor);
        public Color PrimaryColor => primaryColor.Resolve(DisplayedBackground.PrimaryColor);
        public Color SecondaryColor => secondaryColor.Resolve(DisplayedBackground.SecondaryColor);
        public Color AccentColor => accentColor.Resolve(DisplayedBackground.AccentColor);

        private void Awake()
        {
            lightStates = new bool[nativeLights.Length];
            for (var i = 0; i < nativeLights.Length; i++)
            {
                if (!nativeLights[i])
                    throw new InvalidOperationException("Environment native light reference is missing.");
                lightStates[i] = nativeLights[i].enabled;
            }
            triangle = new Mesh { name = "Flat background triangle" };
            triangle.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) };
            triangle.triangles = new[] { 0, 1, 2 };
            triangle.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            FlatBackgroundCamera.Rendering += Draw;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            UpdateLights();
        }

        private void OnDisable()
        {
            FlatBackgroundCamera.Rendering -= Draw;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            UpdateLights();
            previewing = false;
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
            if (triangle) Destroy(triangle);
        }

        private void ActiveSceneChanged(Scene previous, Scene next) => UpdateLights();

        private void UpdateLights()
        {
            for (var i = 0; i < nativeLights.Length; i++)
                if (nativeLights[i])
                    nativeLights[i].enabled = isActiveAndEnabled && IsActiveLocale && lightStates[i];
        }

        public void Preview(FlatBackgroundAsset value)
        {
            if (!value) throw new ArgumentException("Choose an imported flat background.", nameof(value));
            candidate = value;
            previewing = true;
        }

        public void EndPreview()
        {
            previewing = false;
            candidate = null;
        }

        public static Vector2 Offset(Vector3 position, float distance)
        {
            if (!(distance > 0) || float.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            var x = (double)position.x / distance;
            var y = (double)position.y / distance;
            return new Vector2((float)(x - Math.Floor(x)), (float)(y - Math.Floor(y)));
        }

        private void Draw(Camera camera)
        {
            var source = DisplayedBackground;
            if (!IsActiveLocale || !source) return;
            if (!material)
            {
                if (!backgroundShader)
                    throw new InvalidOperationException("Environment background shader is missing; apply through Environment Preview.");
                material = new Material(backgroundShader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var offset = Offset(camera.transform.position, repeatDistance);
            properties.SetTexture("_MainTex", source.Texture);
            properties.SetVector("_Mapping", new Vector4(offset.x, offset.y,
                viewHeightInTiles * camera.aspect, viewHeightInTiles));
            Graphics.DrawMesh(triangle, Matrix4x4.Translate(camera.transform.position + camera.transform.forward),
                material, gameObject.layer, camera, 0, properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        [Serializable]
        private struct PaletteOverride
        {
            public bool useOverride;
            [ColorUsage(false, true)] public Color color;
            public Color Resolve(Color inherited) => useOverride ? color : inherited;
        }
    }
}
