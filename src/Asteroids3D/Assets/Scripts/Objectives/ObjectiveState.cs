using System;

namespace Objectives
{
    public enum ObjectiveType { Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed }

    public abstract class ObjectiveState
    {
        private readonly Action onEnter;

        protected ObjectiveState(Action onEnter = null)
        {
            this.onEnter = onEnter;
        }

        public abstract ObjectiveType StateType { get; }

        public virtual void Enter() => onEnter?.Invoke();

        public abstract void Tick(float deltaTime);

        public virtual void Exit() { }

        public abstract bool IsComplete { get; }
    }
}
