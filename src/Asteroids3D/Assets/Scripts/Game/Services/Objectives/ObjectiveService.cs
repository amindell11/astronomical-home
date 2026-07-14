using System;
using System.Collections.Generic;
using Objectives;
using UnityEngine;

namespace Game.Services
{
    /// <summary>Owns the spine tracker plus open local trackers and ticks them itself via Update.</summary>
    public class ObjectiveService : MonoBehaviour, IObjectiveService, IObjectiveTrackerAdapter
    {
        private readonly List<LocalObjectiveHandle> locals = new();
        private readonly List<LocalObjectiveHandle> tickBuffer = new();
        private SpineObjectiveHandle currentSpine;

        public ObjectiveTracker SpineTracker { get; private set; }
        public ObjectiveType? SpineState => SpineTracker?.CurrentState;
        public string SpineStep => SpineTracker?.CurrentStep;
        public Transform SpineTarget { get; private set; }
        public IReadOnlyList<LocalObjectiveHandle> Locals => locals;

        ObjectiveType IObjectiveTrackerAdapter.CurrentState => SpineTracker?.CurrentState ?? ObjectiveType.Explore;

        event Action<ObjectiveType, ObjectiveType> IObjectiveTrackerAdapter.OnStateChanged
        {
            add { OnSpineStateChanged += value; }
            remove { OnSpineStateChanged -= value; }
        }

        public event Action<ObjectiveType, ObjectiveType> OnSpineStateChanged;
        public event Action<string> OnSpineStepChanged;
        public event Action<Transform> OnSpineTargetChanged;
        public event Action OnLocalsChanged;

        public SpineObjectiveHandle SetSpineObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null)
        {
            CloseSpineCore();

            SpineTracker = new ObjectiveTracker(mission, builders);
            SpineTracker.OnStateChanged += ForwardSpineStateChanged;
            SpineTracker.OnStepChanged += ForwardSpineStepChanged;
            currentSpine = new SpineObjectiveHandle(this, SpineTracker);
            SetSpineTargetCore(target);
            OnSpineStepChanged?.Invoke(SpineTracker.CurrentStep);
            return currentSpine;
        }

        internal bool IsCurrent(SpineObjectiveHandle handle) => currentSpine == handle;

        internal void SetSpineTarget(SpineObjectiveHandle handle, Transform target)
        {
            if (IsCurrent(handle)) SetSpineTargetCore(target);
        }

        internal void CloseSpine(SpineObjectiveHandle handle)
        {
            if (IsCurrent(handle)) CloseSpineCore();
        }

        public LocalObjectiveHandle OpenLocal(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null)
        {
            var handle = new LocalObjectiveHandle(new ObjectiveTracker(mission, builders), target, CloseLocal);
            locals.Add(handle);
            OnLocalsChanged?.Invoke();
            return handle;
        }

        public void ClearAll()
        {
            // Drain until empty: OnLocalsChanged handlers may close other handles or open new ones mid-sweep.
            while (locals.Count > 0)
                locals[locals.Count - 1].Close();
            CloseSpineCore();
        }

        private void Update() => Tick(Time.deltaTime);

        // Locals tick over a snapshot so a handle opening or closing mid-tick cannot corrupt iteration.
        internal void Tick(float deltaTime)
        {
            SpineTracker?.Tick(deltaTime);

            tickBuffer.AddRange(locals);
            foreach (var local in tickBuffer)
            {
                if (locals.Contains(local))
                    local.Tracker.Tick(deltaTime);
            }
            tickBuffer.Clear();
        }

        private void SetSpineTargetCore(Transform target)
        {
            if (SpineTarget == target) return;
            SpineTarget = target;
            OnSpineTargetChanged?.Invoke(target);
        }

        private void CloseSpineCore()
        {
            if (SpineTracker != null)
            {
                SpineTracker.OnStateChanged -= ForwardSpineStateChanged;
                SpineTracker.OnStepChanged -= ForwardSpineStepChanged;
                SpineTracker = null;
            }
            currentSpine = null;
            SetSpineTargetCore(null);
        }

        private void CloseLocal(LocalObjectiveHandle handle)
        {
            if (locals.Remove(handle))
                OnLocalsChanged?.Invoke();
        }

        private void ForwardSpineStateChanged(ObjectiveType from, ObjectiveType to)
        {
            OnSpineStateChanged?.Invoke(from, to);
        }

        private void ForwardSpineStepChanged(string step)
        {
            OnSpineStepChanged?.Invoke(step);
        }
    }
}
