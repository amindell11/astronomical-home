using System;
using AI;
using Movement.MPC;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    public enum OpponentArchetype { Aggressor, Evader, Orbiter, Kiter, Dummy }

    /// <summary>An archetype's shape parameters — drawn per episode by the roster (and embedded in the episode's JSONL row) or authored on a live pilot, then handed to <see cref="ArchetypeBrain.Configure"/>; fields the archetype does not use stay zero.</summary>
    [Serializable]
    public struct OpponentDraw
    {
        [HideInInspector] public string archetype;
        [Tooltip("Caps the velocity law at this fraction of the airframe's max speed. The roster draws 0.7-1.0, or 0.4-0.6 for the Orbiter — above ~0.6 the orbit slides outside the laser envelope.")]
        public float speedFraction;
        [Tooltip("Evader: seconds between juke flips. The roster draws 0.6-1.8.")]
        public float jukePeriod;
        [Tooltip("Orbiter: the circling radius. The roster draws 14-18.")]
        public float orbitRadius;
        [Tooltip("Orbiter: circling direction — positive circles counter-clockwise.")]
        public int orbitDirection;
        [Tooltip("Aggressor/Kiter: the range held on the line of sight. The roster draws 8-12 for the Aggressor and 14-18 for the Kiter; the laser envelope is 20 u.")]
        public float desiredRange;
    }

    /// <summary>Per-episode opponent policy source for the episode loop: consulted BEFORE each pair-reset (respawn re-inits the installed brain — the traversal-probe ordering), it draws an archetype + jitter params on their own seed stream and installs through <see cref="AICommander.InstallBrain"/>. Every archetype drives the velocity interface. Mixture weights ride the spec (curriculum-driven via <see cref="EnvParamOverlay"/>).</summary>
    public sealed class OpponentRoster : IDisposable
    {
        private const uint ArchetypeStream = 505;
        private const uint JukeSeedStream = 1;

        /// <summary>The harness-standard velocity-mode tracker weight (PR-3's policy-matched interface). The opponent airframe's production settings never author it (the utility path never drove velocity mode; the script default is too loose to hold a reference — the dummy random-walks tens of units).</summary>
        private const float ScriptedWVelTrack = 50f;

        // The laser envelope is 20 u — orbit and hold ranges stay inside it so fire-capable archetypes shoot.
        private const float MinSpeedFraction = 0.7f;
        private const float MaxSpeedFraction = 1.0f;
        private const float MinJukePeriod = 0.6f;
        private const float MaxJukePeriod = 1.8f;
        private const float MinOrbitRadius = 14f;
        private const float MaxOrbitRadius = 18f;
        // Above ~0.6 the required v²/R exceeds thrust authority at these radii — the orbit slides outside the envelope.
        private const float MinOrbitSpeedFraction = 0.4f;
        private const float MaxOrbitSpeedFraction = 0.6f;
        private const float MinKiteRange = 14f;
        private const float MaxKiteRange = 18f;
        // The brawl band sits well inside the 20 u laser envelope so the aggressor fights up close.
        private const float MinAggroRange = 8f;
        private const float MaxAggroRange = 12f;

        private readonly AICommander commander;
        private readonly Ship enemy;
        private readonly Navigator navigator;
        private readonly MpcSettings originalSettings;
        private readonly MpcSettings settingsClone;

        public OpponentRoster(Ship opponent, Ship enemy)
        {
            this.enemy = enemy;
            commander = opponent.GetComponentInChildren<AICommander>();

            // Traversal-probe precedent: the next respawn re-creates the solver from the clone.
            navigator = commander.Navigator;
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

        /// <summary>Mixture draw: picks an archetype by the spec's weights, then jitters and installs it.</summary>
        public OpponentDraw Install(in RewardSpec spec, int episodeIndex, Vector2 arenaCenter)
        {
            var scope = Scope(spec.runSeed, episodeIndex);
            return Install(Pick(new System.Random(scope.ToSeed()), in spec), in spec, episodeIndex, arenaCenter);
        }

        /// <summary>Pinned draw for the degeneracy gate: jitters and installs the given archetype. <paramref name="drive"/> selects the K1-2 open-loop probe arms; the default is the production path.</summary>
        public OpponentDraw Install(OpponentArchetype archetype, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter, ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            var scope = Scope(spec.runSeed, episodeIndex);
            var rng = new System.Random(scope.ToSeed());
            Pick(rng, in spec); // burn the selection roll so jitter draws match between mixture and pinned runs
            var draw = new OpponentDraw { archetype = archetype.ToString() };

            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                    draw.speedFraction = Draw(rng, MinSpeedFraction, MaxSpeedFraction);
                    draw.desiredRange = Draw(rng, MinAggroRange, MaxAggroRange);
                    break;
                case OpponentArchetype.Evader:
                    draw.speedFraction = Draw(rng, MinSpeedFraction, MaxSpeedFraction);
                    draw.jukePeriod = Draw(rng, MinJukePeriod, MaxJukePeriod);
                    break;
                case OpponentArchetype.Orbiter:
                    draw.speedFraction = Draw(rng, MinOrbitSpeedFraction, MaxOrbitSpeedFraction);
                    draw.orbitRadius = Draw(rng, MinOrbitRadius, MaxOrbitRadius);
                    draw.orbitDirection = rng.Next(2) == 0 ? -1 : 1;
                    break;
                case OpponentArchetype.Kiter:
                    draw.speedFraction = Draw(rng, MinSpeedFraction, MaxSpeedFraction);
                    draw.desiredRange = Draw(rng, MinKiteRange, MaxKiteRange);
                    break;
            }

            var jukeSeed = scope.Derive(JukeSeedStream).ToSeed();
            // arenaRadius is copied out because a lambda cannot capture the `in` spec.
            var arenaRadius = spec.arenaRadius;
            commander.InstallBrain(host =>
            {
                var brain = host.AddComponent<ArchetypeBrain>();
                brain.Configure(enemy, archetype, in draw, jukeSeed, arenaCenter, arenaRadius, drive);
                return brain;
            });
            return draw;
        }

        private static SeedScope Scope(int runSeed, int episodeIndex) =>
            new SeedScope(runSeed).Derive((uint)episodeIndex).Derive(ArchetypeStream);

        private static float Draw(System.Random rng, float min, float max) =>
            min + (max - min) * (float)rng.NextDouble();

        internal static OpponentArchetype Pick(System.Random rng, in RewardSpec spec)
        {
            var roll = (float)rng.NextDouble()
                * (spec.weightAggressor + spec.weightEvader + spec.weightOrbiter + spec.weightKiter + spec.weightDummy);
            if ((roll -= spec.weightAggressor) < 0f) return OpponentArchetype.Aggressor;
            if ((roll -= spec.weightEvader) < 0f) return OpponentArchetype.Evader;
            if ((roll -= spec.weightOrbiter) < 0f) return OpponentArchetype.Orbiter;
            if (roll - spec.weightKiter < 0f) return OpponentArchetype.Kiter;
            return OpponentArchetype.Dummy;
        }
    }
}
