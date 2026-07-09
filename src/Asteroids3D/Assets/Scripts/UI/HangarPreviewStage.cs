using Ships;
using Ships.Presentation;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Offscreen 3D preview for the hangar: clones the selected ship's visual rig onto a spinning
    /// anchor and renders it to a RenderTexture on the isolated ShipPreview layer.
    /// </summary>
    public sealed class HangarPreviewStage : MonoBehaviour
    {
        // Same plane-dweller convention as the arena (GamePlane.PlanePose): normal away from the
        // viewer (world-down), nose screen-right — world-Y anchor spin is then true in-plane yaw.
        private static readonly Quaternion BaseOrientation =
            Game.GamePlane.PlanePose(Vector3.down, Vector3.right);

        private const float IdleSpinDegPerSec = 20f;
        private const float PopOutSeconds = 0.12f;
        private const float PopInSeconds = 0.25f;
        private const float CameraFovDeg = 30f;
        private const int TextureSize = 768;

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

        public Texture Texture => texture;

        public static HangarPreviewStage Create(bool continueSpinOnSwitch)
        {
            var go = new GameObject("HangarPreviewStage");
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var stage = go.AddComponent<HangarPreviewStage>();
            stage.continueSpinOnSwitch = continueSpinOnSwitch;
            stage.BuildStage();
            return stage;
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

        private void BuildStage()
        {
            var layer = PreviewLayer();
            gameObject.layer = layer;

            texture = new RenderTexture(TextureSize, TextureSize, 16) { name = "HangarPreviewRT" };

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
            stageCamera.targetTexture = texture;

            var lightGo = new GameObject("PreviewLight");
            lightGo.layer = layer;
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.6f;
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

            FrameCamera(root);

            current = root;
            popInT = 0f;
        }

        private void FrameCamera(Transform rig)
        {
            // Measure at target scale and canonical pose — the clone is still zero-scaled for the
            // pop-in, and its world bounds vary with the turntable angle of the click (an elongated
            // hull measured broadside frames larger than nose-on).
            var previousScale = rig.localScale;
            var previousRotation = rig.localRotation;
            rig.localScale = currentTargetScale;
            rig.rotation = BaseOrientation;
            var bounds = new Bounds(anchor.position, Vector3.one);
            var renderers = rig.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                if (FramesBounds(r))
                    bounds.Encapsulate(r.bounds);
            rig.localScale = previousScale;
            rig.localRotation = previousRotation;

            // Largest single-axis extent — the 3D diagonal over-frames by ~1.7x.
            var extents = bounds.extents;
            var radius = Mathf.Max(Mathf.Max(extents.x, extents.y), Mathf.Max(extents.z, 0.5f));
            var distance = radius / Mathf.Tan(CameraFovDeg * 0.5f * Mathf.Deg2Rad) * 0.85f;
            var direction = new Vector3(0f, 0.55f, -1f).normalized;
            stageCamera.transform.position = bounds.center + direction * distance;
            stageCamera.transform.LookAt(bounds.center);
        }

        // Idle particle/trail renderers report bounds anchored at world origin, which would blow
        // the framing by the stage's offset — frame on hull meshes only.
        private static bool FramesBounds(Renderer r) => r is MeshRenderer || r is SkinnedMeshRenderer;

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

        private static float EaseOutBack(float t)
        {
            const float k = 1.70158f;
            t -= 1f;
            return 1f + t * t * ((k + 1f) * t + k);
        }

        // Thruster particles stay but are dormant — the preview never binds a ShipView.
        // Immediate destroy: FrameCamera measures the clone's renderers this same frame, and a
        // deferred Destroy would leave the minimap marker's MeshRenderer in the bounds pass.
        private static void StripNonHull(ShipVisualRig rig)
        {
            foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true))
                if (canvas.gameObject != rig.gameObject)
                    DestroyImmediate(canvas.gameObject);
            foreach (var minimap in rig.GetComponentsInChildren<MinimapLayerSetter>(true))
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
            if (texture)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
