using Game;
using Game.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Projectile.Visual
{
    /// <summary>
    /// Renders the wave's frontier as a ring in the game plane — the ring is the readable damage
    /// boundary, so it tracks <see cref="ConcussionWave.Radius"/> exactly (unit circle scaled by
    /// the transform).
    /// </summary>
    [RequireComponent(typeof(ConcussionWave), typeof(LineRenderer))]
    public sealed class ConcussionWaveVisual : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private PooledVFX burstPrefab;
        [SerializeField, Min(8)] private int segments = 64;

        private ConcussionWave wave;
        private LineRenderer ring;

        private void Awake()
        {
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
            if (GameSettings.VfxEnabled && burstPrefab)
                SimplePool<PooledVFX>.Get(burstPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            transform.localScale = Vector3.one * wave.Radius;
        }
    }
}
