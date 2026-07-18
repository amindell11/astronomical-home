using System;
using AI;
using Movement.MPC;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    public enum OpponentArchetype { Aggressor, Evader, Orbiter, Kiter, Dummy }

    /// <summary>The per-episode archetype selection + jitter draw, embedded in the episode's JSONL row; fields the archetype does not draw stay zero.</summary>
    [Serializable]
    public struct OpponentDraw
    {
        public string archetype;
        public float speedFraction;
        public float jukePeriod;
        public float orbitRadius;
        public int orbitDirection;
        public float desiredRange;
    }

    /// <summary>Per-episode opponent policy source for the episode loop: consulted BEFORE each pair-reset (respawn re-inits the installed chooser — the traversal-probe ordering), it draws an archetype + jitter params on their own seed stream and installs through <see cref="Brain.InstallChooser"/>. Aggressor re-installs the prefab-default utility chooser captured at construction. Mixture weights are fixed in PR-C; PR-D turns them into ML-Agents environment parameters.</summary>
    public sealed class OpponentRoster : IDisposable
    {
        private const uint ArchetypeStream = 505;
        private const uint JukeSeedStream = 1;

        /// <summary>The harness-standard velocity-mode tracker weight (PR-3's policy-matched interface). The opponent airframe's production settings never author it (the utility path never drives velocity mode; the script default is too loose to hold a reference — the dummy random-walks tens of units). Velocity-mode-only cost, so the aggressor's goal-mode behavior is untouched.</summary>
        private const float ScriptedWVelTrack = 50f;

        private const float AggressorWeight = 0.4f;
        private const float EvaderWeight = 0.2f;
        private const float OrbiterWeight = 0.15f;
        private const float KiterWeight = 0.15f;
        private const float DummyWeight = 0.1f;

        // Authored jitter ranges, tuned during the degeneracy gate. The laser envelope is 20 u — orbit and hold ranges stay inside it so fire-capable archetypes actually shoot.
        private const float MinSpeedFraction = 0.7f;
        private const float MaxSpeedFraction = 1.0f;
        private const float MinJukePeriod = 0.6f;
        private const float MaxJukePeriod = 1.8f;
        private const float MinOrbitRadius = 10f;
        private const float MaxOrbitRadius = 18f;
        // Centripetally feasible band: above ~0.6 the required v²/R exceeds thrust authority at these radii and the orbit slides outside the envelope (observed in the gate).
        private const float MinOrbitSpeedFraction = 0.4f;
        private const float MaxOrbitSpeedFraction = 0.6f;
        private const float MinKiteRange = 14f;
        private const float MaxKiteRange = 18f;

        private readonly Brain brain;
        private readonly IIntentChooser aggressorChooser;
        private readonly Ship enemy;
        private readonly float projectileSpeed;
        private readonly Navigator navigator;
        private readonly MpcSettings originalSettings;
        private readonly MpcSettings settingsClone;

        public OpponentRoster(Ship opponent, Ship enemy)
        {
            this.enemy = enemy;
            brain = opponent.GetComponentInChildren<Brain>();
            aggressorChooser = brain.Chooser;
            if (aggressorChooser is not IStateChooser)
                throw new InvalidOperationException(
                    "OpponentRoster must be constructed while the opponent still runs its prefab-default utility chooser — a scripted archetype is already installed.");
            projectileSpeed = opponent.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary);

            // Traversal-probe precedent: the next respawn re-creates the solver from the clone.
            navigator = opponent.GetComponentInChildren<AICommander>().Navigator;
            originalSettings = navigator.mpcSettings;
            settingsClone = UnityEngine.Object.Instantiate(originalSettings);
            settingsClone.wVelTrack = ScriptedWVelTrack;
            navigator.mpcSettings = settingsClone;
        }

        public void Dispose()
        {
            if (navigator) navigator.mpcSettings = originalSettings;
            if (settingsClone) UnityEngine.Object.DestroyImmediate(settingsClone);
        }

        /// <summary>Mixture draw: picks an archetype by the fixed weights, then jitters and installs it.</summary>
        public OpponentDraw Install(in RewardSpec spec, int episodeIndex, Vector2 arenaCenter)
        {
            var scope = Scope(spec.runSeed, episodeIndex);
            return Install(Pick(new System.Random(scope.ToSeed())), in spec, episodeIndex, arenaCenter);
        }

        /// <summary>Pinned draw for the degeneracy gate: jitters and installs the given archetype.</summary>
        public OpponentDraw Install(OpponentArchetype archetype, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter)
        {
            var scope = Scope(spec.runSeed, episodeIndex);
            var rng = new System.Random(scope.ToSeed());
            Pick(rng); // burn the selection roll so jitter draws match between mixture and pinned runs
            var draw = new OpponentDraw { archetype = archetype.ToString() };

            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                    brain.InstallChooser(aggressorChooser);
                    break;
                case OpponentArchetype.Evader:
                    draw.speedFraction = Draw(rng, MinSpeedFraction, MaxSpeedFraction);
                    draw.jukePeriod = Draw(rng, MinJukePeriod, MaxJukePeriod);
                    var evader = new EvaderChooser();
                    evader.Configure(enemy, draw.speedFraction, draw.jukePeriod,
                        scope.Derive(JukeSeedStream).ToSeed(), arenaCenter, spec.arenaRadius);
                    brain.InstallChooser(evader);
                    break;
                case OpponentArchetype.Orbiter:
                    draw.speedFraction = Draw(rng, MinOrbitSpeedFraction, MaxOrbitSpeedFraction);
                    draw.orbitRadius = Draw(rng, MinOrbitRadius, MaxOrbitRadius);
                    draw.orbitDirection = rng.Next(2) == 0 ? -1 : 1;
                    var orbiter = new OrbiterChooser();
                    orbiter.Configure(enemy, draw.orbitRadius, draw.orbitDirection, draw.speedFraction,
                        projectileSpeed, arenaCenter, spec.arenaRadius);
                    brain.InstallChooser(orbiter);
                    break;
                case OpponentArchetype.Kiter:
                    draw.speedFraction = Draw(rng, MinSpeedFraction, MaxSpeedFraction);
                    draw.desiredRange = Draw(rng, MinKiteRange, MaxKiteRange);
                    var kiter = new KiterChooser();
                    kiter.Configure(enemy, draw.desiredRange, draw.speedFraction, projectileSpeed,
                        arenaCenter, spec.arenaRadius);
                    brain.InstallChooser(kiter);
                    break;
                case OpponentArchetype.Dummy:
                    brain.InstallChooser(new DummyChooser());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
            return draw;
        }

        private static SeedScope Scope(int runSeed, int episodeIndex) =>
            new SeedScope(runSeed).Derive((uint)episodeIndex).Derive(ArchetypeStream);

        private static float Draw(System.Random rng, float min, float max) =>
            min + (max - min) * (float)rng.NextDouble();

        private static OpponentArchetype Pick(System.Random rng)
        {
            var roll = (float)rng.NextDouble()
                * (AggressorWeight + EvaderWeight + OrbiterWeight + KiterWeight + DummyWeight);
            if ((roll -= AggressorWeight) < 0f) return OpponentArchetype.Aggressor;
            if ((roll -= EvaderWeight) < 0f) return OpponentArchetype.Evader;
            if ((roll -= OrbiterWeight) < 0f) return OpponentArchetype.Orbiter;
            if (roll - KiterWeight < 0f) return OpponentArchetype.Kiter;
            return OpponentArchetype.Dummy;
        }
    }
}
