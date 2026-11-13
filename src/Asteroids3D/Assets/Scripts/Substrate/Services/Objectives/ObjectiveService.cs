using System;
using System.Collections.Generic;
using Objectives;
using Objectives.Presentation;
using UnityEngine;

namespace Substrate.Services.Objectives
{
    /// <summary>Owns the spine tracker plus open local trackers, which it ticks itself via Update, the open find markers and the timed boosts in force.</summary>
    public class ObjectiveService : MonoBehaviour, IObjectiveService
    {
        private readonly List<LocalObjectiveHandle> locals = new();
        private readonly List<LocalObjectiveHandle> tickBuffer = new();
        private readonly List<FindMarker> finds = new();
        private readonly List<SectorBoost> boosts = new();
        private SpineObjectiveHandle currentSpine;

        public ObjectiveTracker SpineTracker { get; private set; }
        public ObjectiveType? SpineState => SpineTracker?.CurrentState;
        public string SpineStep => SpineTracker?.CurrentStep;
        public Transform SpineTarget { get; private set; }
        public IReadOnlyList<LocalObjectiveHandle> Locals => locals;
        public IReadOnlyList<FindMarker> Finds => finds;
        public IReadOnlyList<SectorBoost> Boosts => boosts;

        public event Action<ObjectiveType, ObjectiveType> OnSpineStateChanged;
        public event Action<string> OnSpineStepChanged;
        public event Action<Transform> OnSpineTargetChanged;

        public SpineObjectiveHandle SetSpineObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null,
            Func<SectorPhaseProgress> phaseProgress = null)
        {
            CloseSpineCore();

            SpineTracker = new ObjectiveTracker(mission, builders, phaseProgress);
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
            return handle;
        }

        public FindMarker MarkFind(Transform target, Sprite icon, Color colour)
        {
            if (!target) throw new ArgumentNullException(nameof(target));
            if (!icon) throw new ArgumentNullException(nameof(icon));
            var find = new FindMarker(target, icon, colour, CloseFind);
            finds.Add(find);
            return find;
        }

        public SectorBoost ShowBoost(string label, float endsAt)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("A boost shows a label.", nameof(label));
            var boost = new SectorBoost(label, endsAt, CloseBoost);
            boosts.Add(boost);
            return boost;
        }

        public void ClearAll()
        {
            while (locals.Count > 0)
                locals[locals.Count - 1].Close();
            while (finds.Count > 0)
                finds[finds.Count - 1].Close();
            while (boosts.Count > 0)
                boosts[boosts.Count - 1].Close();
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
            locals.Remove(handle);
        }

        private void CloseFind(FindMarker find) => finds.Remove(find);

        private void CloseBoost(SectorBoost boost) => boosts.Remove(boost);

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
