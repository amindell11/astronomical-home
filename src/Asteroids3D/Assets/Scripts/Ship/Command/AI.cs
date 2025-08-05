using UnityEngine;
using System.Collections.Generic;
using ShipControl;
using ShipControl.AI;

// Commander modules are now standalone; ShipMovement lives on the parent Ship object.
[RequireComponent(typeof(AINavigator))]
[RequireComponent(typeof(AIGunner))]
[RequireComponent(typeof(AIStateMachine))]
public class AI : MonoBehaviour, ICommandSource
{
    /* ── Difficulty Setting ─────────────────────────────────── */
    [Header("Difficulty")]
    [Tooltip("Bot skill level, typically set by curriculum (0.0 to 1.0)")]
    [Range(0f, 1f)] public float difficulty = 1.0f;

    /* ── internals ───────────────────────────────────────────── */
    private Ship ship;
    private AINavigator navigator;
    private AIGunner gunner;
    private AIContext context;
    private AIStateMachine stateMachine;
    private State currentState;

    private Command cachedCommand;

    public State CurrentState => currentState;

    public Command CachedCommand => cachedCommand;
    public AINavigator Navigator => navigator;
    public AIGunner Gunner => gunner;
    public AIStateMachine StateMachine => stateMachine;
    public string CurrentStateName => stateMachine?.CurrentStateName ?? "None";

    public void InitializeCommander(Ship ship)
    {
        navigator = GetComponent<AINavigator>();
        gunner = GetComponent<AIGunner>();
        context = GetComponent<AIContext>();
        stateMachine = GetComponent<AIStateMachine>();

        this.ship = ship;
        navigator.Initialize(ship);
        gunner.Initialize(ship);
        context.Initialize(ship, this, navigator, gunner);
        
        // Initialize the state machine with all states
        stateMachine.Initialize(
            new IdleState(navigator, gunner),
            new PatrolState(navigator, gunner),
            new EvadeState(navigator, gunner),
            new JinkEvadeState(navigator, gunner),
            new AttackState(navigator, gunner),
            new OrbitState(navigator, gunner),
            new KiteState(navigator, gunner)
        );
    }

    public int Priority => 10;

    public bool TryGetCommand(State state, out Command cmd)
    {   
        // Simply return the command generated in the most recent FixedUpdate().
        cmd = cachedCommand;
        return true;
    }

    void FixedUpdate()
    {
        if (ship == null || stateMachine == null) return;   
        currentState = ship.CurrentState;
        
        // Update state machine with context
        if (context != null)
        {
            stateMachine.Tick(context, Time.fixedDeltaTime);
        }
        
        cachedCommand = GenerateCommand(currentState);
    }

    Command GenerateCommand(State state)
    {
        Command cmd = new Command();

        // --- Difficulty Level 1 (< 0.25): Stationary, no actions. ---
        if (difficulty < 0.25f) return cmd; // cmd defaults to zeros/false.
    

        navigator.GenerateNavCommands(state, ref cmd);

        // --- Difficulty governs weapon usage ---
        // Level 2 (< 0.5): Movement only, no weapons.
        if (difficulty < 0.5f) return cmd;

        gunner.GenerateGunnerCommands(state, ref cmd);

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

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var waypoint = navigator?.CurrentWaypoint ?? new AINavigator.Waypoint { isValid = false };
        if (waypoint.isValid)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, GamePlane.PlaneToWorld(waypoint.position));
        }
    }
    #endif
}