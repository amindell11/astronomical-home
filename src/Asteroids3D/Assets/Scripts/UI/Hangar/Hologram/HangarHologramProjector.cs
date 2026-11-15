using System;
using System.Collections.Generic;
using Ships;
using UI.Projection;
using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// Owns the hangar preview stage and shows the canopy effect step's image of the stage on the
    /// quad, which sits at an offset from the projector lens, at the profile's opacity. It tells that
    /// step where the image shows and when a ship switch happened, and flickers the image and the cone
    /// together. A profile with a cone has it placed at the lens, stretched to the settled ship. The
    /// camera handed over on <see cref="Fit"/> also runs that image's pass. Ship steps reach it through
    /// <see cref="Select"/>.
    /// </summary>
    public sealed class HangarHologramProjector : MonoBehaviour
    {
        private const float GlitchSeconds = .35f;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int LevelId = Shader.PropertyToID("_Level");
        private static readonly int LeanId = Shader.PropertyToID("_Lean");
        private static readonly Vector3[] QuadCorners =
        {
            new(-.5f, -.5f, 0f), new(-.5f, .5f, 0f), new(.5f, -.5f, 0f), new(.5f, .5f, 0f)
        };

        [Tooltip("Hologram restyle, layer opacity, canopy strengths and cone.")]
        [SerializeField] private HangarHologramProfile profile;

        [Tooltip("Quad at the lens, parented here. A 1×1 centred mesh; the hologram image is drawn on it.")]
        [SerializeField] private MeshRenderer surface;

        [Tooltip("Parent for the cone, at the lens. The cone prefab's base sits at this origin.")]
        [SerializeField] private Transform coneAnchor;

        [Tooltip("Runs the canopy effect step on the stage image for the surface.")]
        [SerializeField] private CanopyImage image;

        [Tooltip("Width and height of the quad, in world units.")]
        [SerializeField] private Vector2 surfaceSize = new(9f, 6f);

        [Tooltip("Side of the settled ship's square, in world units. It sits horizontally centred on the surface.")]
        [SerializeField, Min(.01f)] private float frameSide = 4f;

        [Tooltip("Height of the square's bottom edge above the surface's bottom, as a share of the surface height.")]
        [SerializeField, Range(0f, 1f)] private float frameBottom = .1f;

        [Tooltip("Where the square's bottom centre sits from the lens, in world units.")]
        [SerializeField] private Vector2 frameOffset;

        private MaterialPropertyBlock block;
        private HangarPreviewStage stage;
        private Renderer[] coneRenderers = Array.Empty<Renderer>();
        private Camera host;
        private bool fitted;
        private float switchedAt = float.NegativeInfinity;
        private Vector2 apparent;
        // Normalized on the surface; square on screen, as the stage's off-axis framing requires.
        private Rect frame;

        public Rect Frame => frame;
        internal HangarPreviewStage Stage => stage;

        private void Awake()
        {
            if (!profile || !surface || !coneAnchor || !image)
                throw new InvalidOperationException(
                    "HangarHologramProjector needs a profile, surface, cone anchor and canopy image.");
            block = new MaterialPropertyBlock();
            var width = frameSide / surfaceSize.x;
            frame = new Rect(.5f - width * .5f, frameBottom, width, frameSide / surfaceSize.y);
            surface.transform.localScale = new Vector3(surfaceSize.x, surfaceSize.y, 1f);
            surface.transform.localPosition = new Vector3(frameOffset.x,
                frameOffset.y + (.5f - frameBottom) * surfaceSize.y, 0f);
        }

        public void Open(IEnumerable<Ship> offered, Ship selected)
        {
            stage = HangarPreviewStage.Create(transform, new Vector2Int(64, 64), frame, offered, profile.Restyle,
                profile.Resolve);
            if (profile.ProjectorCone)
            {
                var cone = Instantiate(profile.ProjectorCone, coneAnchor);
                cone.transform.localPosition = Vector3.zero;
                cone.transform.localScale = new Vector3(2f * frameSide, frameOffset.y + frameSide * .5f, 1f);
                coneRenderers = cone.GetComponentsInChildren<Renderer>(true);
            }
            stage.Show(selected, backwards: false);
        }

        public void Select(Ship ship, int step)
        {
            switchedAt = Time.unscaledTime;
            stage.Show(ship, backwards: step < 0);
        }

        public void Fit(Camera camera)
        {
            if (!stage) return;
            if (!fitted)
            {
                fitted = true;
                host = camera;
                image.Initialize(camera, () => stage.Texture, () => profile.Canopy, Footprint, TransitionAge);
            }
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in QuadCorners)
            {
                var screen = camera.WorldToScreenPoint(surface.transform.TransformPoint(corner));
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            apparent = max - min;
            stage.Resize(Vector2Int.CeilToInt(apparent), frame);
        }

        private CanopyScreen Footprint() =>
            new(apparent, new Vector2(host.pixelWidth, host.pixelHeight));

        private float TransitionAge()
        {
            var age = Time.unscaledTime - switchedAt;
            return age < GlitchSeconds ? age : -1f;
        }

        private void LateUpdate()
        {
            if (!stage) return;
            var level = 1f - profile.FlickerDepth * Mathf.PerlinNoise(Time.unscaledTime * profile.FlickerRate, 0f);
            surface.GetPropertyBlock(block);
            block.SetTexture(MainTexId, image.Output);
            block.SetFloat(OpacityId, profile.LayerOpacity * level);
            surface.SetPropertyBlock(block);
            var lean = frameOffset.x / frameSide;
            foreach (var renderer in coneRenderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetFloat(LevelId, level);
                block.SetFloat(LeanId, lean);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
