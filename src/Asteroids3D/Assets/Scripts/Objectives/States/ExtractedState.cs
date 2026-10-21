using System;

namespace Objectives.States
{
    public class ExtractedState : ObjectiveState
    {
        public ExtractedState(Action onEnter = null) : base(onEnter) { }
        public override ObjectiveType StateType => ObjectiveType.Extracted;
        public override void Tick(float deltaTime) { }
        public override bool IsComplete => false;
    }
}
