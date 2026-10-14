using System.Collections.Generic;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// Offscreen 3D preview for the hangar: clones the selected ship's visual rig onto a spinning
    /// anchor and renders it to a RenderTexture on the isolated ShipPreview layer. Every offered
    /// ship is framed from one camera distance — the largest ship's fit — so hull sizes compare.
    /// </summary>
    public sealed class HangarPreviewStage : MonoBehaviour
    {
        // Same plane-dweller convention as the arena (GamePlane.PlanePose): normal away from the
        // viewer (world-down), nose screen-right — world-Y anchor spin is then true in-plane yaw.
        private static readonly Quaternion BaseOrientation =
            Substrate.GamePlane.PlanePose(Vector3.down, Vector3.right);

        private const float IdleSpinDegPerSec = 20f;
        private const float PopOutSeconds = 0.12f;
        private const float PopInSeconds = 0.25f;
        private const float CameraFovDeg = 30f;
        private const int MaxTextureSize = 2560;
        private const int TextureMsaa = 8;
        // The approved ink weight: contour widths as drawn on the original 768 px preview texture.
        private const float InkReferenceTextureSize = 768f;
        private const float FramingMargin = 1.05f;
        private const float MinFramedRadius = 0.5f;
        private static readonly int ContourPixelsId = Shader.PropertyToID("_ContourPixels");
        // EaseOutBack (k = 1.70158) peaks ~10% past target scale mid-pop-in.
        internal const float PopInOvershoot = 1.101f;

        private bool continueSpinOnSwitch;
        private Camera stageCamera;
        private Transform anchor;
        private RenderTexture texture;
        private Transform current;
        private Transform retiring;
        private Vector3 currentTargetScale;
        private Vector3 retiringStartScale;
        private float popInT = 1f;
        private float popOutT = 1f;
        private float sharedCameraDistance;

        public Texture Texture => texture;

        public static HangarPreviewStage Create(bool continueSpinOnSwitch, Transform parent, int texturePixels,
            IEnumerable<Ship> offeredShips)
        {
            var go = new GameObject("HangarPreviewStage");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var stage = go.AddComponent<HangarPreviewStage>();
            stage.continueSpinOnSwitch = continueSpinOnSwitch;
            stage.BuildStage(texturePixels);
            stage.sharedCameraDistance = stage.LargestFramingDistance(offeredShips);
            return stage;
        }

        private float LargestFramingDistance(IEnumerable<Ship> ships)
        {
            var largest = 0f;
            foreach (var ship in ships)
            {
                var rigTemplate = ship.GetComponentInChildren<ShipVisualRig>(true);
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

        /// <summary>Re-renders at <paramref name="pixels"/> square (capped), so the UI never upscales the
        /// preview. Replaces <see cref="Texture"/> when the size changes.</summary>
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
        private void ScaleInk(Transform rig)
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

        /// <summary>Swap the preview to the loadout's ship. A null ship (or rigless ship) clears the stage.</summary>
        public void Show(ShipLoadout loadout)
        {
            if (retiring) Destroy(retiring.gameObject);
            retiring = current;
            retiringStartScale = retiring ? retiring.localScale : Vector3.zero;
            popOutT = retiring ? 0f : 1f;
            current = null;

            var ship = loadout?.Ship;
            var rigTemplate = ship ? ship.GetComponentInChildren<ShipVisualRig>(true) : null;
            if (!rigTemplate) return;

            var clone = Instantiate(rigTemplate, anchor, false);
            var root = clone.transform;
            root.gameObject.name = ship.name + "_Preview";
            StripNonHull(clone);
            SetLayerRecursive(root, gameObject.layer);
            ScaleInk(root);

            // Ship size lives on the prefab root, which the clone left behind — re-apply the rig's
            // full in-prefab scale. Under the spinning anchor, BaseOrientation as LOCAL rotation
            // inherits the current spin phase (turntable continues through a switch); countering the
            // anchor makes the ship enter at the canonical pose instead.
            root.localPosition = Vector3.zero;
            root.localRotation = continueSpinOnSwitch
                ? BaseOrientation
                : Quaternion.Inverse(anchor.rotation) * BaseOrientation;
            currentTargetScale = rigTemplate.transform.lossyScale;
            root.localScale = Vector3.zero;

            FrameCamera(rigTemplate);

            current = root;
            popInT = 0f;
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

        // Mesh-stored bounds placed at peak pop-in scale and canonical pose: no clone, spin-independent.
        private Bounds MeasureFramedBounds(ShipVisualRig rigTemplate)
        {
            var rig = rigTemplate.transform;
            var peakPose = anchor.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero,
                Quaternion.Inverse(anchor.rotation) * BaseOrientation, rig.lossyScale * PopInOvershoot);
            var templateToPeak = peakPose * rig.worldToLocalMatrix;
            var bounds = new Bounds(anchor.position, Vector3.one);
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (!FramesBounds(r)) continue;
                // A skinned mesh's stored bounds are relative to its root bone.
                var (local, space) = r is SkinnedMeshRenderer skinned
                    ? (skinned.localBounds, skinned.rootBone ? skinned.rootBone : skinned.transform)
                    : (r.GetComponent<MeshFilter>().sharedMesh.bounds, r.transform);
                var toPeak = templateToPeak * space.localToWorldMatrix;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(toPeak.MultiplyPoint3x4(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            }
            return bounds;
        }

        // The turntable spins the hull about the anchor's vertical axis, so the camera must clear
        // the volume swept over a full revolution — the enclosing sphere of every corner's spin
        // circle — fit with sin (a tan fit clips whatever pokes nearer than the focus plane).
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
                Mathf.Sqrt(spinRadiusSq + bounds.extents.y * bounds.extents.y), MinFramedRadius);
            var distance = radius * FramingMargin / Mathf.Sin(fovDeg * 0.5f * Mathf.Deg2Rad);
            var focus = new Vector3(anchorPosition.x, bounds.center.y, anchorPosition.z);
            return (focus, distance);
        }

        // Hull meshes only: the minimap marker is a MeshRenderer but not part of the silhouette.
        private static bool FramesBounds(Renderer r) =>
            (r is MeshRenderer || r is SkinnedMeshRenderer) && !r.GetComponentInParent<MinimapShipMarker>(true);

        private void Update()
        {
            anchor.Rotate(0f, IdleSpinDegPerSec * Time.deltaTime, 0f, Space.World);

            if (popOutT < 1f && retiring)
            {
                popOutT = Mathf.Min(1f, popOutT + Time.deltaTime / PopOutSeconds);
                retiring.localScale = Vector3.Lerp(retiringStartScale, Vector3.zero, popOutT);
                if (popOutT >= 1f)
                {
                    Destroy(retiring.gameObject);
                    retiring = null;
                }
            }

            if (popInT < 1f && current)
            {
                popInT = Mathf.Min(1f, popInT + Time.deltaTime / PopInSeconds);
                current.localScale = currentTargetScale * EaseOutBack(popInT);
            }
        }

        internal static float EaseOutBack(float t)
        {
            const float k = 1.70158f;
            t -= 1f;
            return 1f + t * t * ((k + 1f) * t + k);
        }

        // Thruster particles stay but are dormant — the preview never binds a ShipView.
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
