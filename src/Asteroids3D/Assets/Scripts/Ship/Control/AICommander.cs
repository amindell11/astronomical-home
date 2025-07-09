using UnityEngine;
using System.Collections.Generic;
using ShipControl;

// Commander modules are now standalone; ShipMovement lives on the parent Ship object.
[RequireComponent(typeof(AINavigator))]
[RequireComponent(typeof(AIGunner))]
public class AICommander : MonoBehaviour, IShipCommandSource
{
    /* ── Difficulty Setting ─────────────────────────────────── */
    [Header("Difficulty")]
    [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
    [Range(0f, 1f)] public float difficulty = 1.0f;


    /* ── internals ───────────────────────────────────────────── */
    private Ship ship;
    private AINavigator navigator;
    private AIGunner gunner;
    private ShipState currentState;

    
    public struct Waypoint
    {
        public Vector2 position;
        public Vector2 velocity;
        public bool isValid;
    }
    private Waypoint navWaypoint;


    private ShipCommand cachedCommand;

    public ShipState CurrentState => currentState;

    public ShipCommand CachedCommand => cachedCommand;
    public Waypoint CurrentWaypoint => navWaypoint;
    public AINavigator Navigator => navigator;
    public AIGunner Gunner => gunner;

    void Awake()
    {
        navWaypoint = new Waypoint { isValid = false };
        navigator = GetComponent<AINavigator>();
        gunner = GetComponent<AIGunner>();      
    }

    public void InitializeCommander(Ship ship)
    {
        this.ship = ship;
        navigator.Initialize(ship);
        gunner.Initialize(ship);
    }

    public int Priority => 10;

    public bool TryGetCommand(ShipState state, out ShipCommand cmd)
    {   
        // Simply return the command generated in the most recent FixedUpdate().
        cmd = cachedCommand;
        return true;
    }

    void FixedUpdate()
    {
        if (ship == null) return;   
        currentState = ship.CurrentState;
        cachedCommand = GenerateCommand(currentState);
    }

    ShipCommand GenerateCommand(ShipState state)
    {
        ShipCommand cmd = new ShipCommand();

        // --- Difficulty Level 1 (< 0.25): Stationary, no actions. ---
        if (difficulty < 0.25f) return cmd; // cmd defaults to zeros/false.
    

        navigator.GenerateNavCommands(state, navWaypoint, ref cmd);

        // --- Difficulty governs weapon usage ---
        // Level 2 (< 0.5): Movement only, no weapons.
        if (difficulty < 0.5f) return cmd;

        gunner.GenerateGunnerCommands(state, navWaypoint, ref cmd);

        // Level 3 (< 0.75): Lasers only, no missiles.
        if (difficulty < 0.75f)
        {
            if (cmd.SecondaryFire) // Only log if we are actually disabling it
            {
                cmd.SecondaryFire = false;
            }
        }
        return cmd;
    }

    /// <summary>Sets an arbitrary world-space point as the navigation goal.</summary>
    public void SetNavigationPoint(Vector3 point, bool avoid=false, Vector3? velocity=null)
    {
        navWaypoint.position = GamePlane.WorldToPlane(point);
        navWaypoint.velocity = velocity.HasValue ? (Vector2)velocity.Value : Vector2.zero;
        navWaypoint.isValid  = true;

        navigator.enableAvoidance = avoid;
    }

    /// <summary>Returns true if an unobstructed line of sight exists to <paramref name="tgt"/>.</summary>
    public bool HasLineOfSight(Transform tgt)
    {
        return gunner.HasLineOfSight(tgt);
    }

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (navWaypoint.isValid)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, GamePlane.PlaneToWorld(navWaypoint.position));
        }
    }
    #endif
}