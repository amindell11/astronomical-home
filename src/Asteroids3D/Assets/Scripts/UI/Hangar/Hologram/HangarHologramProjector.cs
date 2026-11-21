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
    /// together. The camera handed over on <see cref="Fit"/> also runs that image's pass. Ship steps
    /// reach it through <see cref="Select"/>.
    /// </summary>
    public sealed class HangarHologramProjector : MonoBehaviour
    {
        private const float GlitchSeconds = .35f;
        private const float SwitchSeconds = .85f;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int LevelId = Shader.PropertyToID("_Level");
        private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        private static readonly int ReachApexId = Shader.PropertyToID("_ReachApex");
        private static readonly int ReachUnitsId = Shader.PropertyToID("_ReachUnits");
        private static readonly int ReachAxisId = Shader.PropertyToID("_ReachAxis");
        private static readonly int ReachAngleId = Shader.PropertyToID("_ReachAngle");
        private static readonly int ReachLengthId = Shader.PropertyToID("_ReachLength");
        private static readonly int ReachWidthId = Shader.PropertyToID("_ReachWidth");
        private static readonly int ReachSoftnessId = Shader.PropertyToID("_ReachSoftness");
        private static readonly int SputterId = Shader.PropertyToID("_Sputter");
        private static readonly int SputterLinesId = Shader.PropertyToID("_SputterLines");
        private static readonly int EdgeFringeId = Shader.PropertyToID("_EdgeFringe");
        private static readonly int FringeColorId = Shader.PropertyToID("_FringeColor");
        private static readonly int ApexId = Shader.PropertyToID("_Apex");
        private static readonly int QuadUnitsId = Shader.PropertyToID("_QuadUnits");
        private static readonly int SourceRadiusId = Shader.PropertyToID("_SourceRadius");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int ConeColorId = Shader.PropertyToID("_ConeColor");
        private static readonly int ConeLightId = Shader.PropertyToID("_ConeLight");
        private static readonly Vector3[] QuadCorners =
        {
            new(-.5f, -.5f, 0f), new(-.5f, .5f, 0f), new(.5f, -.5f, 0f), new(.5f, .5f, 0f)
        };

        [Tooltip("Hologram restyle, layer opacity, canopy strengths and cone.")]
        [SerializeField] private HangarHologramProfile profile;

        [Tooltip("Quad at the lens, parented here. A 1×1 centred mesh; the hologram image is drawn on it.")]
        [SerializeField] private MeshRenderer surface;

        [Tooltip("Parent for the cone, at the lens.")]
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
        private Transform coneRoot;
        private Renderer[] coneRenderers = Array.Empty<Renderer>();
        private Camera host;
        private bool fitted;
        private float switchedAt = float.NegativeInfinity;
        private float level = 1f;
        private Vector2 coneApex;
        private Vector2 coneUnits;
        private Vector2 apparent;
        // Normalized on the surface; square on screen, as the stage's off-axis framing requires.
        private Rect frame;

        public Rect Frame => frame;
        internal float Level => level;
        internal Color BeamColor => profile.Resolve.GetColor(LineColorId);
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
            if (!profile.ProjectorCone) return;
            var cone = Instantiate(profile.ProjectorCone, coneAnchor);
            coneRoot = cone.transform;
            coneRenderers = cone.GetComponentsInChildren<Renderer>(true);
            PlaceCone(Reach().Axis);
        }

        public void Open(IEnumerable<Ship> offered, Ship selected)
        {
            stage = HangarPreviewStage.Create(transform, new Vector2Int(64, 64), frame, offered, profile.Restyle,
                profile.Resolve);
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

        private HangarReach Reach()
        {
            var centre = new Vector2(frameOffset.x, frameOffset.y + frameSide * .5f);
            var halfAngle = profile.ReachHalfAngle * Mathf.Deg2Rad;
            return new HangarReach(centre.normalized, halfAngle, profile.ReachLength, profile.ReachWidth,
                profile.ReachSoftness);
        }

        private void PlaceCone(Vector2 axis)
        {
            var radius = profile.BeamSourceRadius;
            var below = 1.3f * radius;
            var halfWidth = Mathf.Max(1.3f * (radius + profile.BeamLength * Mathf.Tan(profile.BeamHalfAngle * Mathf.Deg2Rad)),
                3.6f * radius);
            var width = 2f * halfWidth;
            var height = profile.BeamLength + below;
            coneUnits = new Vector2(width, height);
            coneApex = new Vector2(.5f, below / height);
            if (!coneRoot) return;
            coneRoot.localScale = new Vector3(width, height, 1f);
            coneRoot.localPosition = new Vector3(-axis.x * below, -axis.y * below, 0f);
            coneRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-axis.x, axis.y) * Mathf.Rad2Deg);
        }

        private float SwitchDip(float time)
        {
            var age = time - switchedAt;
            if (age >= SwitchSeconds) return 1f;
            var noise = Mathf.PerlinNoise(time * 31f, 7f) > .5f ? 1f : .35f;
            var fade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(SwitchSeconds * 2f / 3f, SwitchSeconds, age));
            return 1f - profile.SwitchFlicker * noise * fade;
        }

        private void LateUpdate()
        {
            if (!stage) return;
            var reach = Reach();
            var time = Time.unscaledTime;
            var baseFlicker = 1f - profile.FlickerDepth * Mathf.PerlinNoise(time * profile.FlickerRate, 0f);
            level = baseFlicker * SwitchDip(time);
            var beam = BeamColor;
            var lens = new Vector2(
                -frameOffset.x / surfaceSize.x + .5f,
                -frameOffset.y / surfaceSize.y + frameBottom);
            surface.GetPropertyBlock(block);
            block.SetTexture(MainTexId, image.Output);
            block.SetFloat(OpacityId, profile.LayerOpacity * level);
            block.SetVector(ReachApexId, new Vector4(lens.x, lens.y, 0f, 0f));
            block.SetVector(ReachUnitsId, new Vector4(surfaceSize.x, surfaceSize.y, 0f, 0f));
            block.SetVector(ReachAxisId, new Vector4(reach.Axis.x, reach.Axis.y, 0f, 0f));
            block.SetFloat(ReachAngleId, reach.HalfAngle);
            block.SetFloat(ReachLengthId, reach.Length);
            block.SetFloat(ReachWidthId, reach.Width);
            block.SetFloat(ReachSoftnessId, reach.Softness);
            block.SetFloat(SputterId, profile.Sputter);
            block.SetFloat(SputterLinesId, profile.SputterLines);
            block.SetFloat(EdgeFringeId, profile.EdgeFringe);
            block.SetColor(FringeColorId, beam);
            surface.SetPropertyBlock(block);
            if (!coneRoot) return;
            PlaceCone(reach.Axis);
            foreach (var renderer in coneRenderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetVector(ApexId, new Vector4(coneApex.x, coneApex.y, 0f, 0f));
                block.SetVector(QuadUnitsId, new Vector4(coneUnits.x, coneUnits.y, 0f, 0f));
                block.SetFloat(SourceRadiusId, profile.BeamSourceRadius);
                block.SetFloat(SpreadId, Mathf.Tan(profile.BeamHalfAngle * Mathf.Deg2Rad));
                block.SetFloat(LengthId, profile.BeamLength);
                block.SetColor(ConeColorId, beam);
                block.SetFloat(ConeLightId, profile.BeamLight);
                block.SetFloat(LevelId, level);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
