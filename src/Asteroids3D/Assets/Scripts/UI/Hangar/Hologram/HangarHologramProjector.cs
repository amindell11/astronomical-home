using System;
using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// Owns the hangar preview stage and shows its image on the quad at the projector lens, at the
    /// profile's opacity, and places the cone. The console hands it the hardware camera on
    /// <see cref="Fit"/>. Ship steps from the hangar screen reach it through <see cref="Select"/>.
    /// </summary>
    public sealed class HangarHologramProjector : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly Vector3[] QuadCorners =
        {
            new(-.5f, -.5f, 0f), new(-.5f, .5f, 0f), new(.5f, -.5f, 0f), new(.5f, .5f, 0f)
        };

        [Tooltip("Hologram restyle, layer opacity and cone.")]
        [SerializeField] private HangarHologramProfile profile;

        [Tooltip("Quad at the lens, parented here. A 1×1 centred mesh; the stage image is drawn on it.")]
        [SerializeField] private MeshRenderer surface;

        [Tooltip("Parent for the cone, at the lens. The cone prefab's base sits at this origin.")]
        [SerializeField] private Transform coneAnchor;

        [Tooltip("Width and height of the quad, in world units.")]
        [SerializeField] private Vector2 surfaceSize = new(9f, 6f);

        [Tooltip("Side of the settled ship's square, in world units. It sits centred on the surface, its bottom at the lens.")]
        [SerializeField, Min(.01f)] private float frameSide = 4f;

        [Tooltip("Height of the square's bottom edge above the surface's bottom, as a share of the surface height.")]
        [SerializeField, Range(0f, 1f)] private float frameBottom = .1f;

        private MaterialPropertyBlock block;
        private HangarPreviewStage stage;
        // Normalized on the surface; square on screen, as the stage's off-axis framing requires.
        private Rect frame;

        public Rect Frame => frame;
        internal HangarPreviewStage Stage => stage;

        private void Awake()
        {
            if (!profile || !surface || !coneAnchor)
                throw new InvalidOperationException(
                    "HangarHologramProjector needs a profile, surface and cone anchor.");
            block = new MaterialPropertyBlock();
            var width = frameSide / surfaceSize.x;
            frame = new Rect(.5f - width * .5f, frameBottom, width, frameSide / surfaceSize.y);
            surface.transform.localScale = new Vector3(surfaceSize.x, surfaceSize.y, 1f);
            surface.transform.localPosition = new Vector3(0f, (.5f - frameBottom) * surfaceSize.y, 0f);
        }

        public void Open(IEnumerable<Ship> offered, Ship selected)
        {
            stage = HangarPreviewStage.Create(transform, new Vector2Int(64, 64), frame, offered, profile.Restyle);
            var cone = Instantiate(profile.ProjectorCone, coneAnchor);
            cone.transform.localPosition = Vector3.zero;
            cone.transform.localScale *= frameSide / HangarPreviewStage.FrameSideInRadii;
            stage.Show(selected, backwards: false);
        }

        public void Select(Ship ship, int step) => stage.Show(ship, backwards: step < 0);

        public void Fit(Camera camera)
        {
            if (!stage) return;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in QuadCorners)
            {
                var screen = camera.WorldToScreenPoint(surface.transform.TransformPoint(corner));
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            var pixels = Vector2Int.CeilToInt(max - min);
            if (pixels.x < 1) pixels.x = 1;
            if (pixels.y < 1) pixels.y = 1;
            stage.Resize(pixels, frame);
        }

        private void LateUpdate()
        {
            if (!stage) return;
            surface.GetPropertyBlock(block);
            block.SetTexture(MainTexId, stage.Texture);
            block.SetFloat(OpacityId, profile.LayerOpacity);
            surface.SetPropertyBlock(block);
        }
    }
}
