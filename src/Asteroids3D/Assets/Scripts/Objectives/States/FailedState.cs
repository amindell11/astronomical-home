namespace Objectives.States
{
    public class FailedState : ObjectiveState
    {
        public override ObjectiveType StateType => ObjectiveType.Failed;
        public override void Tick(float deltaTime) { }
        public override bool IsComplete => false;
        public override float ComputeUtility() => 0f;
    }
}
