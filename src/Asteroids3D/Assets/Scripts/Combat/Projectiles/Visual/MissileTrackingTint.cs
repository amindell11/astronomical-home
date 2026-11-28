using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    /// <summary>
    /// Stand-in tracking readout until the shooter-feedback slice of #958 lands: the missile body
    /// turns red while it flies without a target — a dumbfire launch, or a missile that lost track —
    /// and blue while it flies on a wire.
    /// </summary>
    [RequireComponent(typeof(Missile))]
    public sealed class MissileTrackingTint : MonoBehaviour, IPresentationPart
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Missile missile;
        private MeshRenderer body;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock wiredBlock;

        private void Awake()
        {
            missile = GetComponent<Missile>();
            body = GetComponentInChildren<MeshRenderer>();
            block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, Color.red);
            wiredBlock = new MaterialPropertyBlock();
            wiredBlock.SetColor(BaseColorId, Color.blue);
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

        // A locked or wired launch gets its guidance right after Launched, so it repaints before any frame renders.
        private void Refresh() =>
            body.SetPropertyBlock(missile.IsWired ? wiredBlock : missile.IsTracking ? null : block);
    }
}
