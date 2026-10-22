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
    /// </summary>
    [RequireComponent(typeof(ConcussionWave), typeof(LineRenderer))]
    public sealed class ConcussionWaveVisual : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private PooledVFX burstPrefab;
        [SerializeField] private MeshRenderer disc;
        [SerializeField, Min(8)] private int segments = 64;

        private static readonly int WaveProgressId = Shader.PropertyToID("_WaveProgress");
        private ConcussionWave wave;
        private LineRenderer ring;
        private MaterialPropertyBlock discProperties;

        private void Awake()
        {
            if (!disc) throw new InvalidOperationException("ConcussionWaveVisual requires its seismic disc renderer.");
            discProperties = new MaterialPropertyBlock();
            wave = GetComponent<ConcussionWave>();
            ring = GetComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = segments;
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
            if (burstPrefab)
                SimplePool<PooledVFX>.Get(burstPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            transform.localScale = Vector3.one * wave.Radius;
            SetDiscProgress(Mathf.Clamp01(wave.Radius / wave.MaxRadius));
        }

        private void SetDiscProgress(float progress)
        {
            discProperties.SetFloat(WaveProgressId, progress);
            disc.SetPropertyBlock(discProperties);
        }
    }
}
