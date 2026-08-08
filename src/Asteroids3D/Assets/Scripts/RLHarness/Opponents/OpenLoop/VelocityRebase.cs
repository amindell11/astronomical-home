using Movement.MPC;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>How an archetype brain emits its velocity law: the production roster's border-steered world reference, or one of the K1-2 open-loop probe arms (border steer dropped, fire suppressed) — the same law packed legacy or mechanically rebased into the anchored channel.</summary>
    public enum ArchetypeDrive { Production, OpenLoopLegacy, OpenLoopAnchored }

    /// <summary>One 5 Hz decision's emitted velocity command, as the velrebase probe reads it: the world reference on the production/legacy arms, the polar pair on the anchored arm.</summary>
    public readonly struct ScriptedVelocityCommand
    {
        public readonly Vector2 worldVelocity;
        public readonly float radialSpeed;
        public readonly float tangentialSpeed;

        public ScriptedVelocityCommand(Vector2 worldVelocity, float radialSpeed, float tangentialSpeed)
        {
            this.worldVelocity = worldVelocity;
            this.radialSpeed = radialSpeed;
            this.tangentialSpeed = tangentialSpeed;
        }
    }

    /// <summary>The scripted-brain readout the velrebase probe samples — <see cref="AI.IPolicyReadout"/>'s shape at the smallest honest size (the archetypes have no action ring to expose). The counter bumps once per 5 Hz recompute, never per Decide.</summary>
    public interface IScriptedVelocityReadout
    {
        ArchetypeDrive Drive { get; }
        int TotalDecisions { get; }
        ScriptedVelocityCommand LastCommand { get; }
    }

    public static class VelocityRebase
    {
        /// <summary>Mechanical rebase: the law's world command projected onto the enemy-polar basis — the same numbers under anchored (enemy-relative) semantics, so the MPC re-resolves them per rollout step and the anchored arm's enemy-velocity lead is deliberate (K1-2 brief). Basis matches Cost.AnchoredVelocityRef: losHat toward the enemy, tangentHat CCW; the ε fallback mirrors the laws' own degenerate-los convention.</summary>
        public static AnchoredIntent ToAnchored(Vector2 lawVelocity, Vector2 selfPos, Vector2 enemyPos)
        {
            var los = enemyPos - selfPos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var tangentHat = new Vector2(losHat.y, -losHat.x);
            return new AnchoredIntent
            {
                hasVelocity = true,
                radialSpeed = Vector2.Dot(lawVelocity, losHat),
                tangentialSpeed = Vector2.Dot(lawVelocity, tangentHat),
                velocityWeight = 1f,
            };
        }
    }
}
