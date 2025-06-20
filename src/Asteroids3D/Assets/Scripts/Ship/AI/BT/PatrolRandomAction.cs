using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "PatrolRandom", story: "Agent chooses a random waypoint and navigates to it", category: "Action", id: "15d66e1f7fe1b2d90739f8829ec39fc3")]
public partial class PatrolRandomAction : Action
{
    [SerializeField] private float patrolRadius = 50f;
    [SerializeField] private float arriveThreshold = 5f;
    [SerializeField] private bool enableAvoidance = true;
    
    private AIShipInput aiInput;
    private Vector3 currentTarget;
    private bool hasTarget = false;

    protected override Status OnStart()
    {
        // Get the AI input component
        aiInput = GameObject.GetComponent<AIShipInput>();
        if (aiInput == null)
        {
            Debug.LogError($"PatrolRandomAction: No AIShipInput component found on {GameObject.name}");
            return Status.Failure;
        }

        // Choose a new random patrol point
        ChooseNewPatrolPoint();
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (aiInput == null || !hasTarget)
        {
            return Status.Failure;
        }

        // Check if we've arrived at the target
        float distanceToTarget = Vector3.Distance(GameObject.transform.position, currentTarget);
        
        if (distanceToTarget <= arriveThreshold)
        {
            // We've arrived at the patrol point
            return Status.Success;
        }

        // Still moving towards target
        return Status.Running;
    }

    protected override void OnEnd()
    {
        // Clean up if needed
        hasTarget = false;
    }

    private void ChooseNewPatrolPoint()
    {
        // Try to pick a point that is visible on the player's screen so the AI stays within view.
        Camera cam = Camera.main;

        if (cam != null)
        {
            // Pick a random viewport coordinate with 10% padding so we do not hug the edges
            const float pad = 0.1f;
            Vector3 viewport = new Vector3(
                UnityEngine.Random.Range(pad, 1f - pad),
                UnityEngine.Random.Range(pad, 1f - pad),
                0f);

            // Re-use ship depth so projection lands roughly in the game plane
            Vector3 shipScreen   = cam.WorldToScreenPoint(GameObject.transform.position);
            viewport.z          = shipScreen.z;

            // Convert to world space and project onto the GamePlane to ensure we stay flat
            Vector3 worldPoint  = cam.ViewportToWorldPoint(viewport);
            Vector3 planePoint  = GamePlane.Origin + GamePlane.ProjectOntoPlane(worldPoint);

            currentTarget = planePoint;
            Debug.Log($"PatrolRandomAction: New SCREEN patrol target set at {currentTarget}");
        }
        else
        {
            // Fallback: pick a random point within patrolRadius around current position
            Vector3 currentPos      = GameObject.transform.position;
            float randomDistance    = UnityEngine.Random.Range(patrolRadius * 0.3f, patrolRadius);
            Vector3 randomOffset    = GamePlane.ProjectOntoPlane(UnityEngine.Random.insideUnitSphere).normalized * randomDistance;
            currentTarget           = currentPos + randomOffset;
            Debug.Log($"PatrolRandomAction: New RADIAL patrol target set at {currentTarget} (distance: {randomDistance:F1})");
        }

        hasTarget = true;

        // Set the navigation target using the AI input
        aiInput.SetNavigationPoint(currentTarget, enableAvoidance);
    }
}

