using System.Collections.Generic;
using Cameras;
using Ships;
using Substrate;
using Substrate.Services.Locales;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Travel
{
    /// <summary>
    /// Applies a jump state to live locale skies, the ship's hull and the actual HUD.
    /// Its render targets belong to the presentation, so previews and gameplay use the same renderer.
    /// The caller owns simulation time, destination loading, the world blend and the ship and camera pose.
    /// </summary>
    public sealed class SectorJumpPresentation : MonoBehaviour
    {
        public const int StreakSeed = 1099;

        private Ship ship;
        private Overlay overlay;
        private IEnumerable<Ship> ships;
        private ObserverCam observer;
        private LocaleBackdropPair backdrop;
        private Camera skyCamera;
        private Camera hullCamera;
        private RenderTexture outgoing;
        private RenderTexture incoming;
        private RenderTexture hull;
        private RenderTexture incomingHull;
        private RenderTexture composed;
        private Material material;
        private RawImage image;
        private int[] hullLayers;
        private float worldBlend = 1f;
        private float hullDiameter;

        public static SectorJumpPresentation Create(Transform parent, Ship ship, Overlay overlay,
            IEnumerable<Ship> ships, ObserverCam observer, LocaleBackdropPair backdrop, Shader shader)
        {
            var root = new GameObject("Sector jump presentation");
            root.transform.SetParent(parent, false);
            var presentation = root.AddComponent<SectorJumpPresentation>();
            presentation.Initialize(ship, overlay, ships, observer, backdrop, shader);
            return presentation;
        }

        private void Initialize(Ship player, Overlay playerOverlay, IEnumerable<Ship> activeShips,
            ObserverCam playerObserver, LocaleBackdropPair skies, Shader shader)
        {
            ship = player;
            overlay = playerOverlay;
            ships = activeShips;
            observer = playerObserver;
            backdrop = skies;
            hullLayers = new int[ship.VisualRig.Silhouette.Count];
            skyCamera = NewCamera("Jump sky camera", 1 << LayerIds.Sky);
            hullCamera = NewCamera("Jump hull camera", 1 << LayerMask.NameToLayer("ShipPreview"));
            hullCamera.backgroundColor = Color.clear;
            hull = Target("Jump live hull", 512, 512);
            incomingHull = Target("Jump incoming hull", 512, 512);
            material = new Material(shader);
            material.SetTexture("_ShipImage", hull);
            material.SetTexture("_ShipImageTo", incomingHull);
            var canvas = NewCanvas();
            image = new GameObject("Jump image", typeof(RectTransform)).AddComponent<RawImage>();
            image.gameObject.layer = canvas.gameObject.layer;
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            Resize();
        }

        private Canvas NewCanvas()
        {
            var canvas = new GameObject("Jump canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = observer.Cam;
            canvas.planeDistance = observer.Cam.nearClipPlane + .1f;
            canvas.sortingOrder = -100;
            return canvas;
        }

        private Camera NewCamera(string label, int mask)
        {
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            var camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.cullingMask = mask;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        private static RenderTexture Target(string label, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = label,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            target.Create();
            return target;
        }

        public void Apply(SectorJumpProfile profile, SectorJumpState state, int seed, bool reverseLocales,
            Vector3? skyOrigin = null)
        {
            Resize();
            var aspect = (float)composed.width / composed.height;
            var origin = skyOrigin ?? ship.transform.position;
            var offset = state.JumpDirection * (state.TravelFraction * profile.skyTravelDistance);
            skyCamera.transform.position = new Vector3(origin.x + offset.x, origin.y + offset.y, -10f);
            skyCamera.aspect = aspect;
            skyCamera.orthographicSize = Mathf.Lerp(observer.Cam.orthographicSize, 12f, worldBlend);
            backdrop.Render(skyCamera, outgoing, incoming);
            RenderHull();
            material.SetTexture("_ShipImage", reverseLocales ? incomingHull : hull);
            material.SetTexture("_ShipImageTo", reverseLocales ? hull : incomingHull);
            material.SetTexture("_FromSky", reverseLocales ? incoming : outgoing);
            material.SetTexture("_ToSky", reverseLocales ? outgoing : incoming);
            SectorJumpShader.Apply(material, profile, state, aspect, composed.height, seed);
            var framing = material.GetVector("_Ship");
            framing.x = Mathf.Lerp(hullDiameter / (observer.Cam.orthographicSize * 2f), framing.x, worldBlend);
            material.SetVector("_Ship", framing);
            var previous = RenderTexture.active;
            try { Graphics.Blit(null, composed, material); }
            finally { RenderTexture.active = previous; }
            overlay.SetOpacity(state.HudAlpha);
            foreach (var active in ships)
                if (active && active.VisualRig) active.VisualRig.SetHudOpacity(state.HudAlpha);
        }

        public void SetWorldBlend(float blend)
        {
            worldBlend = Mathf.Clamp01(blend);
            image.color = new Color(1f, 1f, 1f, worldBlend);
        }

        private void Resize()
        {
            var width = Mathf.Max(2, Screen.width);
            var height = Mathf.Max(2, Screen.height);
            if (composed && composed.width == width && composed.height == height) return;
            Release(outgoing);
            Release(incoming);
            Release(composed);
            outgoing = Target("Jump outgoing sky", width, height);
            incoming = Target("Jump incoming sky", width, height);
            composed = Target("Jump composition", width, height);
            image.texture = composed;
        }

        private void RenderHull()
        {
            var renderers = ship.VisualRig.Silhouette;
            var pose = Matrix4x4.TRS(ship.transform.position, ship.transform.rotation, Vector3.one);
            var worldToPose = pose.inverse;
            var minimum = Vector3.positiveInfinity;
            var maximum = Vector3.negativeInfinity;
            var previous = RenderTexture.active;
            try
            {
                for (var i = 0; i < renderers.Count; i++)
                {
                    hullLayers[i] = renderers[i].gameObject.layer;
                    renderers[i].gameObject.layer = LayerMask.NameToLayer("ShipPreview");
                    var skinned = renderers[i] as SkinnedMeshRenderer;
                    var space = skinned && skinned.rootBone ? skinned.rootBone : renderers[i].transform;
                    var local = renderers[i].localBounds;
                    var toPose = worldToPose * space.localToWorldMatrix;
                    for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var point = toPose.MultiplyPoint3x4(local.center +
                            Vector3.Scale(local.extents, new Vector3(x, y, z)));
                        minimum = Vector3.Min(minimum, point);
                        maximum = Vector3.Max(maximum, point);
                    }
                }
                var bounds = new Bounds();
                bounds.SetMinMax(minimum, maximum);
                hullCamera.transform.rotation = ship.transform.rotation;
                hullCamera.transform.position = pose.MultiplyPoint3x4(bounds.center - Vector3.forward * 20f);
                hullCamera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f;
                hullDiameter = hullCamera.orthographicSize * 2f;
                hullCamera.aspect = 1f;
                backdrop.Render(hullCamera, hull, incomingHull);
            }
            finally
            {
                for (var i = 0; i < renderers.Count; i++) renderers[i].gameObject.layer = hullLayers[i];
                hullCamera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        private void OnDestroy()
        {
            if (overlay) overlay.SetOpacity(1f);
            if (ships != null)
                foreach (var active in ships)
                    if (active && active.VisualRig) active.VisualRig.SetHudOpacity(1f);
            Release(outgoing);
            Release(incoming);
            Release(hull);
            Release(incomingHull);
            Release(composed);
            if (material) Destroy(material);
        }

        private static void Release(RenderTexture texture)
        {
            if (!texture) return;
            texture.Release();
            Destroy(texture);
        }
    }
}
