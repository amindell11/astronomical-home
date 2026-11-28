using System;
using Substrate.Presentation;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles.Visual
{
    /// <summary>
    /// The boundary ring and animated disc follow the damage radius in the game plane.
    /// Disc playback uses radius progress so pooled detonations restart without a separate animation clock.
    /// A wave that pulls (Implosion) plays both in reverse, shrinking to its centre, with the ring tinted.
    /// </summary>
    [RequireComponent(typeof(ConcussionWave), typeof(LineRenderer))]
    public sealed class ConcussionWaveVisual : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private PooledVFX burstPrefab;
        [SerializeField] private MeshRenderer disc;
        [SerializeField, Min(8)] private int segments = 64;
        [Tooltip("Ring colour of a wave that pulls toward its centre.")]
        [SerializeField] private Color pullTint = new(0.8f, 0.35f, 1f, 1f);

        private static readonly int WaveProgressId = Shader.PropertyToID("_WaveProgress");
        private ConcussionWave wave;
        private LineRenderer ring;
        private MaterialPropertyBlock discProperties;
        private Gradient pushColors;
        private Gradient pullColors;

        private void Awake()
        {
            if (!disc) throw new InvalidOperationException("ConcussionWaveVisual requires its seismic disc renderer.");
            discProperties = new MaterialPropertyBlock();
            wave = GetComponent<ConcussionWave>();
            ring = GetComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = segments;
            pushColors = ring.colorGradient;
            pullColors = new Gradient();
            pullColors.SetKeys(new[] { new GradientColorKey(pullTint, 0f), new GradientColorKey(pullTint, 1f) },
                pushColors.alphaKeys);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, GamePlane.PlaneDirToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))));
            }
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.zero;
            SetDiscProgress(0f);
            wave.Begun += SpawnBurst;
        }

        private void OnDisable()
        {
            wave.Begun -= SpawnBurst;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        /// <summary>On Begin, not OnEnable — pool warmup activates the instance once without a detonation.</summary>
        private void SpawnBurst()
        {
            ring.colorGradient = wave.Pulls ? pullColors : pushColors;
            if (burstPrefab)
                SimplePool<PooledVFX>.Get(burstPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            var progress = Mathf.Clamp01(wave.Radius / wave.MaxRadius);
            transform.localScale = Vector3.one * (wave.Pulls ? Mathf.Max(0f, wave.MaxRadius - wave.Radius) : wave.Radius);
            SetDiscProgress(wave.Pulls ? 1f - progress : progress);
        }

        private void SetDiscProgress(float progress)
        {
            discProperties.SetFloat(WaveProgressId, progress);
            disc.SetPropertyBlock(discProperties);
        }
    }
}
