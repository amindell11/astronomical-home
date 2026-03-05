using System;

namespace Objectives.States
{
    public class FailedState : ObjectiveState
    {
        public FailedState(Action onEnter = null) : base(onEnter) { }
        public override ObjectiveType StateType => ObjectiveType.Failed;
        public override void Tick(float deltaTime) { }
        public override bool IsComplete => false;
    }
}
