using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(ShipMovement))]
public class ShipHealth : MonoBehaviour, IDamageable
{
    // Global events for UI or other systems to hook into
    public event Action<float,float> OnHealthChanged;   // current, max

    [Header("Damage Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = 0.7f;
    [SerializeField] private bool isPlayerShip = false;

    [Header("Damage Flash")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float flashIntensity = 2f;

    private float currentHealth;
    private ShipMovement shipMovement;

    // Cached renderers & materials for flash
    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Material[][] flashMaterials;
    private bool isFlashing = false;

    private void Awake()
    {
        shipMovement = GetComponent<ShipMovement>();
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        InitializeDamageFlash();
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
    {
        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        // Visual flash
        TriggerDamageFlash();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            DestroyShip();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void DestroyShip()
    {
        // Spawn explosion effect
        if (explosionPrefab != null)
        {
            PooledVFX pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled != null)
            {
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            }
            else
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }
        }

        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }

        // Notify GameManager
        if (isPlayerShip)
        {
            GameManager.Instance?.HandlePlayerDeath(shipMovement);
        }
        else
        {
            GameManager.Instance?.HandleEnemyDeath(shipMovement);
        }

        // Disable ship GameObject (for pooling)
        gameObject.SetActive(false);
    }

    private void InitializeDamageFlash()
    {
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        originalMaterials = new Material[renderers.Length][];
        flashMaterials    = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            originalMaterials[i] = mats;
            flashMaterials[i]    = new Material[mats.Length];

            for (int j = 0; j < mats.Length; j++)
            {
                flashMaterials[i][j] = new Material(mats[j]);
                flashMaterials[i][j].EnableKeyword("_EMISSION");
                flashMaterials[i][j].SetColor("_EmissionColor", flashColor * flashIntensity);
                if (flashMaterials[i][j].HasProperty("_BaseColor")) flashMaterials[i][j].SetColor("_BaseColor", flashColor);
                if (flashMaterials[i][j].HasProperty("_Color"))     flashMaterials[i][j].SetColor("_Color", flashColor);
                if (flashMaterials[i][j].HasProperty("_EmissionMap")) flashMaterials[i][j].SetTexture("_EmissionMap", null);
            }
        }
    }

    private void TriggerDamageFlash()
    {
        if (isFlashing || renderers == null || renderers.Length == 0) return;
        StartCoroutine(DamageFlashCoroutine());
    }

    private System.Collections.IEnumerator DamageFlashCoroutine()
    {
        isFlashing = true;
        for (int i = 0; i < renderers.Length; i++) renderers[i].materials = flashMaterials[i];
        yield return new WaitForSeconds(flashDuration);
        for (int i = 0; i < renderers.Length; i++) renderers[i].materials = originalMaterials[i];
        isFlashing = false;
    }

    private void RestoreOriginalMaterials()
    {
        if (renderers == null || originalMaterials == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && originalMaterials[i] != null)
            {
                renderers[i].materials = originalMaterials[i];
            }
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFlashing = false;
        RestoreOriginalMaterials();
    }

    private void OnEnable()
    {
        if (renderers == null || renderers.Length == 0)
        {
            InitializeDamageFlash();
        }
        isFlashing = false;
        RestoreOriginalMaterials();
    }
} 