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
        // Generate a random point within the patrol radius around the current position
        Vector3 currentPos = GameObject.transform.position;

        float randomDistance = UnityEngine.Random.Range(patrolRadius * 0.3f, patrolRadius);
        Vector3 randomOffset = GamePlane.ProjectOntoPlane(UnityEngine.Random.insideUnitSphere).normalized*randomDistance;
        
        currentTarget = currentPos + randomOffset;
        hasTarget = true;
        
        // Set the navigation target using the AI input
        aiInput.SetNavigationPoint(currentTarget, enableAvoidance);
        
        Debug.Log($"PatrolRandomAction: New patrol target set at {currentTarget} (distance: {randomDistance:F1})");
    }
}

