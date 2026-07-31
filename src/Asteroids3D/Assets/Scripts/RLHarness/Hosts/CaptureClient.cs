using System;
using System.Collections;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The capture lane client: one composition on the single seed against one opponent block, filming episodesPerSeed episodes (RunBlock records the spec's selection). No summary artifact — the JSONL rows fingerprint the clips, and nothing named *-summary.json is written so the eval gate's glob stays unambiguous.</summary>
    internal sealed class CaptureClient : ISessionClient
    {
        public IEnumerator Run(HarnessSessionHost host, SessionSpec spec)
        {
            var baseSpec = EvalProtocol.EvalSpec(spec.fieldDensityScale);
            baseSpec.runSeed = spec.seeds[0];
            var block = Block(spec);
            var jsonlPath = EpisodeJsonl.NewRunPath($"capture-vs-{block.Label}", "rl-capture", spec.outDir);

            var field = baseSpec.useAsteroidField
                ? HarnessField.Spawn(host.Arena, host.Assets, baseSpec.fieldDensityScale,
                    presentationEnabled: spec.Presentation)
                : null;
            var composition = host.NewComposition(in baseSpec, spec.opponentKind, field);

            yield return host.RunBlock(composition, block, spec.episodesPerSeed, baseSpec, jsonlPath, null);

            composition.Dispose();
            host.Projectiles.ReturnAllToPool();
            field?.Dispose();
            Debug.Log($"[CaptureClient] filmed {spec.episodesPerSeed} episodes vs {block.Label} → {jsonlPath}");
        }

        private static OpponentSpec Block(SessionSpec spec) => spec.opponentKind switch
        {
            OpponentKind.Archetype => OpponentSpec.Pinned(spec.opponentArchetype),
            OpponentKind.Mirror => OpponentSpec.Mirror,
            OpponentKind.Checkpoint => OpponentSpec.Checkpoint(spec.opponentLabel),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.opponentKind,
                "capture forbids roster (validated at parse)"),
        };
    }
}
