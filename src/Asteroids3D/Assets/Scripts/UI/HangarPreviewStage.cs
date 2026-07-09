using Ships;
using Ships.Presentation;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Offscreen 3D preview for the hangar: renders the selected ship's visual rig — spinning slowly —
    /// to a RenderTexture the screen shows in a RawImage. The stage lives far from the play space on
    /// the ShipPreview layer with its own camera and light, so nothing leaks between the preview and
    /// the world. Ship switches play a scale pop (old shrinks out, new overshoots in).
    ///
    /// Shows the loadout, not just the hull, so later module visuals (engines/shields/weapons) can
    /// attach to the same rig instance.
    /// </summary>
    public sealed class HangarPreviewStage : MonoBehaviour
    {
        // The display plane sits horizontal above the menu, same convention as the arena's Y plane
        // (GamePlane.PlanePose: normal points away from the viewer, up = nose). Normal world-down,
        // nose starting screen-right — the stage's vertical turntable is then true in-plane yaw,
        // exactly like turning in game.
        private static readonly Quaternion BaseOrientation =
            Game.GamePlane.PlanePose(Vector3.down, Vector3.right);

        private const float IdleSpinDegPerSec = 20f;
        private const float PopOutSeconds = 0.12f;
        private const float PopInSeconds = 0.25f;
        private const float CameraFovDeg = 30f;
        private const int TextureSize = 768;

        private Camera stageCamera;
        private Transform anchor;       // spins; the rig clone hangs under it
        private RenderTexture texture;
        private Transform current;      // active rig clone
        private Transform retiring;     // old clone shrinking out
        private Vector3 currentTargetScale;
        private float popInT = 1f;
        private float popOutT = 1f;

        /// <summary>The texture the screen's RawImage should display.</summary>
        public Texture Texture => texture;

        /// <summary>
        /// Build a stage well below the play space. The ShipPreview layer isolates it; if the layer is
        /// missing from the project the stage still works, just without cross-visibility guarantees.
        /// </summary>
        public static HangarPreviewStage Create()
        {
            var go = new GameObject("HangarPreviewStage");
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var stage = go.AddComponent<HangarPreviewStage>();
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

            // 3/4 view: above and in front, looking down at the anchor.
            var camGo = new GameObject("PreviewCamera");
            camGo.layer = layer;
            camGo.transform.SetParent(transform, false);
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.cullingMask = 1 << layer;
            stageCamera.fieldOfView = CameraFovDeg;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = Color.clear;
            stageCamera.targetTexture = texture;

            // Key light shines roughly along the camera axis so the camera-facing hull is lit.
            var lightGo = new GameObject("PreviewLight");
            lightGo.layer = layer;
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.6f;
        }

        /// <summary>
        /// Show the loadout's selected ship: the current rig pops out, the new ship's rig clone pops
        /// in and resumes the idle spin. A null ship (or a ship with no rig) clears the stage.
        /// </summary>
        public void Show(ShipLoadout loadout)
        {
            // Retire whatever is up now.
            if (retiring) Destroy(retiring.gameObject);
            retiring = current;
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

            // Size lives on the ship prefab ROOT (the rig child inherits it in situ) — re-apply it,
            // then neutralize the rig's own authored local pose under our anchor.
            root.localPosition = Vector3.zero;
            root.localRotation = BaseOrientation;
            currentTargetScale = Vector3.Scale(rigTemplate.transform.localScale, ship.transform.localScale);
            root.localScale = Vector3.zero; // pop-in animates up to currentTargetScale

            FrameCamera(root);

            current = root;
            popInT = 0f;
        }

        private void FrameCamera(Transform rig)
        {
            // Frame from the clone's renderer bounds at its TARGET scale (it is zero-scaled right now).
            var previousScale = rig.localScale;
            rig.localScale = currentTargetScale;
            var bounds = new Bounds(anchor.position, Vector3.one);
            var renderers = rig.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                if (FramesBounds(r))
                    bounds.Encapsulate(r.bounds);
            rig.localScale = previousScale;

            // Largest single-axis extent (not the 3D diagonal — that over-frames by ~1.7x) fills
            // most of the vertical FOV.
            var extents = bounds.extents;
            var radius = Mathf.Max(Mathf.Max(extents.x, extents.y), Mathf.Max(extents.z, 0.5f));
            var distance = radius / Mathf.Tan(CameraFovDeg * 0.5f * Mathf.Deg2Rad) * 0.85f;
            var direction = new Vector3(0f, 0.55f, -1f).normalized;
            stageCamera.transform.position = bounds.center + direction * distance;
            stageCamera.transform.LookAt(bounds.center);
        }

        // Frame on hull geometry only: particle/trail renderers report garbage bounds while idle
        // (anchored at world origin), which would blow the framing up by the stage's offset.
        private static bool FramesBounds(Renderer r) => r is MeshRenderer || r is SkinnedMeshRenderer;

        private void Update()
        {
            anchor.Rotate(0f, IdleSpinDegPerSec * Time.deltaTime, 0f, Space.World);

            if (popOutT < 1f && retiring)
            {
                popOutT = Mathf.Min(1f, popOutT + Time.deltaTime / PopOutSeconds);
                retiring.localScale = Vector3.Lerp(retiring.localScale, Vector3.zero, popOutT);
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

        // Overshoots to ~1.1 then settles at 1 — the "pop".
        private static float EaseOutBack(float t)
        {
            const float k = 1.70158f;
            t -= 1f;
            return 1f + t * t * ((k + 1f) * t + k);
        }

        // Clean-hull curation: the rig also carries ship-space UI (shield ring, lock indicator),
        // a minimap marker, and audio emitters — none belong in a museum-piece preview. Thruster
        // particles stay but are dormant (pilot-command-driven; the preview never binds a ShipView).
        private static void StripNonHull(ShipVisualRig rig)
        {
            foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true))
                if (canvas.gameObject != rig.gameObject)
                    Destroy(canvas.gameObject);
            foreach (var minimap in rig.GetComponentsInChildren<MinimapLayerSetter>(true))
                if (minimap.gameObject != rig.gameObject)
                    Destroy(minimap.gameObject);
            // Disable (not destroy) audio: sources may be [RequireComponent]-pinned by their binders.
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
            if (texture) texture.Release();
        }
    }
}
