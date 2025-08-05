using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Hull : MonoBehaviour
{
    [SerializeField] Renderer hull;           // ship mesh or sprite renderer
    [SerializeField] ParticleSystem smoke;    // looping smoke prefab
    [SerializeField] PooledVFX sparksPrefab; // burst prefab
    [SerializeField] DamageHandler source;   // link to ship damage handler
    
    [Header("Damage Flash")]
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.15f; // total duration (fade in + out)

    // ----- Explosion FX Config (now serialized) -----
    [Header("Death VFX")]
    [SerializeField] GameObject explosionPrefab;

    MaterialPropertyBlock block;
    static readonly int _Color = Shader.PropertyToID("_BaseColor"); // URP Lit shader
    static readonly int _DetailScale = Shader.PropertyToID("_DetailAlbedoMapScale");

    float prevHealth = -1f;
    Color baseColor;
    Coroutine flashCo;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (hull)
        {
            // Try to read an override value that might already exist on the renderer.
            hull.GetPropertyBlock(block);
            if (!block.HasVector(_Color))
            {
                // If no per-renderer override is present, fall back to the material's base colour.
                baseColor = hull.sharedMaterial.GetColor(_Color);
            }
            else
            {
                baseColor = block.GetColor(_Color);
            }
        }
    }

    void OnEnable()
    {
        if (source == null) source = GetComponentInParent<DamageHandler>();
        if (source != null) {
            source.OnHealthChanged += OnHealthChanged;
            source.OnDamaged      += SpawnSparks;
            source.OnDamaged      += TriggerFlash;
            source.OnDeath        += OnDeath;
        }
    }
    void OnDisable()
    {
        if (source != null) {
            source.OnHealthChanged -= OnHealthChanged;
            source.OnDamaged      -= SpawnSparks;
            source.OnDamaged      -= TriggerFlash;
            source.OnDeath        -= OnDeath;
        }
    }

    void OnDeath(Ship victim, Ship killer)
    {
        // Explosion VFX
        if (GameSettings.VfxEnabled && explosionPrefab)
        {
            PooledVFX pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled)
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
    }

    void OnHealthChanged(float current, float previous, float max)
    {
        if (prevHealth < 0f) prevHealth = current; // initialise first time

        if (!GameSettings.VfxEnabled) return;

        // --- visual tint & smoke based on % health lost ---
        float pctLost = 1f - current / max;          // 0->1
        if (hull)
        {
            hull.GetPropertyBlock(block);
            float scale = Mathf.Lerp(0f, 2f, pctLost);  // grime intensity
            block.SetFloat(_DetailScale, scale);
            hull.SetPropertyBlock(block);
        }
        if (smoke)
        {
            bool showSmoke = pctLost > 0.5f;
            if (smoke.gameObject.activeSelf != showSmoke)
                smoke.gameObject.SetActive(showSmoke);
        }

        prevHealth = current;
    }

    void SpawnSparks(float dmg, Vector3 hitPt)
    {
        if (!sparksPrefab || dmg <= 0f || !GameSettings.VfxEnabled) return;

        // Use pooled VFX to avoid instantiation overhead and hierarchy clutter
        SimplePool<PooledVFX>.Get(sparksPrefab, hitPt, Quaternion.identity);
    }

    void TriggerFlash(float dmg, Vector3 _)
    {
        if (dmg <= 0f || hull == null || !GameSettings.VfxEnabled) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (!hull) yield break;

        MaterialPropertyBlock pb = block;
        float half = flashTime * 0.5f;
        // Cache base color once
        if (baseColor == default)
        {
            hull.GetPropertyBlock(pb);
            baseColor = pb.GetColor(_Color);
        }

        // Fade in
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            float k = t / half;
            Color c = Color.Lerp(baseColor, flashColor, k);
            pb.SetColor(_Color, c);
            hull.SetPropertyBlock(pb);
            yield return null;
        }
        // Fade out
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            float k = t / half;
            Color c = Color.Lerp(flashColor, baseColor, k);
            pb.SetColor(_Color, c);
            hull.SetPropertyBlock(pb);
            yield return null;
        }
        pb.SetColor(_Color, baseColor);
        hull.SetPropertyBlock(pb);
    }
}
