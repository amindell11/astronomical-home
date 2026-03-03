namespace Objectives.States
{
    public class ExtractedState : ObjectiveState
    {
        public override ObjectiveType StateType => ObjectiveType.Extracted;
        public override void Tick(float deltaTime) { }
        public override bool IsComplete => false;
    }
}
