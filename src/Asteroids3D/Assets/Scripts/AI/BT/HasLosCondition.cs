using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "HasLOS", story: "Agent is in LOS of [Target]", category: "Conditions", id: "46d5b28d52400d5cc04df1e8236ec967")]
public partial class HasLosCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    public override bool IsTrue()
    {
        return this.GameObject.GetComponent<AIShipInput>().HasLineOfSight(Target.Value);
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
