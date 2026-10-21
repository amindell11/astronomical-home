using System.Collections.Generic;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// Offscreen 3D preview for the hangar: renders pooled clones of each ship's visual rig to a RenderTexture
    /// on the isolated ShipPreview layer. The texture spans the whole surface ships fly across; an off-axis camera
    /// holds the settled ship inside a framing square on it. The camera never moves; each clone is scaled to one
    /// framing sphere, so every hull fills the same share of the square. A <see cref="HangarCarousel"/> places the clones.
    /// </summary>
    public sealed class HangarPreviewStage : MonoBehaviour
    {
        private static readonly Quaternion BaseOrientation =
            Substrate.GamePlane.PlanePose(Vector3.down, new Vector3(-.32f, 0f, -1f));

        private const float CameraFovDeg = 30f;
        private const int MaxTextureSize = 2048;
        private const int TextureMsaa = 4;
        // The approved ink weight: contour widths as drawn on the original 768 px preview texture.
        private const float InkReferenceTextureSize = 768f;
        private const float FramingMargin = 1.05f;
        private const float MinFramedRadius = 0.5f;
        private const float FramedRadius = 1f;
        private static readonly int ContourPixelsId = Shader.PropertyToID("_ContourPixels");
        private readonly Dictionary<Ship, HangarPreviewFlight> clones = new();
        private readonly HangarCarousel carousel = new();
        private Camera stageCamera;
        private Transform anchor;
        private RenderTexture texture;
        private float inkFramePixels;
        private bool showing;

        public Texture Texture => texture;

        public static HangarPreviewStage Create(Transform parent, Vector2Int surfacePixels, Rect frame,
            IEnumerable<Ship> offeredShips)
        {
            var go = new GameObject("HangarPreviewStage");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var stage = go.AddComponent<HangarPreviewStage>();
            stage.BuildStage(surfacePixels, frame);
            foreach (var ship in offeredShips)
                stage.CloneOf(ship);
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

        private void BuildStage(Vector2Int surfacePixels, Rect frame)
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
            var direction = new Vector3(0f, 0.55f, -1f).normalized;
            stageCamera.transform.position = anchor.position + direction * CameraDistance(FramedRadius, CameraFovDeg);
            stageCamera.transform.LookAt(anchor.position);
            Resize(surfacePixels, frame);

            var lightGo = new GameObject("PreviewLight");
            lightGo.layer = layer;
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.6f;
        }

        /// <summary>Renders a <paramref name="surface"/>-pixel target, long side capped at <see cref="MaxTextureSize"/>
        /// (past the cap the UI draws it upscaled), with the framing square at <paramref name="frame"/> in normalized
        /// surface coordinates. Replaces <see cref="Texture"/> when the size changes.</summary>
        public void Resize(Vector2Int surface, Rect frame)
        {
            var fit = Mathf.Min(1f, (float)MaxTextureSize / Mathf.Max(surface.x, surface.y));
            var width = Mathf.Max(1, Mathf.RoundToInt(surface.x * fit));
            var height = Mathf.Max(1, Mathf.RoundToInt(surface.y * fit));
            Project(frame);
            var framePixels = frame.width * width;
            if (texture && texture.width == width && texture.height == height && framePixels == inkFramePixels) return;

            inkFramePixels = framePixels;
            if (!texture || texture.width != width || texture.height != height)
            {
                stageCamera.targetTexture = null;
                ReleaseTexture();
                texture = new RenderTexture(width, height, 24) { name = "HangarPreviewRT", antiAliasing = TextureMsaa };
                stageCamera.targetTexture = texture;
            }
            foreach (var clone in clones.Values)
                ScaleInk(clone);
        }

        // Off-axis frustum: the framing square keeps a CameraFovDeg view wherever it sits on the surface.
        private void Project(Rect frame)
        {
            var near = stageCamera.nearClipPlane;
            var half = near * Mathf.Tan(CameraFovDeg * .5f * Mathf.Deg2Rad);
            var left = -half - 2f * half * frame.x / frame.width;
            var bottom = -half - 2f * half * frame.y / frame.height;
            stageCamera.projectionMatrix = Matrix4x4.Frustum(left, left + 2f * half / frame.width,
                bottom, bottom + 2f * half / frame.height, near, stageCamera.farClipPlane);
        }

        // Contour width is in render-target pixels; scaling with the framing square holds on-screen ink weight.
        private void ScaleInk(Component rig)
        {
            var scale = inkFramePixels / InkReferenceTextureSize;
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

        /// <summary>The first call plays the entry swoop; later calls step the carousel toward the new pick.</summary>
        public void Show(ShipLoadout loadout, bool backwards)
        {
            var ship = loadout?.Ship;
            if (!showing)
            {
                showing = true;
                carousel.Start(ship);
                var clone = CloneOf(ship);
                if (clone) clone.Enter();
                return;
            }
            carousel.Step(ship, backwards ? -1 : 1);
        }

        private void Update()
        {
            if (!showing) return;
            var dt = Time.deltaTime;
            carousel.Advance(dt);
            var nearest = Mathf.FloorToInt(carousel.Position);
            // The path ends where one press's handoff ends; the spring's tail beyond it is off-frame.
            var pathEnd = 1f - HangarCarousel.SettledOffset;
            var behind = carousel.Position - nearest < pathEnd ? CloneOf(carousel.At(nearest)) : null;
            var ahead = nearest + 1 - carousel.Position < pathEnd ? CloneOf(carousel.At(nearest + 1)) : null;
            foreach (var clone in clones.Values)
                if (clone != behind && clone != ahead && clone.gameObject.activeSelf)
                    clone.Hide();
            if (behind) behind.Place(nearest - carousel.Position, carousel.Velocity, dt);
            if (ahead) ahead.Place(nearest + 1 - carousel.Position, carousel.Velocity, dt);
        }

        // Built once per ship and kept: a burst of presses never instantiates or destroys a clone.
        private HangarPreviewFlight CloneOf(Ship ship)
        {
            var rigTemplate = ship ? ship.VisualRig : null;
            if (!rigTemplate) return null;
            if (clones.TryGetValue(ship, out var pooled)) return pooled;

            var clone = Instantiate(rigTemplate, anchor, false);
            var root = clone.transform;
            root.gameObject.name = ship.name + "_Preview";
            StripNonHull(clone);
            SetLayerRecursive(root, gameObject.layer);
            ScaleInk(root);

            // The prefab root's scale, which the visual-only clone leaves behind, fitted to the framing sphere.
            var ownDistance = SolveFraming(MeasureFramedBounds(rigTemplate), anchor.position, CameraFovDeg).distance;
            root.localScale = rigTemplate.transform.lossyScale
                * (CameraDistance(FramedRadius, CameraFovDeg) / ownDistance);

            var flight = root.gameObject.AddComponent<HangarPreviewFlight>();
            flight.Initialize(FramedRadius, BaseOrientation);
            flight.Hide();
            clones[ship] = flight;
            return flight;
        }

        private Bounds MeasureFramedBounds(ShipVisualRig rigTemplate)
        {
            var rig = rigTemplate.transform;
            var pose = anchor.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero,
                BaseOrientation, rig.lossyScale);
            var templateToPose = pose * rig.worldToLocalMatrix;
            var bounds = new Bounds(anchor.position, Vector3.one);
            foreach (var r in rigTemplate.Silhouette)
            {
                // A skinned mesh's stored bounds are relative to its root bone.
                var skinned = r as SkinnedMeshRenderer;
                var (local, space) = skinned
                    ? (skinned.localBounds, skinned.rootBone ? skinned.rootBone : skinned.transform)
                    : (r.localBounds, r.transform);
                var toPose = templateToPose * space.localToWorldMatrix;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(toPose.MultiplyPoint3x4(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            }
            return bounds;
        }

        // An anchor-centered sphere covers banking too; the margin leaves room for idle drift.
        internal static (Vector3 focus, float distance) SolveFraming(
            Bounds bounds, Vector3 anchorPosition, float fovDeg)
        {
            var planarRadiusSq = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = new Vector2(
                    bounds.center.x - anchorPosition.x + x * bounds.extents.x,
                    bounds.center.z - anchorPosition.z + z * bounds.extents.z);
                planarRadiusSq = Mathf.Max(planarRadiusSq, corner.sqrMagnitude);
            }
            var radius = Mathf.Max(
                Mathf.Sqrt(planarRadiusSq + Mathf.Pow(Mathf.Abs(bounds.center.y - anchorPosition.y) + bounds.extents.y, 2f)),
                MinFramedRadius);
            return (anchorPosition, CameraDistance(radius, fovDeg));
        }

        private static float CameraDistance(float radius, float fovDeg) =>
            radius * FramingMargin / Mathf.Sin(fovDeg * 0.5f * Mathf.Deg2Rad);

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
