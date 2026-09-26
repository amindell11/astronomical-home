using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cameras.Background
{
    [ExecuteAlways, RequireComponent(typeof(Camera), typeof(Skybox))]
    public sealed class ContinuousSkyBackground : MonoBehaviour
    {
        [SerializeField] private Shader displayShader;
        [SerializeField, Range(0, 1f)] private float parallax = 0.002f;

        private Camera targetCamera;
        private Skybox skybox;
        private Material display;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int Rotation = Shader.PropertyToID("_Rotation");
        private static readonly int Parallax = Shader.PropertyToID("_Parallax");

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            skybox = GetComponent<Skybox>();
        }

        private void OnEnable()
        {
            if (!displayShader)
                throw new InvalidOperationException("Continuous sky background requires its display shader.");
            display = new Material(displayShader) { hideFlags = HideFlags.HideAndDontSave };
            skybox.material = display;
            skybox.enabled = true;
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != targetCamera || targetCamera.clearFlags != CameraClearFlags.Skybox)
                return;
            var source = RenderSettings.skybox;
            if (!source || source.shader.name != "Skybox/Panoramic" ||
                source.GetFloat("_Mapping") != 1 || source.GetFloat("_ImageType") != 0 ||
                source.GetFloat("_Layout") != 0 || !source.GetTexture(MainTex))
                throw new InvalidOperationException(
                    "Continuous sky background requires an active mono 360-degree panoramic skybox with a texture.");
            display.SetTexture(MainTex, source.GetTexture(MainTex));
            display.SetColor(Tint, source.GetColor(Tint));
            display.SetFloat(Exposure, source.GetFloat(Exposure));
            display.SetFloat(Rotation, source.GetFloat(Rotation));
            display.SetFloat(Parallax, parallax);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            skybox.enabled = false;
            skybox.material = RenderSettings.skybox;
            if (Application.isPlaying)
                Destroy(display);
            else
                DestroyImmediate(display);
        }
    }
}


