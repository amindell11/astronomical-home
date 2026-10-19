using System.Collections.Generic;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// Offscreen 3D preview for the hangar: clones the selected ship's visual rig for flight motion
    /// and renders it to a RenderTexture on the isolated ShipPreview layer. Every offered
    /// ship is framed from one camera distance — the largest ship's fit — so hull sizes compare.
    /// </summary>
    public sealed class HangarPreviewStage : MonoBehaviour
    {
        private static readonly Quaternion BaseOrientation =
            Substrate.GamePlane.PlanePose(Vector3.down, Vector3.right);

        private const float CameraFovDeg = 30f;
        private const int MaxTextureSize = 2048;
        private const int TextureMsaa = 4;
        // The approved ink weight: contour widths as drawn on the original 768 px preview texture.
        private const float InkReferenceTextureSize = 768f;
        private const float FramingMargin = 1.05f;
        private const float MinFramedRadius = 0.5f;
        private static readonly int ContourPixelsId = Shader.PropertyToID("_ContourPixels");
        private Camera stageCamera;
        private Transform anchor;
        private RenderTexture texture;
        private HangarPreviewFlight current;
        private HangarPreviewFlight retiring;
        private float sharedCameraDistance;

        public Texture Texture => texture;

        public static HangarPreviewStage Create(Transform parent, int texturePixels,
            IEnumerable<Ship> offeredShips)
        {
            var go = new GameObject("HangarPreviewStage");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var stage = go.AddComponent<HangarPreviewStage>();
            stage.BuildStage(texturePixels);
            stage.sharedCameraDistance = stage.LargestFramingDistance(offeredShips);
            return stage;
        }

        private float LargestFramingDistance(IEnumerable<Ship> ships)
        {
            var largest = 0f;
            foreach (var ship in ships)
            {
                var rigTemplate = ship.VisualRig;
                if (!rigTemplate) continue;
                var bounds = MeasureFramedBounds(rigTemplate);
                largest = Mathf.Max(largest, SolveFraming(bounds, anchor.position, CameraFovDeg).distance);
            }
            return largest;
        }

        private static int PreviewLayer()
        {
            var layer = LayerMask.NameToLayer("ShipPreview");
            if (layer < 0)
            {
                Debug.LogWarning("HangarPreviewStage: no 'ShipPreview' layer; preview isolation is best-effort.");
                layer = 0;
            }
            return layer;
        }

        private void BuildStage(int texturePixels)
        {
            var layer = PreviewLayer();
            gameObject.layer = layer;

            anchor = new GameObject("Anchor").transform;
            anchor.SetParent(transform, false);
            anchor.gameObject.layer = layer;

            var camGo = new GameObject("PreviewCamera");
            camGo.layer = layer;
            camGo.transform.SetParent(transform, false);
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.cullingMask = 1 << layer;
            stageCamera.fieldOfView = CameraFovDeg;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = Color.clear;
            Resize(texturePixels);

            var lightGo = new GameObject("PreviewLight");
            lightGo.layer = layer;
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.6f;
        }

        /// <summary>Re-renders at <paramref name="pixels"/> square, capped at <see cref="MaxTextureSize"/>; past the
        /// cap the UI draws the preview upscaled. Replaces <see cref="Texture"/> when the size changes.</summary>
        public void Resize(int pixels)
        {
            var size = Mathf.Min(pixels, MaxTextureSize);
            if (texture && texture.width == size) return;

            stageCamera.targetTexture = null;
            ReleaseTexture();
            texture = new RenderTexture(size, size, 24) { name = "HangarPreviewRT", antiAliasing = TextureMsaa };
            stageCamera.targetTexture = texture;
            ScaleInk(current);
            ScaleInk(retiring);
        }

        // Contour width is in render-target pixels; scaling with the texture holds on-screen ink weight.
        private void ScaleInk(Component rig)
        {
            if (!rig) return;
            var scale = texture.width / InkReferenceTextureSize;
            var block = new MaterialPropertyBlock();
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !materials[i].HasProperty(ContourPixelsId)) continue;
                    r.GetPropertyBlock(block, i);
                    block.SetFloat(ContourPixelsId, materials[i].GetFloat(ContourPixelsId) * scale);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        public void Show(ShipLoadout loadout)
        {
            if (retiring) Destroy(retiring.gameObject);
            retiring = current;
            if (retiring) retiring.Depart();
            current = null;

            var ship = loadout?.Ship;
            var rigTemplate = ship ? ship.VisualRig : null;
            if (!rigTemplate) return;

            var clone = Instantiate(rigTemplate, anchor, false);
            var root = clone.transform;
            root.gameObject.name = ship.name + "_Preview";
            StripNonHull(clone);
            SetLayerRecursive(root, gameObject.layer);
            ScaleInk(root);

            // Include the prefab root's scale, which the visual-only clone leaves behind.
            root.localScale = rigTemplate.transform.lossyScale;

            FrameCamera(rigTemplate);

            current = root.gameObject.AddComponent<HangarPreviewFlight>();
            var radius = Vector3.Distance(stageCamera.transform.position, anchor.position)
                * Mathf.Sin(CameraFovDeg * .5f * Mathf.Deg2Rad) / FramingMargin;
            current.Initialize(radius, BaseOrientation);
        }

        private void FrameCamera(ShipVisualRig rigTemplate)
        {
            var bounds = MeasureFramedBounds(rigTemplate);
            var (focus, ownDistance) = SolveFraming(bounds, anchor.position, CameraFovDeg);
            // A ship outside the offer is never framed closer than its own fit.
            var distance = Mathf.Max(ownDistance, sharedCameraDistance);
            var direction = new Vector3(0f, 0.55f, -1f).normalized;
            stageCamera.transform.position = focus + direction * distance;
            stageCamera.transform.LookAt(focus);
        }

        private Bounds MeasureFramedBounds(ShipVisualRig rigTemplate)
        {
            var rig = rigTemplate.transform;
            var peakPose = anchor.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero,
                BaseOrientation, rig.lossyScale);
            var templateToPeak = peakPose * rig.worldToLocalMatrix;
            var bounds = new Bounds(anchor.position, Vector3.one);
            foreach (var r in rigTemplate.Silhouette)
            {
                // A skinned mesh's stored bounds are relative to its root bone.
                var skinned = r as SkinnedMeshRenderer;
                var (local, space) = skinned
                    ? (skinned.localBounds, skinned.rootBone ? skinned.rootBone : skinned.transform)
                    : (r.localBounds, r.transform);
                var toPeak = templateToPeak * space.localToWorldMatrix;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(toPeak.MultiplyPoint3x4(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            }
            return bounds;
        }

        // An anchor-centered sphere covers banking too; the margin leaves room for idle drift.
        internal static (Vector3 focus, float distance) SolveFraming(
            Bounds bounds, Vector3 anchorPosition, float fovDeg)
        {
            var spinRadiusSq = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = new Vector2(
                    bounds.center.x - anchorPosition.x + x * bounds.extents.x,
                    bounds.center.z - anchorPosition.z + z * bounds.extents.z);
                spinRadiusSq = Mathf.Max(spinRadiusSq, corner.sqrMagnitude);
            }
            var radius = Mathf.Max(
                Mathf.Sqrt(spinRadiusSq + Mathf.Pow(Mathf.Abs(bounds.center.y - anchorPosition.y) + bounds.extents.y, 2f)),
                MinFramedRadius);
            var distance = radius * FramingMargin / Mathf.Sin(fovDeg * 0.5f * Mathf.Deg2Rad);
            return (anchorPosition, distance);
        }

        // Thrusters remain dormant; only wings receive preview flight input.
        private static void StripNonHull(ShipVisualRig rig)
        {
            foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true))
                if (canvas.gameObject != rig.gameObject)
                    DestroyImmediate(canvas.gameObject);
            foreach (var minimap in rig.GetComponentsInChildren<MinimapShipMarker>(true))
                if (minimap && minimap.gameObject != rig.gameObject)
                    DestroyImmediate(minimap.gameObject);
            // Disable, don't destroy: sources may be [RequireComponent]-pinned by their binders.
            foreach (var audio in rig.GetComponentsInChildren<AudioSource>(true))
            {
                audio.playOnAwake = false;
                audio.enabled = false;
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private void OnDestroy()
        {
            if (stageCamera) stageCamera.targetTexture = null;
            ReleaseTexture();
        }

        private void ReleaseTexture()
        {
            if (!texture) return;
            texture.Release();
            Destroy(texture);
        }
    }
}
