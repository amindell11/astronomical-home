using System;
using Objectives;

namespace Game.Services
{
    public class ObjectiveService : IObjectiveService
    {
        public ObjectiveTracker CurrentTracker { get; private set; }
        public ObjectiveType? CurrentState => CurrentTracker?.CurrentState;

        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public void SetObjective(
            MissionDefinition mission,
            ObjectiveStateFactory factory,
            IPlayerAlive playerAlive)
        {
            Clear();

            CurrentTracker = new ObjectiveTracker(mission, factory, playerAlive);
            CurrentTracker.OnStateChanged += ForwardStateChanged;
        }

        public void Tick(float deltaTime)
        {
            CurrentTracker?.Tick(deltaTime);
        }

        public void Restart()
        {
            CurrentTracker?.Restart();
        }

        public void Clear()
        {
            if (CurrentTracker != null)
            {
                CurrentTracker.OnStateChanged -= ForwardStateChanged;
                CurrentTracker = null;
            }
        }

        private void ForwardStateChanged(ObjectiveType from, ObjectiveType to)
        {
            OnStateChanged?.Invoke(from, to);
        }
    }
}
