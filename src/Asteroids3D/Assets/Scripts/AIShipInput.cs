using UnityEngine;
using UnityEngine.AI;

// Uses a NavMeshAgent to pilot a Ship component.
[RequireComponent(typeof(Ship))]
[RequireComponent(typeof(NavMeshAgent))]
public class AIShipInput : MonoBehaviour
{
    [Header("Navigation Settings")]
    public Transform target;
    [Tooltip("The distance to a waypoint to consider it 'reached'.")]
    public float waypointDistance = 2.0f;

    [Header("Combat Settings")]
    [SerializeField] private float fireAngleTolerance = 5.0f;
    [SerializeField] private float fireDistance = 20.0f;
    [SerializeField] private LayerMask lineOfSightMask;

    private Ship ship;
    private NavMeshAgent agent;
    private LaserGun laserGun;
    private int currentWaypointIndex;
    private Camera mainCamera;

    private void Start()
    {
        ship = GetComponent<Ship>();
        agent = GetComponent<NavMeshAgent>();
        laserGun =  GetComponentInChildren<LaserGun>();
        mainCamera = Camera.main;
        
        // Disable NavMeshAgent's automatic movement and rotation.
        // We are using it for pathfinding only.
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        // Make sure the agent's settings match the ship's capabilities
        agent.speed = ship.maxSpeed;
        agent.angularSpeed = ship.maxRotationSpeed;
        
        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            ship.Controller.SetControls(0, 0);
            ship.Controller.SetRotationTarget(false, 0f);
            return;
        }

        // Sync NavMeshAgent's position with the ship's actual position.
        // This is crucial because we disabled automatic updates.
        agent.nextPosition = transform.position;

        // Update destination if target moved significantly.
        if (Vector3.Distance(agent.destination, target.position) > 1.0f)
        {
            agent.SetDestination(target.position);
            currentWaypointIndex = 0; // Reset waypoint tracking.
        }

        if (!agent.hasPath || agent.path.corners.Length == 0)
        {
            ship.Controller.SetControls(0, 0);
            ship.Controller.SetRotationTarget(false, 0f);
            return;
        }

        NavigatePath();
        HandleShooting();
    }

    private void NavigatePath()
    {
        // Advance to the next waypoint if we are close enough to the current one.
        if (currentWaypointIndex < agent.path.corners.Length)
        {
            float distanceToWaypoint = Vector3.Distance(transform.position, agent.path.corners[currentWaypointIndex]);
            if (distanceToWaypoint < waypointDistance)
            {
                currentWaypointIndex++;
            }
        }
        
        // If we've passed the last waypoint, stop.
        if (currentWaypointIndex >= agent.path.corners.Length)
        {
            ship.Controller.SetControls(0, 0);
            ship.Controller.SetRotationTarget(false, ship.Controller.Angle);
            return;
        }
        
        // Get the current target waypoint in 3D and convert to 2D plane space.
        Vector3 nextWaypoint3D = agent.path.corners[currentWaypointIndex];
        Vector2 targetWaypoint2D = ship.WorldToPlane(nextWaypoint3D - ship.GetPlaneOrigin());

        // Get current ship state from the 2D controller.
        Vector2 currentPosition = ship.Controller.Position;
        Vector2 currentVelocity = ship.Controller.Velocity;
        float angleRad = ship.Controller.Angle * Mathf.Deg2Rad;
        Vector2 currentForward = new Vector2(-Mathf.Sin(angleRad), Mathf.Cos(angleRad));
        
        // Call the VelocityPilot to get the desired inputs.
        VelocityPilot.ComputeInputs(
            targetWaypoint2D, 
            currentPosition, 
            currentVelocity, 
            currentForward,
            ship.maxSpeed,
            out float thrustCmd, 
            out float strafeCmd, 
            out float rotTargetDeg
        );
        
        // Feed the computed inputs into the ship controller.
        ship.Controller.SetControls(thrustCmd, strafeCmd);
        ship.Controller.SetRotationTarget(true, rotTargetDeg);
    }
    
    private void HandleShooting()
    {
        if (laserGun == null || target == null || mainCamera == null)
        {
            Debug.Log("HandleShooting: Aborted. LaserGun, Target, or Camera is null.");
            return;
        }

        // Check if the AI ship is visible on screen
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(transform.position);
        bool isVisible = viewportPoint.z > 0 && // In front of camera
                        viewportPoint.x >= 0 && viewportPoint.x <= 1 && // Within horizontal bounds
                        viewportPoint.y >= 0 && viewportPoint.y <= 1;   // Within vertical bounds

        if (!isVisible)
        {
            Debug.Log("HandleShooting: Aborted. AI ship is not visible on screen.");
            return;
        }
        
        Vector3 firePointPosition = laserGun.firePoint != null ? laserGun.firePoint.position : transform.position;
        Vector3 directionToTarget = target.position - firePointPosition;
        float distanceToTarget = directionToTarget.magnitude;

        // Check 1: Is the target within firing distance?
        if (distanceToTarget > fireDistance)
        {
            Debug.Log($"HandleShooting: Aborted. Target is too far. Distance: {distanceToTarget}, Max: {fireDistance}");
            return;
        }

        // Check 2: Is the ship pointing towards the target?
        float angleToTarget = Vector3.Angle(transform.up, directionToTarget);
        if (angleToTarget > fireAngleTolerance)
        {
            Debug.Log($"HandleShooting: Aborted. Not aiming at target. Angle: {angleToTarget}, Tolerance: {fireAngleTolerance}");
            return;
        }

        // Check 3: Is there a clear line of sight?
        if (Physics.Raycast(firePointPosition, directionToTarget.normalized, out RaycastHit hit, distanceToTarget, lineOfSightMask))
        {
            if (hit.transform.root != target.root)
            {
                // Obstacle detected
                Debug.Log($"HandleShooting: Aborted. Line of sight blocked by {hit.transform.name}.");
                return;
            }
        }
        
        // All checks passed, fire the weapon.
        Debug.Log("HandleShooting: All checks passed. FIRING!");
        laserGun.Fire();
    }

    private void OnDrawGizmos()
    {
        if (agent != null && agent.hasPath)
        {
            // Draw the full path from the NavMeshAgent.
            Gizmos.color = Color.cyan;
            for (int i = 0; i < agent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(agent.path.corners[i], agent.path.corners[i + 1]);
                Gizmos.DrawWireSphere(agent.path.corners[i], 0.2f);
            }
            Gizmos.DrawWireSphere(agent.path.corners[agent.path.corners.Length - 1], 0.2f);


            // Highlight the current target waypoint.
            if (currentWaypointIndex < agent.path.corners.Length)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(agent.path.corners[currentWaypointIndex], 0.5f);
                
                // Draw a line from the ship to the current waypoint.
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, agent.path.corners[currentWaypointIndex]);
            }
        }
    }
}