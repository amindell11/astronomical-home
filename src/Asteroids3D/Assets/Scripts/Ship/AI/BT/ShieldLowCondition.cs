using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "ShieldState", story: "Ship shield is considered [State] with threshold [Threshold]", category: "Conditions", id: "62b53b5654226e0be717a95de4cfb437")]
public partial class ShieldStateCondition : Condition
{
    [SerializeReference, Tooltip("Fraction (0-1) of max shield used as threshold")] 
    public BlackboardVariable<float> Threshold;

    [SerializeReference, Tooltip("If true: Low (<=), false: High (>=)")]
    public BlackboardVariable<bool> State;

    public override bool IsTrue()
    {
        var dmg = this.GameObject.GetComponent<Ship>().damageHandler;
        if (!dmg || dmg.maxShield <= 0f) return false;

        float pct = dmg.CurrentShield / dmg.maxShield;

        bool isLow  = State != null && State.Value;
        float thr   = Threshold != null ? Threshold.Value : 0.25f;
        var val =  isLow ? pct <= thr : pct >= thr;
        Debug.Log($"ShieldStateCondition: pct={pct}, thr={thr}, isLow={isLow}, val={val}");
        return val;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
