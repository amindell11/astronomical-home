using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "FireWeaponAction", story: "DEPRECATED: Firing is now handled by AIShipInput automatically when a target is set.", category: "Action", id: "b4971c5f668df6e186dbf14373f655f1")]
public partial class FireWeaponAction : Action
{
    [SerializeReference, Tooltip("This field is no longer used.")] 
    public BlackboardVariable<WeaponComponent> Weapon;

    protected override Status OnStart() => Status.Success;

    protected override Status OnUpdate()
    {
        // This action is now a no-op.
        // Firing logic has been moved into AIShipInput and is triggered automatically
        // when a navigation target is set (e.g., via SeekTargetAction).
        // This node is kept for compatibility with existing Behavior Trees,
        // but it no longer has any effect.
        return Status.Success;
    }
}

