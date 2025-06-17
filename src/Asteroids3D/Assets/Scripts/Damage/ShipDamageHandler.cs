using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(ShipMovement))]
public class ShipDamageHandler : MonoBehaviour, IDamageable
{
    // ------ Events ------
    public event Action<float,float> OnShieldChanged;   // current, max
    public event Action<float,float> OnHealthChanged;   // current, max
    public event Action<int>         OnLivesChanged;    // remaining lives
    public event Action<float, Vector3> OnDamaged;      // dmg, hitPoint
    public event Action<float, Vector3> OnShieldDamaged; // dmg, hitPoint when shield absorbs

    // ------ Inspector ------
    [Header("Health & Shield")]
    [SerializeField] private float maxHealth  = 100f;
    [SerializeField] private float maxShield  = 50f;
    [SerializeField] private int   startingLives = 3;

    [Header("Shield Regen")]
    [Tooltip("Seconds after last damage before shield starts to regenerate")]
    [SerializeField] private float shieldRegenDelay = 3f;
    [Tooltip("Shield points regained per second")]
    [SerializeField] private float shieldRegenRate  = 10f;

    [Header("Death Visuals")]    
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip  explosionSound;
    [SerializeField] private float      explosionVolume = 0.7f;
    [SerializeField] private bool       isPlayerShip = false;

    // ------ State ------
    private float currentHealth;
    private float currentShield;
    private int   lives;
    private float lastDamageTime;

    private ShipMovement shipMovement;
    private Rigidbody     rb;

    // -----------------------------------------------------------
    void Awake()
    {
        shipMovement  = GetComponent<ShipMovement>();
        rb            = GetComponent<Rigidbody>();

        currentHealth = maxHealth;
        currentShield = maxShield;
        lives         = startingLives;

        BroadcastState();
    }

    void Update()
    {
        // Shield regeneration after delay
        if (currentShield < maxShield && Time.time - lastDamageTime >= shieldRegenDelay)
        {
            float regen = shieldRegenRate * Time.deltaTime;
            currentShield = Mathf.Min(currentShield + regen, maxShield);
            OnShieldChanged?.Invoke(currentShield, maxShield);
        }
    }

    // -----------------------------------------------------------
    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
    {
        if (damage <= 0) return;

        lastDamageTime = Time.time;

        // 1. Apply to shields first if any remain
        if (currentShield > 0f)
        {
            float absorbed = Mathf.Min(damage, currentShield);
            currentShield -= absorbed;
            OnShieldChanged?.Invoke(currentShield, maxShield);
            OnShieldDamaged?.Invoke(absorbed, hitPoint);
            // shipMovement?.TriggerDamageFlash();

            // A single hit never spills into health
            return;
        }

        // 2. No shields – apply to health
        currentHealth = Mathf.Max(currentHealth - damage, 0f);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(damage, hitPoint);

        // shipMovement?.TriggerDamageFlash();

        if (currentHealth <= 0f)
        {
            LoseLife();
        }
    }

    void LoseLife()
    {
        lives = Mathf.Max(lives - 1, 0);
        OnLivesChanged?.Invoke(lives);

        if (lives > 0)
        {
            // Restore armour
            currentHealth = maxHealth;
            currentShield = maxShield;
            BroadcastState();
        }
        else
        {
            DestroyShip();
        }
    }

    void BroadcastState()
    {
        OnShieldChanged?.Invoke(currentShield, maxShield);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnLivesChanged?.Invoke(lives);
    }

    // -----------------------------------------------------------
    public void ResetAll()
    {
        currentHealth = maxHealth;
        currentShield = maxShield;
        lives         = startingLives;
        BroadcastState();
    }

    // -----------------------------------------------------------
    void DestroyShip()
    {
        // Explosion VFX
        if (explosionPrefab)
        {
            PooledVFX pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled)
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        if (explosionSound)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);

        // Notify game manager
        if (isPlayerShip)
            GameManager.Instance?.HandlePlayerDeath(shipMovement);
        else
            GameManager.Instance?.HandleEnemyDeath(shipMovement);

        gameObject.SetActive(false);
    }
} 