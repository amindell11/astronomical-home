using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Jink-Evade state – performs aggressive zig-zag manoeuvres and full-throttle
    /// sprints to break enemy aim and missile locks.  Triggered in the most
    /// dangerous situations (incoming missile, very low HP, or point-blank enemy).
    /// </summary>
    public class JinkEvadeState : AIState
    {
        /* ── Tunables ─────────────────────────────────────────────── */
        private readonly float fleeDistance       = 40f;   // forward component away from enemy
        private readonly float sideStepDistance   = 12f;   // lateral jink amplitude
        private readonly float jinkInterval       = 0.75f; // seconds between direction flips
        private readonly float missileAmpFactor   = 1.5f;  // multiply amplitude when missile threat

        /* ── Internals ────────────────────────────────────────────── */
        private bool  jinkLeft;
        private float nextJinkTime;
        private Vector2 currentTarget;

        public JinkEvadeState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner) { }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            jinkLeft      = Random.value > 0.5f;
            nextJinkTime  = Time.time + jinkInterval;
            gunner.SetTarget(Vector2.zero); // cease fire while jinking
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            // Decide if we need to flip jink side
            if (Time.time >= nextJinkTime)
            {
                jinkLeft = !jinkLeft;
                nextJinkTime = Time.time + jinkInterval;
            }

            // Determine principal flee direction
            Vector2 fleeDir = ctx.Enemy != null ? -ctx.VectorToEnemy.normalized : Random.insideUnitCircle.normalized;

            // Perpendicular side-step direction (right-hand rule)
            Vector2 sideDir = jinkLeft ? new Vector2(fleeDir.y, -fleeDir.x)  // 90° CW
                                        : new Vector2(-fleeDir.y, fleeDir.x); // 90° CCW

            // Increase amplitude when a missile is incoming
            float amp = ctx.IncomingMissile ? sideStepDistance * missileAmpFactor : sideStepDistance;

            // Compose target offset in plane space
            Vector2 offset = fleeDir * fleeDistance + sideDir * amp;
            currentTarget  = ctx.SelfPosition + offset;

            navigator.SetNavigationPoint(currentTarget, avoid: true);
            navigator.SetFacingTarget(fleeDir); // point nose roughly along flee axis for max accel
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            float score = 0f;

            // Highest priority when missile threat present.
            if (ctx.IncomingMissile) score += 0.7f;

            // Low health/shield => strong urge to jink
            score += AIUtilityCurves.FearCurve(ctx.HealthPct, 0.25f);
            score += AIUtilityCurves.FearCurve(ctx.ShieldPct, 0.25f);

            // Enemy very close OR has clear LOS increases urgency
            if (ctx.Enemy != null)
            {
                float dist = ctx.VectorToEnemy.magnitude;
                if (dist < 10f) score += 0.2f;
                if (ctx.LineOfSightToEnemy) score += 0.15f;

                // Closing fast or enemy pointing at us
                float closing = ctx.ClosingSpeed;
                score += Mathf.Clamp(closing * 0.02f, 0f, 0.15f);
                float facingFactor = Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad); // 1 when facing us
                score += Mathf.Clamp01(facingFactor) * 0.1f;
            }

            // Penalise if no threat detected at all
            if (ctx.Enemy == null && !ctx.IncomingMissile)
                score *= 0.3f;

            // Ensure within [0,1]
            return Mathf.Clamp01(score);
        }

#if UNITY_EDITOR
        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            if (ctx?.SelfTransform == null) return;

            Vector3 selfPos = ctx.SelfTransform.position;
            Vector3 tgtPos  = GamePlane.PlaneToWorld(currentTarget);

            // Draw path line
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(selfPos, tgtPos);
            Gizmos.DrawWireSphere(tgtPos, 1.2f);

            // Draw flee + jink vectors
            if (ctx.Enemy != null)
            {
                Vector3 enemyPos = GamePlane.PlaneToWorld(ctx.EnemyPos);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos);
            }

            // Label
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, $"JINK EVADE\nNext flip in {(nextJinkTime - Time.time):F1}s");
        }
#endif
    }
} 