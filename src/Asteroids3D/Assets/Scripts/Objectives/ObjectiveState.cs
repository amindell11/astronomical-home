using System;

namespace Objectives
{
    public enum ObjectiveType { Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed, Completed, ClearHostiles, Survive }

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

        /// <summary>One HUD line for the player while this state runs; null shows nothing.</summary>
        public virtual string StatusLine => null;
    }
}
