using UnityEngine;
using UnityEngine.AI;

// Uses a NavMeshAgent to pilot a Ship component.
[RequireComponent(typeof(Ship2D))]
[RequireComponent(typeof(NavMeshAgent))]
public class AIShipInput : MonoBehaviour
{
    [Header("Navigation Settings")]
    public Transform target;
    public float waypointDistance = 1.0f;

    private Ship2D ship;
    private NavMeshAgent agent;
    private int currentWaypointIndex;

    private void Start()
    {
        ship = GetComponent<Ship2D>();
        agent = GetComponent<NavMeshAgent>();
        
        // Disable NavMeshAgent's automatic movement and rotation
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        // Initialize waypoint tracking
        currentWaypointIndex = 0;

        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            ship.SetControls(0, 0);
            ship.SetRotationTarget(false, 0f);
            return;
        }

        // Sync NavMeshAgent position with ship position
        agent.nextPosition = transform.position;

        // Update destination if target moved significantly
        if (Vector3.Distance(agent.destination, target.position) > 0.5f)
        {
            agent.SetDestination(target.position);
            currentWaypointIndex = 0; // Reset waypoint tracking when path changes
        }

        if (!agent.hasPath || agent.path.corners.Length == 0)
        {
            ship.SetControls(0, 0);
            ship.SetRotationTarget(false, 0f);
            return;
        }

        // Translate the NavMeshAgent's desired movement into Ship controls
        NavigatePath();
    }

    private void NavigatePath()
    {
        // Ensure we have a valid waypoint index
        currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, agent.path.corners.Length - 1);
        
        // Get the current target waypoint
        Vector3 nextWaypoint = agent.path.corners[currentWaypointIndex];
        Vector3 velocity = ship.CurrentVelocity;
        
        // Check if we've reached the current waypoint
        float distanceToWaypoint = Vector3.Distance(transform.position, nextWaypoint);

        if (distanceToWaypoint < waypointDistance && currentWaypointIndex < agent.path.corners.Length - 1)
        {
            currentWaypointIndex++;
            nextWaypoint = agent.path.corners[currentWaypointIndex];
        }

        VelocityPilot.ComputeInputs(nextWaypoint, transform.position, ship.CurrentVelocity, ship.transform.up, out float thrustCmd, out float strafeCmd, out float rotTargetDeg);
        ship.SetControls(thrustCmd, strafeCmd);
        ship.SetRotationTarget(true, rotTargetDeg);

        return;


        // Set rotation target for the ship using the Vector3 overload

        // Calculate movement based on distance and ship's current orientation
        Vector3 directionToWaypoint = (nextWaypoint - transform.position).normalized;
        
        // Get the angle between ship's forward direction and target direction
        float angleDifference = Vector3.SignedAngle(-1*velocity.normalized, directionToWaypoint, ship.GetPlaneNormal());
        ship.SetRotationTarget(true, angleDifference);
        // Calculate thrust and strafe based on how aligned we are with the target
        float thrustInput = 0f;
        float strafeInput = 0f;
        

            // Move forward towards the target
        thrustInput = Mathf.Clamp01(Mathf.Pow(distanceToWaypoint, 1/6f)); // Scale thrust based on distance
        
        // Use minimal strafe for fine adjustments
        strafeInput = Mathf.Clamp(-angleDifference / 30f, -1f, 1f);

        ship.SetControls(thrustInput, strafeInput);
        
        // Debug logging
        Debug.Log($"AI Navigation - Waypoint: {currentWaypointIndex}/{agent.path.corners.Length-1}, Distance: {distanceToWaypoint:F2}, Angle: {angleDifference:F1}°, Thrust: {thrustInput:F2}, Strafe: {strafeInput:F2}");
    }
    private void OnDrawGizmos()
    {
        if (agent != null && agent.hasPath && agent.path.corners.Length > 0)
        {
            // Draw the full path
            Gizmos.color = Color.blue;
            for (int i = 0; i < agent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(agent.path.corners[i], agent.path.corners[i + 1]);
            }
            
            // Highlight current waypoint
            if (currentWaypointIndex < agent.path.corners.Length)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(agent.path.corners[currentWaypointIndex], waypointDistance);
                
                // Draw line to current waypoint
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, agent.path.corners[currentWaypointIndex]);
            }
            
            // Draw ship's forward direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up * 2f);
        }
    }
} 