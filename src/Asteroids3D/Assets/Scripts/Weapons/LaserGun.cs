using UnityEngine;

/// <summary>
/// Concrete weapon that fires pooled <see cref="LaserProjectile"/> instances.
/// All common launcher logic lives in <see cref="LauncherBase{TProj}"/>.
/// This weapon uses a heat system for ammo.
/// </summary>
public class LaserGun : LauncherBase<LaserProjectile>
{
    [Header("Heat System")]
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float heatPerShot = 25f;
    [SerializeField] private float coolingRate = 50f; // units per second
    [SerializeField] private float coolDownDelay = 0.5f; // seconds before cooling starts after a normal shot
    [SerializeField] private float overheatPenaltyTime = 1.5f; // seconds before cooling starts after overheating

    private float currentHeat = 0f;
    private float lastShotTime = -100f; // Initialize to allow immediate firing

    public float CurrentHeat => currentHeat;
    public float MaxHeat => maxHeat;
    public float HeatPct => currentHeat / maxHeat;

    void Start()
    {
        ResetHeat();
    }
    
    void Update()
    {
        if (currentHeat <= 0) return;

        bool wasOverheated = currentHeat >= maxHeat;
        float delay = wasOverheated ? overheatPenaltyTime : coolDownDelay;
        
        if (Time.time > lastShotTime + delay)
        {
            currentHeat -= coolingRate * Time.deltaTime;
            currentHeat = Mathf.Max(0, currentHeat);
        }
    }

    public override bool CanFire()
    {
        // Check base for cooldown, then check for heat.
        return base.CanFire() && currentHeat < maxHeat;
    }

    public override ProjectileBase Fire()
    {
        // base.Fire() now calls our overridden CanFire(), so all checks are handled.
        ProjectileBase proj = base.Fire();
        if (proj != null)
        {
            currentHeat += heatPerShot;
            lastShotTime = Time.time;
            currentHeat = Mathf.Min(currentHeat, maxHeat); // Clamp heat to max
        }
        return proj;
    }

    public void ResetHeat()
    {
        currentHeat = 0f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw heat bar
        if (Application.isPlaying && transform.parent)
        {
            Vector3 position = transform.parent.position + transform.parent.right * 1.5f;
            float heatRatio = currentHeat / maxHeat;
            
            UnityEditor.Handles.Label(position + Vector3.up * 1.2f, $"Heat: {currentHeat:F0}/{maxHeat:F0}");

            Vector3 barStart = position;
            Vector3 barEnd = position + Vector3.up * 1.0f;
            
            // Background
            UnityEditor.Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            UnityEditor.Handles.DrawAAPolyLine(3f, barStart, barEnd);

            // Foreground
            if (heatRatio > 0)
            {
                UnityEditor.Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
                UnityEditor.Handles.DrawAAPolyLine(3f, barStart, Vector3.Lerp(barStart, barEnd, heatRatio));
            }
        }
    }
#endif
} 