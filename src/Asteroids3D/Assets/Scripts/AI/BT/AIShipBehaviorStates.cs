using System;
using Unity.Behavior;

[BlackboardEnum]
[Serializable]
public enum AIShipBehaviorStates
{
    Idle = 0,
	Patrol = 1,
	Evade = 2,
	Attack = 3,

    // Placeholder states for forthcoming utility-based behavior tree
    ScanPatrol = 4,
    Investigate = 5,
    PredictivePursue = 6,
    StrafeRun = 7,
    KiteRun = 8,
    LowShieldEvade = 9,
    HideRepair = 10,
    Reposition = 11,
    Finisher = 12,
    GroupRegroup = 13
}
