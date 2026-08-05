using Damage;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using Utils;

namespace Ships.Visuals
{
    public class HullVisuals : MonoBehaviour, IShipVisual
    {
        [SerializeField] private Renderer hull;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private PooledVFX sparksPrefab;

        [Header("Damage Flash")]
        [SerializeField] private Color flashColor = UnityEngine.Color.white;
        [SerializeField] private float flashTime = 0.15f;

        [Header("Death VFX")]
        [SerializeField]
        private GameObject explosionPrefab;

        private IDamageEvents source;
        private bool subscribed;

        private MaterialPropertyBlock block;
        private static readonly int Color = Shader.PropertyToID("_BaseColor"); // URP Lit shader
        private static readonly int DetailScale = Shader.PropertyToID("_DetailAlbedoMapScale");

        private Color baseColor;
        private bool flashActive;
        private float flashElapsed;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (!hull) return;
            hull.GetPropertyBlock(block);
            baseColor = !block.HasVector(Color) ? hull.sharedMaterial.GetColor(Color) : block.GetColor(Color);
        }

        public void Bind(in ShipView view)
        {
            source = view.Damage;
            if (isActiveAndEnabled)
            {
                Subscribe();
                ApplyVisualStateFromHealth();
            }
        }

        private void OnEnable()
        {
            if (source == null) return;
            Subscribe();
            ApplyVisualStateFromHealth();
        }

        private void OnDisable()
        {
            Unsubscribe();
            flashActive = false;
        }

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.Health.OnValueChanged += OnHealthChanged;
            source.OnDamaged += SpawnSparks;
            source.OnDamaged += TriggerFlash;
            source.OnDeath += OnDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || source == null) return;
            source.Health.OnValueChanged -= OnHealthChanged;
            source.OnDamaged -= SpawnSparks;
            source.OnDamaged -= TriggerFlash;
            source.OnDeath -= OnDeath;
            subscribed = false;
        }

        private void OnDeath(ShipId _victimId, DamageInfo _killingBlow)
        {
            if (!explosionPrefab) return;
            var pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled)
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        private void OnHealthChanged(float current, float previous, float max)
        {
            ApplyVisualStateFromHealth();
        }

        private void ApplyVisualStateFromHealth()
        {
            if (source == null) return;

            var healthPct = source.Health.Pct;

            if (hull)
            {
                hull.GetPropertyBlock(block);
                var scale = Mathf.Lerp(2f, 0f, healthPct);
                block.SetFloat(DetailScale, scale);
                hull.SetPropertyBlock(block);
            }

            if (!smoke) return;
            var showSmoke = healthPct < 0.5f;
            if (smoke.gameObject.activeSelf != showSmoke)
                smoke.gameObject.SetActive(showSmoke);
        }

        private void SpawnSparks(DamageInfo hit)
        {
            if (!sparksPrefab || hit.Amount <= 0f) return;

            SimplePool<PooledVFX>.Get(sparksPrefab, hit.HitPoint, Quaternion.identity);
        }

        private void TriggerFlash(DamageInfo hit)
        {
            if (hit.Amount <= 0f || !hull) return;
            flashActive = true;
            flashElapsed = 0f;
            ApplyFlashColor(0f);
        }

        private void Update()
        {
            if (!flashActive || !hull) return;

            flashElapsed += Time.unscaledDeltaTime;
            if (flashElapsed >= flashTime)
            {
                flashActive = false;
                ApplyFlashColor(1f);
                return;
            }

            ApplyFlashColor(flashElapsed / flashTime);
        }

        private void ApplyFlashColor(float normalizedTime)
        {
            var pb = block;
            if (baseColor == default)
            {
                hull.GetPropertyBlock(pb);
                baseColor = pb.GetColor(Color);
            }

            var blend = normalizedTime <= 0.5f
                ? normalizedTime * 2f
                : 2f - normalizedTime * 2f;
            pb.SetColor(Color, UnityEngine.Color.Lerp(baseColor, flashColor, blend));
            hull.SetPropertyBlock(pb);
        }
    }
}
