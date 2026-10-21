using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    /// <summary>
    /// Stand-in tracking readout until the shooter-feedback slice of #958 lands: the missile body
    /// turns red while it flies without a target — a dumbfire launch, or a missile that lost track.
    /// </summary>
    [RequireComponent(typeof(Missile), typeof(MeshRenderer))]
    public sealed class MissileTrackingTint : MonoBehaviour, IPresentationPart
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Missile missile;
        private MeshRenderer body;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            missile = GetComponent<Missile>();
            body = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.red);
        }

        private void OnEnable()
        {
            missile.Launched += Refresh;
            missile.TrackingChanged += Refresh;
        }

        private void OnDisable()
        {
            missile.Launched -= Refresh;
            missile.TrackingChanged -= Refresh;
            body.SetPropertyBlock(null);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        // A locked launch gets its target right after Launched, so it repaints before any frame renders.
        private void Refresh() => body.SetPropertyBlock(missile.IsTracking ? null : block);
    }
}
