using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SeekTarget", story: "Agent sets path-planning to seek [Target]", category: "Action", id: "0d2022f4769a9bf08524ebe2e3a78985")]
public partial class SeekTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;
    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        this.GameObject.GetComponent<AIShipInput>().SetNavigationTarget(Target.Value,true);
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

