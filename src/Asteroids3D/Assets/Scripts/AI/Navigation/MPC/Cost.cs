using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC
{
    public static partial class Cost
    {
        private const float HeadingGateDistance = 2f;
        private const float HeadingGateDistanceSq = HeadingGateDistance * HeadingGateDistance;
        private const float TwoPi = 2f * math.PI;

        /// <summary>
        /// Preprocessed per-step context shared by Evaluate and EvaluateBreakdown.
        /// Keeps goal-mode logic (Flee, arrival, heading flip) in one place.
        /// </summary>
        internal struct EvalContext
        {
            public float2 goalTarget;   // positional/closing/heading goal (waypoint, or the enemy if anchored)
            public float2 enemyPos;     // tactical reference: predicted enemy position this step
            public float2 enemyVel;
            public float enemyYaw;
            public bool hasEnemy;
            public float wVel;
            public float wClosing;
            public float wYaw;
            public float2 headingGoal;
            public float facingTarget;

            internal static EvalContext Create(State s, CostInput input, Config cfg, int step)
            {
                var stepTime = step * cfg.dt;
                var hasEnemy = !math.isnan(input.enemyYaw);
                var anchored = cfg.goalMode.IsEnemyAnchored();

                // Predicted enemy this step: the pre-rolled trajectory if present, else linear
                // extrapolation from the snapshot. Drives the tactical costs, and the goal too
                // when it is enemy-anchored.
                var haveTrack = input.enemyStates.IsCreated && step < input.enemyStateCount;
                float2 enemyPos, enemyVel;
                float enemyYaw;
                if (haveTrack)
                {
                    var es = input.enemyStates[step];
                    enemyPos = es.pos;
                    enemyVel = es.vel;
                    enemyYaw = hasEnemy ? es.yaw : input.enemyYaw;
                }
                else
                {
                    enemyPos = input.enemyPos + input.enemyVel * stepTime;
                    enemyVel = input.enemyVel;
                    enemyYaw = hasEnemy ? input.enemyYaw + input.enemyYawRate * stepTime : input.enemyYaw;
                }

                // The navigation goal target is the enemy when the goal is anchored to it,
                // otherwise the (linearly extrapolated) absolute waypoint. This is the one
                // place "is the goal the enemy?" is resolved.
                var goalTarget = (anchored && hasEnemy)
                    ? enemyPos
                    : input.goalPos + input.goalVel * stepTime;

                var isFlee = cfg.goalMode == GoalMode.Flee;
                var wVel = cfg.wVel;
                var wYaw = cfg.wYaw;
                var distToGoalSq = math.lengthsq(goalTarget - s.pos);

                // Arrival stabilization (disabled in Flee — we start near the goal and want to accelerate away)
                if (!isFlee && distToGoalSq < cfg.arrivalDistanceSq)
                {
                    var distToGoal = math.sqrt(distToGoalSq);
                    var t = 1f - (distToGoal / cfg.arrivalDistance);
                    wVel = math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                    wYaw = math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
                }

                // Flee disables both velocity damping and the closing-velocity reward.
                if (isFlee) wVel = 0f;
                var wClosing = isFlee ? 0f : cfg.wClosing;

                var facingTarget = cfg.facingTarget;
                if (input.projectileSpeed > 0f && hasEnemy)
                    facingTarget = InterceptYaw(s.pos, enemyPos, enemyVel, input.projectileSpeed);

                float2 headingGoal;
                if (isFlee) headingGoal = 2f * s.pos - goalTarget;
                else headingGoal = goalTarget;

                return new EvalContext
                {
                    goalTarget = goalTarget,
                    enemyPos = enemyPos,
                    enemyVel = enemyVel,
                    enemyYaw = enemyYaw,
                    hasEnemy = hasEnemy,
                    wVel = wVel,
                    wClosing = wClosing,
                    wYaw = wYaw,
                    headingGoal = headingGoal,
                    facingTarget = facingTarget,
                };
            }
        }

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);
            var profileScale = BankProfileScale(u.strafe, cfg);

            ObstacleCosts(s, input, cfg, profileScale, out var collisionCost, out var obstacleCost);

            // The active objective. The position-goal family is ramped (terminal goal); the
            // velocity tracker is per-step and un-ramped, so the receding-horizon controller
            // tracks the near term instead of only matching v_ref at the horizon end.
            var velocityMode = cfg.goalMode == GoalMode.VelocityReference;
            var rampedObjective = velocityMode ? 0f : Objective(s, ctx, cfg);
            var trackCost = velocityMode
                ? VelocityTrackCost(s.vel, input.velocityReference, cfg.maxSpeedSq) * cfg.wVelTrack
                : 0f;

            // State cost — grows toward the horizon end via the terminal ramp. Objective (the
            // one active goal), Aim (intercept-facing, both identities), authored Tactical
            // (gated off in the velocity-tracker), and the state-shaping regularizers.
            var stateCost = rampedObjective
                + Aim(s, ctx, cfg)
                + (cfg.tacticalEnabled ? Tactical(s, ctx, input, cfg, profileScale) : 0f)
                + Regularizers(s, input, cfg, obstacleCost);

            var total = stateCost + ControlCost(u, prevU, cfg) + trackCost;

            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                var ramp = math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier;
                total += ramp * stateCost;
            }

            // Collision is a fixed, decisive penalty per colliding step — deliberately outside
            // the terminal ramp so an early hit is punished as hard as a late one.
            return total + collisionCost;
        }

        /// <summary>
        /// The objective — the "what to achieve", exactly one active, dispatched on goalMode.
        /// Ships the position family (waypoint / range-band / flee) today; a future velocity
        /// branch returns before the position bundle. Ramped.
        /// </summary>
        internal static float Objective(State s, in EvalContext ctx, in Config cfg)
        {
            var pos = PositionalGoalCost(s.pos, ctx, cfg) * cfg.wPos;
            var vel = VelocityCost(s.vel, cfg.maxSpeedSq) * ctx.wVel;
            var closing = ctx.wClosing == 0f ? 0f
                : ClosingCost(s.pos, s.vel, ctx.goalTarget, cfg.maxSpeedSq, cfg.closingFadeDistance) * ctx.wClosing;
            var heading = HeadingCost(s.pos, s.yaw, ctx.headingGoal, cfg.wYawDistanceScale) * ctx.wYaw;
            return pos + vel + closing + heading;
        }

        /// <summary>
        /// Intercept-facing geometry. Kept in both cost identities (aiming is not an authored
        /// tactic); only the <see cref="Tactical"/> block toggles off in the velocity-tracker.
        /// Ramped. Contributes 0 when no facing target is set (FacingCost handles NaN).
        /// </summary>
        internal static float Aim(State s, in EvalContext ctx, in Config cfg)
            => FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing;

        /// <summary>
        /// Authored combat tactics (LOS, exposure, tangential, miss-distance). Summed only when
        /// <see cref="Config.tacticalEnabled"/>; the velocity-tracker gates the whole block off,
        /// since the reward teaches those behaviors instead. Ramped. Requires a live enemy.
        /// </summary>
        internal static float Tactical(State s, in EvalContext ctx, in CostInput input, in Config cfg, float profileScale)
        {
            if (!ctx.hasEnemy) return 0f;

            var los = (cfg.wLos > 0f && input.obstacleCount > 0)
                ? LosCost(s.pos, ctx.enemyPos, input.obstacles, input.obstacleCount) * cfg.wLos : 0f;
            var exposure = cfg.wExposure > 0f
                ? ExposureCost(s.pos, ctx.enemyPos, ctx.enemyYaw, cfg.exposureWidth) * cfg.wExposure : 0f;
            var tangential = cfg.wTangential > 0f
                ? TangentialVelocityCost(s.pos, s.vel, ctx.enemyPos) * cfg.wTangential : 0f;
            var missDistance = (cfg.wMissDistance > 0f && input.projectileSpeed > 0f)
                ? MissDistanceCost(s.pos, s.vel, ctx.enemyPos, input.projectileSpeed, profileScale) * cfg.wMissDistance : 0f;

            return los + exposure + tangential + missDistance;
        }

        /// <summary>
        /// State-shaping regularizers that ride the terminal ramp with the objective (as in the
        /// legacy cost): obstacle turn-away, yaw-rate damping, momentum. Always on in both
        /// identities. Takes the already-resolved <paramref name="obstacleCost"/> from
        /// <see cref="ObstacleCosts"/> (collision and turn-away are mutually exclusive).
        /// </summary>
        internal static float Regularizers(State s, in CostInput input, in Config cfg, float obstacleCost)
        {
            var yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate;
            var momentum = cfg.wMomentum > 0f ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum : 0f;
            return obstacleCost + yawRate + momentum;
        }

        /// <summary>Per-step control effort — effort, boost, smoothness. Never ramped.</summary>
        internal static float ControlCost(Control u, Control prevU, in Config cfg)
            => EffortCost(u) * cfg.wEffort + u.boost * u.boost * cfg.wBoostEffort + SmoothnessCost(u, prevU, cfg);

        /// <summary>
        /// Bank profile: cos(strafe * maxBank) is the fraction of the ship's cross-section
        /// visible from any horizontal direction — banking rolls the collider, genuinely
        /// narrowing the in-plane hull. Drives obstacle clearance and the miss-distance profile.
        /// </summary>
        internal static float BankProfileScale(float strafe, in Config cfg)
            => cfg.maxBankAngleRad > 0f ? math.cos(math.abs(strafe) * cfg.maxBankAngleRad) : 1f;

        /// <summary>
        /// The hard collision penalty (fixed, un-ramped) and the collision-course-gated
        /// turn-away admissibility cost (ramped) are mutually exclusive per step: an overlapping
        /// hull pays the penalty and no turn-away; a clear hull pays only turn-away.
        /// </summary>
        internal static void ObstacleCosts(State s, in CostInput input, in Config cfg,
            float profileScale, out float collision, out float obstacle)
        {
            collision = 0f;
            obstacle = 0f;
            if (input.obstacleCount <= 0 || (cfg.collisionPenalty <= 0f && cfg.wObstacle <= 0f)) return;

            var hullRadius = cfg.shipRadius * profileScale + cfg.collisionSafetyMargin;
            if (Collides(s.pos, input.obstacles, input.obstacleCount, hullRadius))
                collision = cfg.collisionPenalty;
            else if (cfg.wObstacle > 0f)
                obstacle = TurnAwayCost(s.pos, s.vel, input.obstacles, input.obstacleCount,
                    hullRadius, cfg.maxLatAccel) * cfg.wObstacle;
        }

        /// <summary>
        /// Computes the yaw angle to aim at a first-order intercept point.
        /// Uses t = dist / projectileSpeed as time-of-flight estimate.
        /// </summary>
        internal static float InterceptYaw(float2 shipPos, float2 targetPos, float2 targetVel, float projectileSpeed)
        {
            var toTarget = targetPos - shipPos;
            var dist = math.length(toTarget);
            if (dist < 1e-4f) return math.atan2(-toTarget.x, toTarget.y);
            var tof = dist / projectileSpeed;
            var intercept = targetPos + targetVel * tof;
            var toIntercept = intercept - shipPos;
            return math.atan2(-toIntercept.x, toIntercept.y);
        }

        /// <summary>
        /// Single resolution point for the position cost target. Shared by Evaluate and
        /// EvaluateBreakdown so goal-mode handling lives in exactly one place.
        /// </summary>
        internal static float PositionalGoalCost(float2 pos, in EvalContext ctx, in Config cfg)
            => GoalCost(pos, ctx.goalTarget, cfg);

        internal static float GoalCost(float2 pos, float2 goal, Config cfg)
        {
            switch (cfg.goalMode)
            {
                case GoalMode.MaintainRange:
                    return RangeBandCost(pos, goal, cfg.desiredRange, cfg.rangeTolerance, cfg.positionCurve);
                case GoalMode.Flee:
                    return FleeCost(pos, goal, cfg.positionCurve);
                default:
                    return PositionCost(pos, goal, cfg.positionCurve, cfg.positionSaturationDistance);
            }
        }

        /// <summary>
        /// Position cost normalized to [0, 1) via Lorentzian saturation: cost = raw / (raw + satMax),
        /// where raw = d^curve and satMax = satDistance^curve. Half-saturates at d = satDistance,
        /// asymptotes to 1 at far distance. Closing-velocity reward provides the long-range
        /// urgency that an unbounded quadratic used to. satDistance &lt;= 0 falls back to raw d^curve
        /// (unbounded; legacy behavior).
        /// </summary>
        internal static float PositionCost(float2 pos, float2 goal, float curve, float satDistance)
        {
            var raw = (curve == 2f)
                ? math.lengthsq(pos - goal)
                : math.pow(math.length(pos - goal), curve);
            if (satDistance <= 0f) return raw;
            var satMax = math.pow(satDistance, curve);
            return raw / (raw + satMax);
        }

        private const float FleeEpsilon = 1f;

        internal static float RangeBandCost(float2 pos, float2 goal, float desiredRange, float tolerance, float curve)
        {
            var dist = math.length(pos - goal);
            var inner = desiredRange - tolerance;
            var outer = desiredRange + tolerance;

            if (dist >= inner && dist <= outer) return 0f;

            if (dist > outer)
            {
                // Too far: Lorentzian-saturated urgency normalized to [0, 1).
                // Half-saturates at err = tolerance, asymptotes to 1.
                var err = dist - outer;
                var errSq = err * err;
                var tolSq = math.max(tolerance * tolerance, 1e-4f);
                return errSq / (errSq + tolSq);
            }

            // Too close: diminishing reward — closer is better for aiming but with soft floor
            // Returns negative (reward), approaching -1 as dist→0
            var t = dist / math.max(inner, 1e-4f); // 1 at inner edge, 0 at enemy
            return -(1f - t * t);
        }

        internal static float FleeCost(float2 pos, float2 goal, float curve)
        {
            var dist = math.length(pos - goal);
            return FleeEpsilon / math.pow(dist + FleeEpsilon, curve * 0.5f);
        }

        /// <summary>Normalized 0-1: 0 = stopped, 1 = at maxSpeed.</summary>
        internal static float VelocityCost(float2 vel, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel) / maxSpeedSq : 0f;

        /// <summary>
        /// Squared velocity-tracking error, normalized by maxSpeed² (same idiom as VelocityCost).
        /// 0 when the ship's planar velocity equals the commanded reference; grows with the error.
        /// World-plane throughout — no frame conversion, since State.vel and the reference share it.
        /// </summary>
        internal static float VelocityTrackCost(float2 vel, float2 velocityReference, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel - velocityReference) / maxSpeedSq : 0f;

        /// <summary>
        /// Negative-when-closing reward for velocity component aimed at goal. Provides a
        /// Lyapunov-style gradient so "spin slightly slower with slight thrust" beats
        /// "spin at full rate" — escape path from sample-based MPC's spinning local optima.
        /// Returns ~ [-1, 1]: -1 = closing at maxSpeed, +1 = receding at maxSpeed.
        /// Smoothstep-gated to 0 within fadeDistance so velocity-damping arrival can take over.
        /// </summary>
        internal static float ClosingCost(float2 pos, float2 vel, float2 goal,
            float maxSpeedSq, float fadeDistance)
        {
            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f || maxSpeedSq <= 0f) return 0f;

            var dist = math.sqrt(distSq);
            var closingSpeed = math.dot(vel, toGoal / dist);
            var raw = -closingSpeed / math.sqrt(maxSpeedSq);

            if (fadeDistance <= 0f) return raw;
            var gate = Smoothstep01(dist / fadeDistance);
            return raw * gate;
        }

        /// <summary>Normalized 0-1 (scaled by 1 + distanceScale*dist): 0 = pointing at goal, large when misaligned at range.</summary>
        internal static float HeadingCost(float2 pos, float yaw, float2 goal, float distanceScale)
        {
            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f) return 0f;

            var goalYaw = math.atan2(-toGoal.x, toGoal.y);
            var angErr = WrapRadians(yaw - goalYaw);
            var cost = (angErr * angErr) / (math.PI * math.PI);

            // Scale by (1 + scale * dist) so heading stays visible vs position cost (≈ dist²) at range.
            // Matches ∂PositionCost/∂heading for positionCurve=2; scale=0 disables and recovers normalized 0-1.
            var dist = math.sqrt(distSq);
            var distMultiplier = 1f + distanceScale * dist;

            if (distSq >= HeadingGateDistanceSq) return cost * distMultiplier;

            var normalizedDist = dist / HeadingGateDistance;
            return cost * Smoothstep01(normalizedDist) * distMultiplier;
        }

        /// <summary>Normalized 0-1: 0 = on target, 1 = worst possible facing (π error).</summary>
        internal static float FacingCost(float yaw, float targetYaw, float width = 1f)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = math.abs(WrapRadians(yaw - targetYaw));
            var raw = err < width ? err * err : 2f * width * err - width * width;
            var maxRaw = 2f * width * math.PI - width * width;
            return raw / math.max(maxRaw, 1e-4f);
        }

        /// <summary>Normalized 0-1: 0 = no spin, 1 = at maxYawRate.</summary>
        internal static float YawRateCost(float yawRate, float maxYawRateSq) =>
            maxYawRateSq > 0f ? (yawRate * yawRate) / maxYawRateSq : 0f;

        /// <summary>Normalized 0-1: 0 = no input, 1 = all controls maxed.</summary>
        internal static float EffortCost(Control u) =>
            (u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque) / 3f;

        /// <summary>Normalized 0-1 per axis: 0 = no change, 1 = full reversal in one step.</summary>
        internal static float SmoothnessCost(Control u, Control prev, Config cfg)
        {
            // Max delta is 2 (-1 to +1), max rate = 2*invDt, max rate² = 4*invDt²
            var normFactor = 0.25f * cfg.dt * cfg.dt; // 1 / (4 * invDt²)
            var duT = u.thrust - prev.thrust;
            var duS = u.strafe - prev.strafe;
            var duY = u.yawTorque - prev.yawTorque;

            return (duT * duT * normFactor) * cfg.wSmoothnessThrust +
                   (duS * duS * normFactor) * cfg.wSmoothnessStrafe +
                   (duY * duY * normFactor) * cfg.wSmoothnessYaw;
        }

        /// <summary>
        /// Hard hull-overlap test: true if the (bank-narrowed, margin-inflated) ship disc
        /// overlaps any obstacle disc. Near-binary by design — rollouts that hit are rejected
        /// via a large fixed penalty; rollouts that miss are NOT punished for proximity, so
        /// close-and-tight flying stays free (trade study §3.4).
        /// </summary>
        internal static bool Collides(float2 pos,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count, float hullRadius)
        {
            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + hullRadius;
                if (math.lengthsq(obs.position - pos) < range * range)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Admissibility (collision-course-gated turn-away) cost: only obstacles the current
        /// velocity actually leads INTO incur cost. For each obstacle ahead, project its center
        /// onto the velocity axis; if the perpendicular offset already clears the corridor
        /// (obs.radius + hull) the ship passes clean → 0. Otherwise the cost measures whether
        /// the ship can still sidestep the remaining lateral deficit (dNeeded) with its lateral
        /// thrust before reaching the obstacle's plane (dTurn = ½·a_lat·t² over tAvail).
        /// Exactly 0 when the sidestep suffices; rises smoothly (C¹ at the boundary, bounded
        /// [0,1]) as it falls short. A weaving pursuer steers around off-course rocks for free
        /// and only pays for dead-ahead obstacles it leads into. Worst obstacle wins (max) —
        /// the binding constraint, never a sum. Chosen over the stopping-distance ratio after
        /// the A2 ablation showed braking-based cost causes chase timidity without preventing
        /// the failures it targets (see Chase_Nav_Track_A_Implementation_Log.md).
        /// </summary>
        internal static float TurnAwayCost(float2 pos, float2 vel,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count,
            float hullRadius, float maxLatAccel)
        {
            var speed = math.length(vel);
            if (speed <= 1e-3f) return 0f; // no heading — nothing is being led into

            var velDir = vel / speed;
            var halfLatAccel = 0.5f * math.max(maxLatAccel, 1e-4f);
            var worst = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var toObs = obs.position - pos;
                var corridor = obs.radius + hullRadius;

                var along = math.dot(toObs, velDir);
                if (along <= 0f) continue;                      // behind us — no cost

                var perp = math.length(toObs - along * velDir);
                if (perp >= corridor) continue;                 // collision-course gate: passes clear

                var dNeeded = corridor - perp;                  // extra lateral clearance to miss
                var tAvail = along / speed;                     // time until we reach its plane
                var dTurn = halfLatAccel * tAvail * tAvail;     // max sidestep before impact
                // 0 when the sidestep covers the deficit; →1 as it falls short. Squaring gives C¹ zero.
                var deficit = math.saturate(1f - dTurn / math.max(dNeeded, 1e-4f));
                worst = math.max(worst, deficit * deficit);
            }
            return worst;
        }

        /// <summary>
        /// Penalizes positions where obstacles block the line from ship to enemy.
        /// Uses closest-approach distance to detect line-circle intersection.
        /// Returns a soft occlusion score: 0 = clear LOS, positive = blocked.
        /// </summary>
        /// <summary>Normalized 0-1: 0 = clear LOS, 1 = fully blocked.</summary>
        public static float LosCost(float2 pos, float2 enemy,
            NativeArray<ObstacleData> obstacles, int count)
        {
            var ray = enemy - pos;
            var rayLenSq = math.lengthsq(ray);
            if (rayLenSq < 1e-8f) return 0f;

            var invRayLenSq = 1f / rayLenSq;
            var maxPenetration = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var rSq = obs.radius * obs.radius;

                // Skip the target itself: if the goal endpoint sits inside this obstacle's sphere,
                // the obstacle IS the target (or wraps it) — no self-blocking of LOS to the very thing we're aiming at.
                if (math.lengthsq(obs.position - enemy) <= rSq) continue;

                var toObs = obs.position - pos;
                var t = math.saturate(math.dot(toObs, ray) * invRayLenSq);
                var closest = pos + t * ray;
                var distSq = math.lengthsq(obs.position - closest);

                if (distSq >= rSq) continue;

                var penetration = 1f - math.sqrt(distSq) / obs.radius;
                maxPenetration = math.max(maxPenetration, penetration * penetration);
            }

            return maxPenetration;
        }

        /// <summary>
        /// Penalizes being in the enemy's forward weapon arc.
        /// Cost is highest when directly in front of the enemy at close range,
        /// zero when behind/beside. Inverse-distance scaling makes close-range
        /// exposure far more urgent than long-range.
        /// </summary>
        public static float ExposureCost(float2 pos, float2 enemyPos, float enemyYaw,
            float width = 1f)
        {
            var toShip = pos - enemyPos;
            var distSq = math.lengthsq(toShip);
            if (distSq < 1e-8f) return 1f; // On top of enemy = max exposure

            var dir = toShip * math.rsqrt(distSq);
            // Enemy forward vector (matching yaw convention: yaw=0 → +Y)
            var enemyFwd = new float2(-math.sin(enemyYaw), math.cos(enemyYaw));
            var cosAngle = math.dot(enemyFwd, dir);

            // Angle from enemy's nose (0 = directly in front, π = behind)
            var angle = math.acos(math.clamp(cosAngle, -1f, 1f));
            var x = angle / math.max(width, 1e-4f);
            return math.exp(-x * x);
        }

        /// <summary>
        /// Rewards lateral (tangential) velocity relative to the enemy.
        /// High tangential speed = low cost, making the ship harder to track.
        /// </summary>
        /// <summary>Normalized 0-1: 1 = no lateral movement, ~0 = fast lateral movement.</summary>
        internal static float TangentialVelocityCost(float2 pos, float2 vel, float2 enemyPos)
        {
            var toEnemy = enemyPos - pos;
            var dist = math.length(toEnemy);
            if (dist < 1e-4f) return 0f;
            var radialDir = toEnemy / dist;
            var tangentialSpeed = math.abs(vel.x * radialDir.y - vel.y * radialDir.x);
            return 0.5f / (tangentialSpeed + 0.5f);
        }

        /// <summary>
        /// Penalizes states where the ship is easy to hit.
        /// Computes miss distance: the perpendicular displacement of the ship
        /// from the enemy's line of fire during the projectile's time of flight.
        /// Banking (from strafe) reduces the ship's cross-section by cos(bankAngle),
        /// shrinking the effective profile the enemy must hit.
        /// Naturally captures speed, lateral movement, range, and bank in one term.
        /// </summary>
        internal static float MissDistanceCost(float2 pos, float2 vel, float2 enemyPos,
            float projectileSpeed, float profileScale)
        {
            var toShip = pos - enemyPos;
            var distSq = math.lengthsq(toShip);
            if (distSq < 1e-4f) return 1f; // On top of enemy = max danger

            var dist = math.sqrt(distSq);
            var tof = dist / projectileSpeed;

            // Perpendicular velocity component relative to the line of fire
            var radialDir = toShip / dist;
            var radialSpeed = math.dot(vel, radialDir);
            var tangentialSpeedSq = math.lengthsq(vel) - radialSpeed * radialSpeed;
            var missDistance = math.sqrt(math.max(0f, tangentialSpeedSq)) * tof;

            // Banking shrinks the ship's profile: effective width = baseWidth * cos(bankAngle)
            var effectiveProfile = 0.5f * profileScale;
            return effectiveProfile / (missDistance + effectiveProfile);
        }

        /// <summary>
        /// Penalizes velocity direction changes relative to the initial velocity.
        /// Returns 0 when maintaining course, up to 2 when reversing direction.
        /// Returns 0 when either velocity is near-zero (no meaningful direction).
        /// </summary>
        internal static float MomentumCost(float2 vel, float2 initialVel)
        {
            var speedSq = math.lengthsq(vel);
            var initSpeedSq = math.lengthsq(initialVel);
            if (speedSq < 1e-4f || initSpeedSq < 1e-4f) return 0f;

            var cosAngle = math.dot(vel, initialVel) / (math.sqrt(speedSq) * math.sqrt(initSpeedSq));
            return (1f - cosAngle) * 0.5f; // 0 = same direction, 0.5 = perpendicular, 1 = opposite
        }

        internal static float WrapRadians(float angle)
        {
            if (angle > math.PI) return angle - TwoPi;
            if (angle < -math.PI) return angle + TwoPi;
            return angle;
        }

        private static float Smoothstep01(float x)
        {
            x = math.clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
