using UnityEngine;

namespace Substrate.Services.Environment.Flat
{
    public sealed class FlatBackgroundAsset : ScriptableObject
    {
        [SerializeField] private Texture2D texture;
        [SerializeField] private bool isFinal;
        [SerializeField] private Color baseColor;
        [SerializeField] private Color primaryColor;
        [SerializeField] private Color secondaryColor;
        [SerializeField] private Color accentColor;
        [SerializeField, HideInInspector] private string manifestJson;

        public Texture2D Texture => texture;
        public bool IsFinal => isFinal;
        public Color BaseColor => baseColor;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Color AccentColor => accentColor;
        public string ManifestJson => manifestJson;

        public void Initialize(Texture2D image, bool final, Color background, Color primary,
            Color secondary, Color accent, string manifest)
        {
            texture = image;
            isFinal = final;
            baseColor = background;
            primaryColor = primary;
            secondaryColor = secondary;
            accentColor = accent;
            manifestJson = manifest;
        }
    }
}
