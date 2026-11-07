using System.Linq;
using AI.Strategy;
using Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    [InitializeOnLoad]
    internal static class BeatGizmos
    {
        static BeatGizmos() =>
            GizmoView.Register(typeof(AICommander), "beat", "Brain Beat",
                "each enemy's current beat, its seconds left and its last pick's two best scores", "Combat");

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(AICommander), "beat") || !GizmoView.InScope(commander)) return;
            if (!Application.isPlaying || commander.context?.Self == null || !commander.Brain) return;
            var readout = commander.Brain as IBeatReadout;
            if (readout == null) return;

            var left = float.IsPositiveInfinity(readout.BeatSecondsLeft) ? "" : $" {readout.BeatSecondsLeft:F1}s";
            var best = Enumerable.Range(0, readout.ScoredCount).Select(readout.Scored)
                .OrderByDescending(scored => scored.score).Take(2)
                .Select(scored => $"{scored.beat} {scored.score:F2}");
            ShipReadout.Draw(commander.context.Self.Kinematics.pos, ShipReadoutRow.Beat,
                $"{readout.Beat}{left}\n{string.Join("  ", best)}", Color.white);
        }
    }
}
