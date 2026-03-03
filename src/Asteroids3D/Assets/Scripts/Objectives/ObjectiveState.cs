namespace Objectives
{
    public enum ObjectiveType { Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed }

    public abstract class ObjectiveState
    {
        public abstract ObjectiveType StateType { get; }

        public virtual void Enter() { }

        public abstract void Tick(float deltaTime);

        public virtual void Exit() { }

        public abstract bool IsComplete { get; }
    }
}