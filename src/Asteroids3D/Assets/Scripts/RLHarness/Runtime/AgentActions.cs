using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One decision's decoded command in the enemy-anchored frame: a facing offset around the intercept anchor (with authority weight) and a polar velocity (radial/tangential speeds in m/s, with authority weight), plus the fire/boost branches. All primitives — the MPC-frame assembly happens in <see cref="PolicyBrain"/>.</summary>
    public readonly struct AgentAction
    {
        public readonly float facingOffsetRad;
        public readonly float facingWeight;
        public readonly float radialSpeed;
        public readonly float tangentialSpeed;
        public readonly float velocityWeight;
        public readonly bool fire;
        public readonly bool boost;

        public AgentAction(float facingOffsetRad, float facingWeight, float radialSpeed,
            float tangentialSpeed, float velocityWeight, bool fire, bool boost)
        {
            this.facingOffsetRad = facingOffsetRad;
            this.facingWeight = facingWeight;
            this.radialSpeed = radialSpeed;
            this.tangentialSpeed = tangentialSpeed;
            this.velocityWeight = velocityWeight;
            this.fire = fire;
            this.boost = boost;
        }
    }

    /// <summary>Decodes the action vector — 5 continuous [ox, oy, vr, vt, vw] plus 2 discrete branches [fire, boost] — into enemy-anchored scalars. Facing rides as a direction: angle = offset around the intercept anchor ((0,+1) = aim at intercept, (0,−1) = face away), magnitude = authority weight (near-zero vectors have unstable angles AND near-zero weight). Velocity rides as normalized polar speeds scaled to maxSpeed with an explicit weight. This decode is MPC-type-free; the brain packs the scalars into the anchored intent.</summary>
    public static class AgentActions
    {
        public const int Count = 5;
        public const int ChoicesPerBranch = 2;

        // vr/vt stay unclamped: training-time exploration samples beyond [-1,1] while clipped ONNX
        // inference cannot — the shipped checkpoint trained unclamped, so clamping requires a retrain.
        public static AgentAction Map(float ox, float oy, float vr, float vt, float vw,
            int fire, int boost, float maxSpeed) => new(
            Mathf.Atan2(ox, oy),
            Mathf.Clamp01(Mathf.Sqrt(ox * ox + oy * oy)),
            vr * maxSpeed,
            vt * maxSpeed,
            Mathf.Clamp01(vw),
            fire == 1,
            boost == 1);
    }
}
