using System.Collections.Generic;
using Game;
using UnityEngine;
using ShipMain.Movement;

namespace EnemyAI
{
    /// <summary>
    /// 2-D version of the original PathPlanner.  Only operates in the XY plane and
    ///  consumes the compact <see cref="Kinematics"/> record.  The algorithm is
    ///  the same seek / arrive / predictive-avoidance strategy, but expressed with
    ///  Vector2 math so it can be unit-tested and stays detached from Unity's
    ///  transforms and physics.
    /// </summary>
    public static class PathPlanner
    {
        #region IO structs
        public readonly struct Input
        {
            public readonly Kinematics kin;
            public readonly Vector2 goal;            // waypoint in plane space
            public readonly Vector2 waypointVel;     // velocity of the waypoint
            public readonly float   arriveRadius;
            public readonly float   maxSpeed;
            public readonly float   avoidradius;
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
                avoidradius = avoid;
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
                dbg             = d;
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
            /* -------- seek / arrive --------------------------------- */
            Vector2 toGoal = io.goal - io.kin.Pos;
            float dist = toGoal.magnitude;
            Vector2 dirToGoal = dist > 0.01f ? toGoal / dist : Vector2.zero;

            // Using ForwardAcc from a default SteeringTuning to calculate maxRelativeSpeed
            float maxRelativeSpeed = Mathf.Sqrt(2f * io.tuning.ForwardAcc * dist);
            float desiredRelSpeed = Mathf.Min(maxRelativeSpeed, io.maxSpeed);
        
            Vector2 desired = io.waypointVel + dirToGoal * desiredRelSpeed;
        
            if (desired.sqrMagnitude > io.maxSpeed * io.maxSpeed)
                desired = desired.normalized * io.maxSpeed;

            /* -------- predictive avoidance --------------------------- */
            Vector2 future = io.kin.Pos + io.kin.Vel * io.lookAheadTime;
            Vector2 push   = Vector2.zero;
            float   weight = 0f;
#if UNITY_EDITOR
            List<Vector2> collidingFutures = null;
#endif

            Vector2 segStart = io.kin.Pos;
            Vector2 segEnd   = future;
            Vector2 segDir   = segEnd - segStart;
            float   segLenSq = segDir.sqrMagnitude;

            foreach (var rock in io.nearbyAsteroids)
            {
                Vector3 rp3 = rock.transform.position;
                Vector2 rockPos = GamePlane.WorldPointToPlane(rp3);
                Vector3 rv3 = rock.attachedRigidbody ? rock.attachedRigidbody.linearVelocity : Vector3.zero;
                Vector2 rockVel = GamePlane.WorldPointToPlane(rv3);
                Vector2 rockFut = rockPos + rockVel * io.lookAheadTime;

                float rockRad = rock.bounds.extents.x; // assumes roughly spherical
                float combined = io.avoidradius + rockRad + io.safeMargin;

                // closest point on ship segment to rockFut
                float t = 0f;
                Vector2 offset = rockFut - segStart;
                if (segLenSq > 0.0001f)
                    t = Mathf.Clamp(Vector2.Dot(offset, segDir) / segLenSq, 0f, 1f);
                Vector2 closest = segStart + segDir * t;
                Vector2 sep     = closest - rockFut;
                float   sq      = sep.sqrMagnitude;

                if (sq < combined * combined)
                {
                    float w = 1f / Mathf.Max(sq, 0.01f);
                    push   += sep.normalized * w;
                    weight += w;

#if UNITY_EDITOR
                    collidingFutures ??= new List<Vector2>();
                    collidingFutures.Add(rockFut);
#endif
                }
            }

            Vector2 avoid = (weight > 0f) ? push / weight * io.maxSpeed : Vector2.zero;

            /* -------- final desired velocity ------------------------- */
            Vector2 desiredVel = desired + avoid;

            Vector2 accel = desiredVel - io.kin.Vel;

#if UNITY_EDITOR
            var dbg = new DebugInfo(future, desired, avoid, accel, collidingFutures ?? new List<Vector2>());
#else
        var dbg = new DebugInfo(future, desired, avoid, accel, new List<Vector2>());
#endif
            return new Output(desiredVel, accel, dbg);
        }
    }
} 