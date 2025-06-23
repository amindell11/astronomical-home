using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "AquireEnemies", story: "First [Enemy] within [Radius] is acquired with [Tag]", category: "Action", id: "bac221e895ed5a1a05bb060ab0e751d5")]
public partial class AquireEnemiesAction : Action
{
    [SerializeReference] public BlackboardVariable<Ship> Enemy;
    [SerializeReference, Tooltip("Detection radius (world units)")] public BlackboardVariable<float> Radius;
    [SerializeReference, Tooltip("Filter by tag")] public BlackboardVariable<String> Tag;

    // Pre-allocated buffer for overlap queries (Optimization #3)
    private static readonly Collider[] enemyHitBuffer = new Collider[32];

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        GameObject self = this.GameObject;
        if (!self)
            return Status.Failure;

        // If we already have a valid enemy within range, succeed immediately
        if (Enemy != null && Enemy.Value != null && Enemy.Value.gameObject.activeInHierarchy)
        {
        return Status.Success;
        }

        // Scan for any Ship components in range (excluding self) using non-allocating overlap sphere
        int hitCount = Physics.OverlapSphereNonAlloc(self.transform.position, Radius.Value, enemyHitBuffer, LayerMask.GetMask("Ship"));
        for (int i = 0; i < hitCount; i++)
        {
            var col = enemyHitBuffer[i];
            if (!col) continue;
            Ship other = col.GetComponentInParent<Ship>();
            if (other && other.gameObject != self)
            {
                // Replace string tag comparison with layer mask comparison (Optimization #3)
                if (string.IsNullOrEmpty(Tag.Value) || other.gameObject.layer == LayerMask.NameToLayer(Tag.Value))
                {
                    Enemy.Value = other;
                    return Status.Success;
                }
            }
        }

        // No enemies found within radius
        Enemy.Value = null;
        return Status.Failure;
    }

    protected override void OnEnd()
    {
    }
}

