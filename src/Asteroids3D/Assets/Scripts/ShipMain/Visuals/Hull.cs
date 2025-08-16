using System.Collections;
using UnityEngine;
using Utils;

namespace ShipMain.Visuals
{
    public class Hull : MonoBehaviour
    {
        [SerializeField] private Renderer hull;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private PooledVFX sparksPrefab;
        [SerializeField] private DamageHandler source;
        
        [Header("Damage Flash")]
        [SerializeField] private Color flashColor = UnityEngine.Color.white;
        [SerializeField] private float flashTime = 0.15f;

        [Header("Death VFX")]
        [SerializeField]
        private GameObject explosionPrefab;

        private MaterialPropertyBlock block;
        private static readonly int Color = Shader.PropertyToID("_BaseColor"); // URP Lit shader
        private static readonly int DetailScale = Shader.PropertyToID("_DetailAlbedoMapScale");

        private Color baseColor;
        private Coroutine doFlash;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (!hull) return;
            hull.GetPropertyBlock(block);
            baseColor = !block.HasVector(Color) ? hull.sharedMaterial.GetColor(Color) : block.GetColor(Color);
        }

        private void OnEnable()
        {
            if (!source) source = GetComponentInParent<DamageHandler>();
            if (!source) return;
            source.OnHealthChanged += OnHealthChanged;
            source.OnDamaged      += SpawnSparks;
            source.OnDamaged      += TriggerFlash;
            source.OnDeath        += OnDeath;
        }

        private void OnDisable()
        {
            if (!source) return;
            source.OnHealthChanged -= OnHealthChanged;
            source.OnDamaged      -= SpawnSparks;
            source.OnDamaged      -= TriggerFlash;
            source.OnDeath        -= OnDeath;
        }

        private void OnDeath(Ship victim, Ship killer)
        {
            if (!GameSettings.VfxEnabled || !explosionPrefab) return;
            var pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled)
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        private void OnHealthChanged(float current, float previous, float max)
        {
            if (!GameSettings.VfxEnabled) return;

            if (hull)
            {
                hull.GetPropertyBlock(block);
                float scale = Mathf.Lerp(2f, 0f, source.HealthPct);  
                block.SetFloat(DetailScale, scale);
                hull.SetPropertyBlock(block);
            }

            if (!smoke) return;
            bool showSmoke = source.HealthPct < 0.5f;
            Debug.Log(gameObject+" " +showSmoke);
            if (smoke.gameObject.activeSelf != showSmoke)
                smoke.gameObject.SetActive(showSmoke);
        }

        private void SpawnSparks(float dmg, Vector3 hitPt)
        {
            if (!sparksPrefab || dmg <= 0f || !GameSettings.VfxEnabled) return;

            SimplePool<PooledVFX>.Get(sparksPrefab, hitPt, Quaternion.identity);
        }

        private void TriggerFlash(float dmg, Vector3 _)
        {
            if (dmg <= 0f || !hull || !GameSettings.VfxEnabled) return;
            if (doFlash != null) StopCoroutine(doFlash);
            doFlash = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (!hull) yield break;

            var pb = block;
            float half = flashTime * 0.5f;
            // Cache base color once
            if (baseColor == default)
            {
                hull.GetPropertyBlock(pb);
                baseColor = pb.GetColor(Color);
            }

            // Fade in
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                float k = t / half;
                Color c = UnityEngine.Color.Lerp(baseColor, flashColor, k);
                pb.SetColor(Color, c);
                hull.SetPropertyBlock(pb);
                yield return null;
            }
            // Fade out
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                float k = t / half;
                var c = UnityEngine.Color.Lerp(flashColor, baseColor, k);
                pb.SetColor(Color, c);
                hull.SetPropertyBlock(pb);
                yield return null;
            }
            pb.SetColor(Color, baseColor);
            hull.SetPropertyBlock(pb);
        }
    }
}
