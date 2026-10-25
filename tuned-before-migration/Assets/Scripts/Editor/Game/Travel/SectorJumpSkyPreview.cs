using System;
using Substrate;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Travel
{
    public sealed class SectorJumpSkyPreview : IDisposable
    {
        private readonly Scene scene;
        private readonly Camera camera;
        private readonly RenderTexture target;

        public SectorJumpSkyPreview(string scenePath)
        {
            scene = EditorSceneManager.OpenPreviewScene(scenePath);
            var cameraObject = new GameObject("Jump sky preview") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 12;
            camera.aspect = 16f / 9f;
            camera.cullingMask = 1 << LayerIds.Sky;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            camera.targetTexture = target;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
        }

        public Texture Render(Vector2 position)
        {
            camera.transform.position = new Vector3(position.x, position.y, -10);
            camera.Render();
            return target;
        }

        public void Dispose()
        {
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
