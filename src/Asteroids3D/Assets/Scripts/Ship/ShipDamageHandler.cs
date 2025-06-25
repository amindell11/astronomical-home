using UnityEngine;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(ShipMovement))]
public class ShipDamageHandler : MonoBehaviour, IDamageable
{
    // ------ Events ------
    public event Action<float,float, float> OnShieldChanged;   // current, previous, max
    public event Action<float,float, float> OnHealthChanged;   // current, previous, max
    public event Action<int>         OnLivesChanged;    // remaining lives
    public event Action<float, Vector3> OnDamaged;      // dmg, hitPoint
    public event Action<float, Vector3> OnShieldDamaged; // dmg, hitPoint when shield absorbs
    public event Action<Ship> OnDeath; // Passes the Ship component of the destroyed ship

    public float maxHealth;
    public float maxShield;
    public int   startingLives;
    public float shieldRegenDelay;
    public float shieldRegenRate;

    // ------ State ------
    private float currentHealth;
    private float currentShield;
    private int   lives;
    private float lastDamageTime;
    private Ship  lastAttacker;


    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public int   Lives => lives;
    public float HealthPct => currentHealth / maxHealth;
    public float ShieldPct => currentShield / maxShield;

    // Global death event so game systems (e.g., GameManager) can react without tight coupling.
    // -----------------------------------------------------------
    void Awake()
    {
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
            var oldShield = currentShield;
            currentShield = Mathf.Min(currentShield + regen, maxShield);
            if(currentShield != oldShield)
                OnShieldChanged?.Invoke(currentShield, oldShield, maxShield);
        }
    }

    // -----------------------------------------------------------
    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject damageSource)
    {
        if (damageSource != null)
        {
            lastAttacker = damageSource.GetComponentInParent<Ship>();
            var proj = damageSource.GetComponent<ProjectileBase>();
            if (proj && proj.Shooter != null)
                lastAttacker = proj.Shooter.GetComponentInParent<Ship>();
        }
        
        RLog.Log($"Ship taking {damage} damage from {(damageSource ? damageSource.name : "unknown source")}");
        if (damage <= 0) return;

        lastDamageTime = Time.time;

        // 1. Apply to shields first if any remain
        if (currentShield > 0f)
        {
            float absorbed = Mathf.Min(damage, currentShield);
            var oldShield = currentShield;
            currentShield -= absorbed;
            OnShieldChanged?.Invoke(currentShield, oldShield, maxShield);
            OnShieldDamaged?.Invoke(absorbed, hitPoint);
            // shipMovement?.TriggerDamageFlash();

            // A single hit never spills into health
            return;
        }

        // 2. No shields – apply to health
        var oldHealth = currentHealth;
        currentHealth = Mathf.Max(currentHealth - damage, 0f);
        OnHealthChanged?.Invoke(currentHealth, oldHealth, maxHealth);
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
            var oldHealth = currentHealth;
            var oldShield = currentShield;
            currentHealth = maxHealth;
            currentShield = maxShield;
            
            OnHealthChanged?.Invoke(currentHealth, oldHealth, maxHealth);
            OnShieldChanged?.Invoke(currentShield, oldShield, maxShield);
        }
        else
        {
            DestroyShip();
        }
    }

    void BroadcastState()
    {
        OnShieldChanged?.Invoke(currentShield, currentShield, maxShield);
        OnHealthChanged?.Invoke(currentHealth, currentHealth, maxHealth);
        OnLivesChanged?.Invoke(lives);
    }

    // -----------------------------------------------------------
    public void ResetAll()
    {
        var oldHealth = currentHealth;
        var oldShield = currentShield;
        currentHealth = maxHealth;
        currentShield = maxShield;
        lives         = startingLives;
        BroadcastState();
    }

    // -----------------------------------------------------------
    void DestroyShip()
    {
        // Global death event so game systems (e.g., GameManager) can react without tight coupling.
        Ship ship = GetComponent<Ship>();
        if (ship != null)
        {
            OnDeath?.Invoke(ship);
            Ship.BroadcastShipDestroyed(ship, lastAttacker);
        }

        gameObject.SetActive(false);
    }

    // Expose a config method for central Ship to push settings
    public void ApplySettings(ShipSettings s)
    {
        if (s == null) return;

        maxHealth       = s.maxHealth;
        maxShield       = s.maxShield;
        startingLives   = s.startingLives;
        shieldRegenDelay= s.shieldRegenDelay;
        shieldRegenRate = s.shieldRegenRate;

        ResetAll();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        /* ------------------- Configurable offsets ------------------- */
        const float baseOffset   = 2f;   // distance from ship to first element (shield bar)
        const float barSpacing   = 0.25f; // gap between shield and health bars
        const float textSpacing  = .75f;  // gap between each text line
        const float barWidth     = 3.5f;
        const float barHeight    = 0.25f;
        const float barDepth     = 0.1f; // thickness along GamePlane.Normal

        /* ------------------- Position chain ------------------------- */
        // Start just "above" the ship along the forward axis
        Vector3 shieldBarPos  = transform.position + GamePlane.Forward * baseOffset;
        Vector3 healthBarPos  = shieldBarPos   + GamePlane.Forward * barSpacing;
        Vector3 shieldTextPos = healthBarPos   + GamePlane.Forward * (barSpacing*2+textSpacing*2);
        Vector3 healthTextPos = shieldTextPos  + GamePlane.Forward * textSpacing;

        /* ------------------- Draw Bars ------------------------------ */
        Vector3 barSize = GamePlane.Right * barWidth + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;

        // Shield Bar (background + fill)
        Gizmos.color = Color.gray; // background
        Gizmos.DrawCube(shieldBarPos, barSize);
        if (maxShield > 0)
        {
            float shieldPercent = currentShield / maxShield;
            Gizmos.color = Color.cyan; // fill
            Vector3 fillPos  = shieldBarPos - GamePlane.Right * (barWidth * (1f - shieldPercent) * 0.5f);
            Vector3 fillSize = GamePlane.Right * (barWidth * shieldPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
            Gizmos.DrawCube(fillPos, fillSize);
        }

        // Health Bar (background + fill)
        Gizmos.color = Color.red; // background
        Gizmos.DrawCube(healthBarPos, barSize);
        if (maxHealth > 0)
        {
            float healthPercent = currentHealth / maxHealth;
            Gizmos.color = Color.green; // fill
            Vector3 fillPos  = healthBarPos - GamePlane.Right * (barWidth * (1f - healthPercent) * 0.5f);
            Vector3 fillSize = GamePlane.Right * (barWidth * healthPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
            Gizmos.DrawCube(fillPos, fillSize);
        }

        /* ------------------- Draw Text ------------------------------ */
        string shieldText = $"Shield: {currentShield:F1}/{maxShield:F1}";
        string healthText = $"Health: {currentHealth:F1}/{maxHealth:F1}";

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleCenter;

        Handles.Label(shieldTextPos, shieldText, style);
        Handles.Label(healthTextPos, healthText, style);
    }
#endif
} 