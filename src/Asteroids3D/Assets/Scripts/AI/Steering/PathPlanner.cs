using System.Collections.Generic;
using AI.Steering;
using Game;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering
{
    public static class PathPlanner
    {
        #region IO structs
        public readonly struct Input
        {
            public readonly Kinematics kin;
            public readonly Vector2 goal;
            public readonly Vector2 waypointVel;     
            public readonly float   arriveRadius;
            public readonly float   maxSpeed;
            public readonly float   avoidRadius;
            public readonly float   lookAheadTime;
            public readonly float   safeMargin;
            public readonly IReadOnlyList<Collider> nearbyAsteroids;
            public readonly SteeringTuning tuning;

            public Input(Kinematics k, Vector2 g, Vector2 wpVel, float avoid, float arrive, float max, float lookAhead,
                float margin, IReadOnlyList<Collider> rocks, SteeringTuning t)
            {
                kin   = k;
                goal  = g;
                waypointVel = wpVel;
                avoidRadius = avoid;
                arriveRadius = arrive;
                maxSpeed     = max;
                lookAheadTime= lookAhead;
                safeMargin   = margin;
                nearbyAsteroids = rocks;
                tuning = t;
            }
        }

        public readonly struct Output
        {
            public readonly Vector2 desiredVelocity;
            public readonly Vector2 desiredAccel;
            public readonly DebugInfo dbg;

            public Output(Vector2 dv, Vector2 da, DebugInfo d)
            {
                desiredVelocity = dv;
                desiredAccel = da;
                dbg = d;
            }
        }

        public readonly struct DebugInfo
        {
            public readonly Vector2 future;
            public readonly Vector2 desired;
            public readonly Vector2 avoid;
            public readonly Vector2 accel;
            public readonly List<Vector2> rockFutures;

            public DebugInfo(Vector2 f, Vector2 d, Vector2 a, Vector2 ac, List<Vector2> rf)
            {
                future = f; desired = d; avoid = a; accel = ac; rockFutures = rf;
            }
        }
        #endregion

        public static Output Compute(Input io)
        {
            var toGoal = io.goal - io.kin.Pos;
            var dist = toGoal.magnitude;
            var dirToGoal = dist > 0.01f ? toGoal / dist : Vector2.zero;

            var maxRelativeSpeed = Mathf.Sqrt(2f * io.tuning.ForwardAcc * dist);
            var desiredRelSpeed = Mathf.Min(maxRelativeSpeed, io.maxSpeed);
        
            var desired = io.waypointVel + dirToGoal * desiredRelSpeed;
        
            if (desired.sqrMagnitude > io.maxSpeed * io.maxSpeed)
                desired = desired.normalized * io.maxSpeed;

            var future = io.kin.Pos + io.kin.Vel * io.lookAheadTime;
            var push   = Vector2.zero;
            var weight = 0f;
#if UNITY_EDITOR
            List<Vector2> collidingFutures = null;
#endif

            var segStart = io.kin.Pos;
            var segEnd   = future;
            var segDir   = segEnd - segStart;
            var segLenSq = segDir.sqrMagnitude;

            foreach (var rock in io.nearbyAsteroids)
            {
                var rp3 = rock.transform.position;
                var rockPos = GamePlane.WorldPointToPlane(rp3);
                var rv3 = rock.attachedRigidbody ? rock.attachedRigidbody.linearVelocity : Vector3.zero;
                var rockVel = GamePlane.WorldPointToPlane(rv3);
                var rockFut = rockPos + rockVel * io.lookAheadTime;

                var rockRad = rock.bounds.extents.x;
                var combined = io.avoidRadius + rockRad + io.safeMargin;

                var t = 0f;
                var offset = rockFut - segStart;
                if (segLenSq > 0.0001f)
                    t = Mathf.Clamp(Vector2.Dot(offset, segDir) / segLenSq, 0f, 1f);
                var closest = segStart + segDir * t;
                var sep     = closest - rockFut;
                var sq      = sep.sqrMagnitude;

                if (!(sq < combined * combined)) continue;
                var w = 1f / Mathf.Max(sq, 0.01f);
                push   += sep.normalized * w;
                weight += w;

#if UNITY_EDITOR
                collidingFutures ??= new List<Vector2>();
                collidingFutures.Add(rockFut);
#endif
            }

            var avoid = (weight > 0f) ? push / weight * io.maxSpeed : Vector2.zero;

            var desiredVel = desired + avoid;

            var accel = desiredVel - io.kin.Vel;

#if UNITY_EDITOR
            var dbg = new DebugInfo(future, desired, avoid, accel, collidingFutures ?? new List<Vector2>());
#else
        var dbg = new DebugInfo(future, desired, avoid, accel, new List<Vector2>());
#endif
            return new Output(desiredVel, accel, dbg);
        }
    }
} 