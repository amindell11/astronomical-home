using System;
using System.Collections.Generic;
using Cameras;
using Ships;
using Substrate;
using Substrate.Services.Locales;
using UI;
using UnityEngine;

namespace Game.Travel
{
    /// <summary>
    /// Applies a jump state to live locale skies, the ship's hull and the actual HUD.
    /// Its render targets belong to the presentation, so previews and gameplay use the same renderer.
    /// The jump image draws through <see cref="SectorJumpFeature"/> on the observer camera after post-processing.
    /// The caller owns simulation time, destination loading, the world blend, the ship and camera pose, and
    /// restoring the HUD once the jump ends.
    /// </summary>
    public sealed class SectorJumpPresentation : MonoBehaviour
    {
        public const int StreakSeed = 1099;

        private Ship ship;
        private Overlay overlay;
        private IEnumerable<Ship> ships;
        private ObserverCam observer;
        private LocaleBackdropPair backdrop;
        private SectorJumpFeature feature;
        private Camera boundCamera;
        private Camera skyCamera;
        private Camera hullCamera;
        private RenderTexture outgoing;
        private RenderTexture incoming;
        private RenderTexture hull;
        private RenderTexture incomingHull;
        private Material material;
        private int[] hullLayers;
        private float worldBlend = 1f;
        private float hullDiameter;

        public static SectorJumpPresentation Create(Transform parent, Ship ship, Overlay overlay,
            IEnumerable<Ship> ships, ObserverCam observer, LocaleBackdropPair backdrop, SectorJumpFeature feature)
        {
            if (!feature.JumpShader)
                throw new InvalidOperationException($"Sector jump feature '{feature.name}' has no jump shader.");
            var root = new GameObject("Sector jump presentation");
            root.transform.SetParent(parent, false);
            var presentation = root.AddComponent<SectorJumpPresentation>();
            presentation.Initialize(ship, overlay, ships, observer, backdrop, feature);
            return presentation;
        }

        private void Initialize(Ship player, Overlay playerOverlay, IEnumerable<Ship> activeShips,
            ObserverCam playerObserver, LocaleBackdropPair skies, SectorJumpFeature jumpFeature)
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
            material = new Material(jumpFeature.JumpShader);
            material.SetTexture("_ShipImage", hull);
            material.SetTexture("_ShipImageTo", incomingHull);
            jumpFeature.Bind(observer.Cam, new SectorJumpPass(material));
            feature = jumpFeature;
            boundCamera = observer.Cam;
            Resize();
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
            var aspect = (float)outgoing.width / outgoing.height;
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
            SectorJumpShader.Apply(material, profile, state, aspect, outgoing.height, seed);
            var framing = material.GetVector("_Ship");
            framing.x = Mathf.Lerp(hullDiameter / (observer.Cam.orthographicSize * 2f), framing.x, worldBlend);
            material.SetVector("_Ship", framing);
            overlay.SetOpacity(state.HudAlpha);
            foreach (var active in ships)
                if (active && active.VisualRig) active.VisualRig.SetHudOpacity(state.HudAlpha);
        }

        public void SetWorldBlend(float blend)
        {
            worldBlend = Mathf.Clamp01(blend);
            material.SetFloat("_Opacity", worldBlend);
        }

        private void Resize()
        {
            var width = Mathf.Max(2, observer.Cam.pixelWidth);
            var height = Mathf.Max(2, observer.Cam.pixelHeight);
            if (outgoing && outgoing.width == width && outgoing.height == height) return;
            Release(outgoing);
            Release(incoming);
            outgoing = Target("Jump outgoing sky", width, height);
            incoming = Target("Jump incoming sky", width, height);
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
            if (feature) feature.Unbind(boundCamera);
            Release(outgoing);
            Release(incoming);
            Release(hull);
            Release(incomingHull);
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
