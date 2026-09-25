using System;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Substrate.Services.Environment
{
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class FlatBackgroundCamera : MonoBehaviour, IPresentationPart
    {
        public static event Action<Camera> Rendering;
        private Camera cameraView;
        private bool presenting = true;

        private void Awake() => cameraView = GetComponent<Camera>();
        private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeginCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeginCamera;
        public void ApplyPresentation(bool visible) => presenting = visible;

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (presenting && camera == cameraView)
                Rendering?.Invoke(camera);
        }
    }
}
