using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    /// <summary>
    /// A station model's paint roles. Wearing a palette pushes it through property blocks so every
    /// station of a kind shares its materials. Worn before the model first wakes, so its motion caches
    /// the window light.
    /// </summary>
    public sealed class StationModel : MonoBehaviour
    {
        private static readonly int PaperColorId = Shader.PropertyToID("_PaperColor");
        private static readonly int PaintLightColorId = Shader.PropertyToID("_PaintLightColor");
        private static readonly int PaintShadowStrengthId = Shader.PropertyToID("_PaintShadowStrength");
        private static readonly int PaintLightStrengthId = Shader.PropertyToID("_PaintLightStrength");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("The renderers the bone colour lands on.")]
        [SerializeField] private Renderer[] bone = { };

        [Tooltip("The renderers the canvas colour lands on.")]
        [SerializeField] private Renderer[] canvas = { };

        [Tooltip("The renderers the slate colour lands on.")]
        [SerializeField] private Renderer[] slate = { };

        [Tooltip("The renderers the ochre colour lands on.")]
        [SerializeField] private Renderer[] ochre = { };

        [Tooltip("The renderers the brick colour lands on.")]
        [SerializeField] private Renderer[] brick = { };

        [Tooltip("The renderers the blue colour lands on.")]
        [SerializeField] private Renderer[] blue = { };

        [Tooltip("Crew windows, glowing at the worn palette's window light.")]
        [SerializeField] private Renderer[] windows = { };

        public void Wear(StationPalette palette)
        {
            var block = new MaterialPropertyBlock();
            Paint(block, bone, palette.bone, palette.brush);
            Paint(block, canvas, palette.canvas, palette.brush);
            Paint(block, slate, palette.slate, palette.brush);
            Paint(block, ochre, palette.ochre, palette.brush);
            Paint(block, brick, palette.brick, palette.brush);
            Paint(block, blue, palette.blue, palette.brush);
            var emission = (Vector4)palette.windowLight.linear;
            foreach (var window in windows)
            {
                window.GetPropertyBlock(block);
                block.SetVector(EmissionColorId, emission);
                window.SetPropertyBlock(block);
            }
        }

        private static void Paint(MaterialPropertyBlock block, Renderer[] renderers, Color colour, float brush)
        {
            var lin = colour.linear;
            var light = new Color(Mathf.Min(1f, 2f * lin.r), Mathf.Min(1f, 2f * lin.g),
                Mathf.Min(1f, 2f * lin.b), 1f).gamma;
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(PaperColorId, colour);
                block.SetColor(PaintLightColorId, light);
                block.SetFloat(PaintShadowStrengthId, brush);
                block.SetFloat(PaintLightStrengthId, brush);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
