using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "FireWeaponAction", story: "Agent fires [Weapon]", category: "Action", id: "b4971c5f668df6e186dbf14373f655f1")]
public partial class FireWeaponAction : Action
{
    [SerializeReference] public BlackboardVariable<WeaponComponent> Weapon;

    protected override Status OnStart() => Status.Running;

    protected override Status OnUpdate()
    {
        if (Weapon.Value != null)
        {
            Weapon.Value.Fire();
            return Status.Success;
        }
        return Status.Failure;
    }
}

