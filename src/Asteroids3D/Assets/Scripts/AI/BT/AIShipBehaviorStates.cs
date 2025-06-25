using System;
using Unity.Behavior;

[BlackboardEnum]
[Serializable]
public enum AIShipBehaviorStates
{
    Idle = 0,
	Patrol = 1,
	Evade = 2,
	Attack = 3
}
