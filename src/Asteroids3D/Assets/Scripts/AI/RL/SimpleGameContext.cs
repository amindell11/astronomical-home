using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A simple implementation of IGameContext for non-training scenarios.
/// This allows RL agents to work in regular gameplay without requiring ArenaInstance.
/// </summary>
public class SimpleGameContext : MonoBehaviour, IGameContext
{
    [Header("Game Area Settings")]
    [Tooltip("Center point of the play area (if null, uses this transform's position)")]
    [SerializeField] private Transform centerTransform;
    
    [Tooltip("Size/radius of the play area for distance normalization")]
    [SerializeField] private float areaSize = 100f;
    
    [Tooltip("Whether the game context is currently active")]
    [SerializeField] private bool isActive = true;
    
    [Header("Ship Detection")]
    [Tooltip("If true, automatically finds all Ship components in the scene")]
    [SerializeField] private bool autoDetectShips = true;
    
    [Tooltip("Manual list of ships (used when autoDetectShips is false)")]
    [SerializeField] private Ship[] manualShips;
    
    // Cached ship list for performance
    private Ship[] cachedShips;
    private bool shipsCacheDirty = true;
    
    // --- IGameContext implementation ---
    
    public Vector3 CenterPosition => centerTransform != null ? centerTransform.position : transform.position;
    
    public float AreaSize => areaSize;
    
    public IReadOnlyList<Ship> ActiveShips
    {
        get
        {
            if (shipsCacheDirty)
            {
                RefreshShipsCache();
            }
            return cachedShips ?? System.Array.Empty<Ship>();
        }
    }
    
    public bool IsActive => isActive;
    
    // --- Public API ---
    
    /// <summary>
    /// Manually set the active state of this game context.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    /// <summary>
    /// Force refresh of the ships cache.
    /// Call this if ships are added/removed during gameplay.
    /// </summary>
    public void RefreshShipsCache()
    {
        if (autoDetectShips)
        {
            cachedShips = FindObjectsOfType<Ship>()
                .Where(ship => ship.gameObject.activeInHierarchy)
                .ToArray();
        }
        else
        {
            cachedShips = manualShips?.Where(ship => ship != null && ship.gameObject.activeInHierarchy).ToArray()
                         ?? System.Array.Empty<Ship>();
        }
        shipsCacheDirty = false;
    }
    
    void Start()
    {
        RefreshShipsCache();
    }
    
    void Update()
    {
        // Periodically refresh ships cache in case ships are dynamically added/removed
        if (autoDetectShips && Time.frameCount % 60 == 0) // Every ~1 second at 60 FPS
        {
            shipsCacheDirty = true;
        }
    }
    
    // Mark cache as dirty when ships might have changed
    void OnEnable()
    {
        shipsCacheDirty = true;
    }
} 