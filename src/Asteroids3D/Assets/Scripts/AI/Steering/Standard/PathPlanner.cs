using System.Collections.Generic;
using Game;
using Ships.Movement;
using UnityEngine;

namespace AI.Steering.Standard
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
            public readonly int   obstacleCount;
            public readonly IReadOnlyList<Collider> nearbyAsteroids;
            public readonly Dynamics tuning;

            public Input(Kinematics k, Vector2 g, Vector2 wpVel, float avoid, float arrive, 
                float max, float lookAhead, float margin, IReadOnlyList<Collider> rocks, int count, Dynamics t)
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
                obstacleCount = count;
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
            var desired = ComputeSeekVelocity(io.goal, io.kin.Pos, io.arriveRadius, io.maxSpeed, io.tuning.forwardAcc);
            var avoid = ComputeAvoidanceForce(io, out var future, out var collidingFutures);
            var desiredVel = desired + avoid + io.waypointVel;
            
            if (desiredVel.sqrMagnitude > io.maxSpeed * io.maxSpeed)
                desiredVel = desiredVel.normalized * io.maxSpeed;
            
            var accel = desiredVel - io.kin.Vel;
            
            var dbg = new DebugInfo(future, desired, avoid, accel, collidingFutures);
            return new Output(desiredVel, accel, dbg);
        }

        private static Vector2 ComputeSeekVelocity(Vector2 goal, Vector2 pos, float arriveRadius, float maxSpeed, float deceleration)
        {
            var toGoal = goal - pos;
            var dist = toGoal.magnitude;
            
            if (dist < arriveRadius)
                return Vector2.zero;
            
            var slowdownDist = StoppingDistance(maxSpeed, deceleration);
            var speed = (dist > slowdownDist) 
                ? maxSpeed 
                : MaxSpeedForStoppingDistance(dist, deceleration);
            
            return toGoal.normalized * speed;
        }

        private static float StoppingDistance(float speed, float deceleration)
        {
            return (speed * speed) / (2f * deceleration);
        }

        private static float MaxSpeedForStoppingDistance(float distance, float deceleration)
        {
            return Mathf.Sqrt(2f * deceleration * distance);
        }

        private static Vector2 ComputeAvoidanceForce(Input io, out Vector2 future, out List<Vector2> collidingFutures)
        {
            future = io.kin.Pos + io.kin.Vel * io.lookAheadTime;
            
            var push = Vector2.zero;
            var weight = 0f;
            
#if UNITY_EDITOR
            collidingFutures = null;
#else
            collidingFutures = new List<Vector2>();
#endif

            var segStart = io.kin.Pos;
            var segEnd = future;
            var segDir = segEnd - segStart;
            var segLenSq = segDir.sqrMagnitude;

            for (var i = 0; i < io.obstacleCount; i++)
            {
                var rock = io.nearbyAsteroids[i];
                if (rock == null) continue;
                
                var rockFut = PredictRockPosition(rock, io.lookAheadTime);
                var rockRad = rock.bounds.extents.x;
                var combined = io.avoidRadius + rockRad + io.safeMargin;

                var closest = ClosestPointOnSegment(segStart, segDir, segLenSq, rockFut);
                var sep = closest - rockFut;
                var sq = sep.sqrMagnitude;

                if (!(sq < combined * combined)) continue;
                
                var w = 1f / Mathf.Max(sq, 0.01f);
                push += sep.normalized * w;
                weight += w;

#if UNITY_EDITOR
                collidingFutures ??= new List<Vector2>();
                collidingFutures.Add(rockFut);
#endif
            }

            return weight > 0f ? push / weight * io.maxSpeed : Vector2.zero;
        }

        private static Vector2 PredictRockPosition(Collider rock, float lookAheadTime)
        {
            if (rock == null) return Vector2.zero;
            var rockPos = GamePlane.WorldPointToPlane(rock.transform.position);
            var rockVel = rock.attachedRigidbody 
                ? GamePlane.WorldDirToPlane(rock.attachedRigidbody.linearVelocity) 
                : Vector2.zero;
            return rockPos + rockVel * lookAheadTime;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 segStart, Vector2 segDir, float segLenSq, Vector2 point)
        {
            var t = 0f;
            if (!(segLenSq > 0.0001f)) return segStart + segDir * t;
            var offset = point - segStart;
            t = Mathf.Clamp(Vector2.Dot(offset, segDir) / segLenSq, 0f, 1f);
            return segStart + segDir * t;
        }
    }
} 