using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "EvadeAction", story: "Agent Flees from [Enemy]", category: "Action", id: "c84e85759dff7566990323a5826444ff")]
public partial class EvadeAction : Action
{
    [SerializeReference] public BlackboardVariable<Ship> Enemy;

    // Cached runtime data
    AICommander _ai;
    Vector3     _evadePoint;
    Vector3     _evadeVelocity;
    const float DefaultFleeDistance = 30f; // World units to flee if no AI param available

    protected override Status OnStart()
    {
        // Validate inputs and cache references
        _ai = this.GameObject.GetComponent<AICommander>();
        if (_ai == null) return Status.Failure;

        Ship enemyShip = Enemy != null ? Enemy.Value : null;
        if (enemyShip == null || !enemyShip.gameObject.activeInHierarchy)
            return Status.Failure;

        // Compute a flee direction in the game plane away from the enemy.
        Vector3 selfPos   = this.GameObject.transform.position;
        Vector3 enemyPos  = enemyShip.transform.position;
        Vector3 dir       = (selfPos - enemyPos);
        if (dir.sqrMagnitude < 0.01f)
        {
            // Degenerate case – pick a random lateral direction in the plane.
            dir = GamePlane.ProjectOntoPlane(UnityEngine.Random.insideUnitSphere);
        }
        dir = dir.normalized;

        float distance = _ai.Navigator != null ? Mathf.Max(_ai.Navigator.arriveRadius * 3f, DefaultFleeDistance) : DefaultFleeDistance;
        _evadePoint = selfPos + dir * distance;
        var shipComponent = this.GameObject.GetComponentInParent<Ship>();
        float maxSpeed = shipComponent ? shipComponent.settings.maxSpeed : DefaultFleeDistance;
        _evadeVelocity = dir * maxSpeed;

        // Tell the AI pilot to navigate towards this point with avoidance enabled.
        _ai.SetNavigationPoint(_evadePoint, true);

        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        if (_ai == null) return Status.Failure;

        // Success if we've reached the evade point or enemy is gone.
        Ship enemyShip = Enemy != null ? Enemy.Value : null;
        if (enemyShip == null || !enemyShip.gameObject.activeInHierarchy)
            return Status.Success;

        float arrive = _ai.Navigator != null ? _ai.Navigator.arriveRadius : 5f;
        float distSq = (this.GameObject.transform.position - _evadePoint).sqrMagnitude;
        if (distSq <= arrive * arrive)
            return Status.Success;

        // Keep navigating – update target in case the point was altered externally.
        _ai.SetNavigationPoint(_evadePoint, true, _evadeVelocity);
        return Status.Success;
    }
}

