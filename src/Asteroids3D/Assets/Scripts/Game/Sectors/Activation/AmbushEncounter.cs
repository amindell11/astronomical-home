using System;
using System.Collections;
using System.Collections.Generic;
using Game.Services;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Ambush encounter: fires on its terms (spatial + delay), opens a concurrent local clear-hostiles objective over its wave spawner (observation-only ref), and latches its cleared output when the wave is dead.</summary>
    public class AmbushEncounter : ActivationRule, IHostileTracker
    {
        public const string StepClear = "clear-hostiles";
        public const string StepCleared = "cleared";
        public const string OutputCleared = "cleared";

        [SerializeField] private SectorSpawner waveSpawner;

        private SectorBuildContext ctx;
        private LocalObjectiveHandle local;

        public override IEnumerable<SignalOutput> Outputs
        {
            get
            {
                foreach (var output in base.Outputs) yield return output;
                yield return new SignalOutput(OutputCleared, SignalKind.Latch);
            }
        }

        // An unspawned wave reads as not-cleared: a misconfigured spawner must never complete the local silently.
        public bool HostilesCleared
        {
            get
            {
                if (!waveSpawner || waveSpawner.Spawned.Count == 0) return false;
                foreach (var ship in waveSpawner.Spawned)
                    if (ship && ship.gameObject.activeInHierarchy) return false;
                return true;
            }
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!waveSpawner)
            {
                Debug.LogError($"AmbushEncounter on '{name}' has no wave spawner — inert.", this);
                yield break;
            }

            this.ctx = ctx;
            var setup = base.Setup(ctx);
            while (setup.MoveNext()) yield return setup.Current;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            local?.Close();
            local = null;
            this.ctx = default;
            return base.Teardown(ctx);
        }

        protected override void OnFired()
        {
            var mission = new MissionDefinition(
                StepClear, new Dictionary<string, string> { { StepClear, StepCleared } });
            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                [StepClear] = () => new ClearHostilesState(this),
                [StepCleared] = () => new CompletedState(onEnter: HandleCleared)
            };
            local = ctx.Services.ObjectiveService.OpenLocal(mission, builders, waveSpawner.transform);
        }

        private void HandleCleared()
        {
            ctx.Bus?.Latch(new SignalRef(this, OutputCleared));
            local?.Close();
            local = null;
        }

#if UNITY_EDITOR
        internal void Bind(SectorSpawner spawner) => waveSpawner = spawner;
#endif
    }
}
