#if UNITY_EDITOR
using System;
using AI;
using Asteroids.Fields;
using Game;
using Movement.MPC;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// Builds the two-AI chase scenario in a live <c>BigFieldSettings</c> asteroid field:
    /// a pursuer (<c>MaintainRange</c>) chasing an evader (<c>Flee</c>), both driven directly
    /// (their utility <c>Brain</c> disabled) so the pairing is fixed. Owns the spawned
    /// GameObjects and tears them down on <see cref="Dispose"/>. The caller owns the
    /// <see cref="ShipRegistry"/> and the <c>SolverBuffers.SeedBias</c> lifetime.
    /// </summary>
    public sealed class ChaseBenchmarkScenario : IDisposable
    {
        private const string FieldPrefabPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";

        public Ship Pursuer { get; private set; }
        public Ship Evader { get; private set; }
        public ShipBenchmarkProbe PursuerProbe { get; private set; }
        public ShipBenchmarkProbe EvaderProbe { get; private set; }

        private GameObject fieldGo;
        private readonly ChaseRunConfig config;

        public ChaseBenchmarkScenario(ChaseRunConfig config, ShipRegistry registry)
        {
            this.config = config;

            var pursuerPlane = config.startOffset;
            var evaderPlane = config.startOffset + new Vector2(config.startGap, 0f);
            var interceptRadius = Mathf.Max(config.desiredRange, 5f);

            Pursuer = ShipTestFactory.CreateDefaultShipAt(
                GamePlane.PlanePointToWorld(pursuerPlane), Quaternion.identity, useMpcPilot: true, team: 0);
            Evader = ShipTestFactory.CreateDefaultShipAt(
                GamePlane.PlanePointToWorld(evaderPlane), Quaternion.identity, useMpcPilot: true, team: 1);
            if (!Pursuer || !Evader)
                throw new InvalidOperationException("ChaseBenchmark: failed to create ships (check test asset paths).");

            registry.ActiveShips.Add(Pursuer);
            registry.ActiveShips.Add(Evader);

            var pursuerCmdr = Wire(Pursuer, Evader, registry, GoalMode.MaintainRange,
                config.desiredRange, config.rangeTolerance, "pursuer", interceptRadius, out var pProbe);
            var evaderCmdr = Wire(Evader, Pursuer, registry, GoalMode.Flee,
                0f, 0f, "evader", interceptRadius, out var eProbe);
            _ = pursuerCmdr; _ = evaderCmdr;
            PursuerProbe = pProbe;
            EvaderProbe = eProbe;

            // Deterministic BigField, anchored on the pursuer, with a carved clearing at the
            // start so neither ship spawns embedded in a rock (fair — applied every run).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldPrefabPath);
            if (!prefab)
                throw new InvalidOperationException($"ChaseBenchmark: field prefab not found at {FieldPrefabPath}");
            fieldGo = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            fieldGo.name = "ChaseBenchmark_Field";
            if (fieldGo.GetComponent<AsteroidField>() is UpdatingAsteroidField field)
            {
                field.SetPlayer(Pursuer.transform);
                field.SetPlayerStart(pursuerPlane);
            }
        }

        private static AICommander Wire(Ship self, Ship target, ShipRegistry registry,
            GoalMode goalMode, float desiredRange, float rangeTolerance, string role,
            float interceptRadius, out ShipBenchmarkProbe probe)
        {
            var cmdr = self.GetComponentInChildren<AICommander>();
            if (!cmdr) throw new InvalidOperationException("ChaseBenchmark: ship missing AICommander.");

            cmdr.SetRegistry(registry);      // triggers system init (Navigator/Scout/Brain)
            cmdr.Brain.enabled = false;      // bypass the utility state machine; we push intents

            var driver = cmdr.gameObject.AddComponent<ChaseIntentDriver>();
            driver.Configure(target, cmdr.Navigator, goalMode, desiredRange, rangeTolerance);

            probe = self.gameObject.AddComponent<ShipBenchmarkProbe>();
            probe.Configure(self, target, cmdr.Navigator, role, interceptRadius);
            return cmdr;
        }

        /// <summary>Snapshots both ships' metrics into a run result row.</summary>
        public ChaseRunResult BuildResult()
        {
            return new ChaseRunResult
            {
                index = config.index,
                label = config.label,
                startOffsetX = config.startOffset.x,
                startOffsetY = config.startOffset.y,
                seedBias = config.seedBias,
                ticks = config.ticks,
                simSeconds = config.ticks * Time.fixedDeltaTime,
                pursuer = PursuerProbe.Result(),
                evader = EvaderProbe.Result(),
                minDistance = float.IsInfinity(PursuerProbe.MinDistance) ? -1f : PursuerProbe.MinDistance,
                meanDistanceBehind = PursuerProbe.MeanDistanceBehind,
                timeToInterceptSec = PursuerProbe.TimeToInterceptSec,
            };
        }

        public void Dispose()
        {
            if (fieldGo)
            {
                if (fieldGo.GetComponent<AsteroidField>() is AsteroidField f) f.DespawnAll();
                UnityEngine.Object.DestroyImmediate(fieldGo);
            }
            if (Pursuer) UnityEngine.Object.DestroyImmediate(Pursuer.gameObject);
            if (Evader) UnityEngine.Object.DestroyImmediate(Evader.gameObject);
            fieldGo = null; Pursuer = null; Evader = null;
        }
    }
}
#endif
