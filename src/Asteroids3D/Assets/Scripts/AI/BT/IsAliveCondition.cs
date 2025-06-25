using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "IsAlive", story: "[Ship] is Alive", category: "Conditions", id: "6b136bd7aa7cd8a750e5a1a375dcc4d2")]
public partial class IsAliveCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Ship> Ship;

    public override bool IsTrue()
    {
        return Ship.Value.damageHandler.CurrentHealth > 0;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
