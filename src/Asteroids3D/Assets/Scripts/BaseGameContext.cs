using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ShipMain;

/// <summary>
/// Abstract base class that provides common IGameContext implementation.
/// Consolidates ship management, area size, and active filtering logic
/// to eliminate duplication across GameManager and ArenaInstance.
/// </summary>
public abstract class BaseGameContext : MonoBehaviour, IGameContext
{
    // --- Abstract properties that derived classes must implement ---
    
    /// <summary>
    /// The center point of the play area. Implementation varies by context.
    /// </summary>
    public abstract Vector3 CenterPosition { get; }
    
    /// <summary>
    /// The size/radius of the play area. Implementation varies by context.
    /// </summary>
    public abstract float AreaSize { get; }
    
    /// <summary>
    /// Whether the current game session is active. Implementation varies by context.
    /// </summary>
    public abstract bool IsActive { get; }
    
    // --- Common ship management implementation ---
    
    protected Ship[] managedShips;
    private bool shipsCacheDirty = true;
    
    /// <summary>
    /// All active ships in the current game context.
    /// Filters managed ships to only include active, non-null ships.
    /// </summary>
    public virtual IReadOnlyList<Ship> ActiveShips
    {
        get
        {
            if (managedShips == null)
                return System.Array.Empty<Ship>();
                
            return managedShips
                .Where(ship => ship != null && ship.gameObject.activeInHierarchy)
                .ToList();
        }
    }
    
    /// <summary>
    /// Refresh the managed ships cache. Derived classes should call this
    /// when ships are added, removed, or when ship discovery is needed.
    /// </summary>
    protected virtual void RefreshShipsCache()
    {
        managedShips = GetShipsForContext();
        shipsCacheDirty = false;
    }
    
    /// <summary>
    /// Default implementation finds all Ship components in the scene.
    /// ArenaInstance overrides this to use its specific ship collection.
    /// </summary>
    protected virtual Ship[] GetShipsForContext()
    {
        return FindObjectsByType<Ship>(FindObjectsSortMode.None);
    }
    
    /// <summary>
    /// Mark the ships cache as dirty, forcing a refresh on next access.
    /// Call this when the ship collection might have changed.
    /// </summary>
    protected void MarkShipsCacheDirty()
    {
        shipsCacheDirty = true;
    }
    
    /// <summary>
    /// Check if ships cache needs refreshing and do so if needed.
    /// </summary>
    protected void EnsureShipsCacheValid()
    {
        if (shipsCacheDirty)
        {
            RefreshShipsCache();
        }
    }
    
    protected virtual void Start()
    {
        RefreshShipsCache();
    }
    
    protected virtual void OnEnable()
    {
        MarkShipsCacheDirty();
    }
} 