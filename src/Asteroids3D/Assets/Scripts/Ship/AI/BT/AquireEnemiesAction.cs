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

        // Scan for any Ship components in range (excluding self)
        Collider[] hits = Physics.OverlapSphere(self.transform.position, Radius.Value, LayerMask.GetMask("Ship"));
        foreach (var col in hits)
        {
            if (!col) continue;
            Ship other = col.GetComponentInParent<Ship>();
            if (other && other.gameObject != self && (other.tag == Tag.Value || Tag.Value == ""))
            {
                Enemy.Value = other;
                return Status.Success;
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

