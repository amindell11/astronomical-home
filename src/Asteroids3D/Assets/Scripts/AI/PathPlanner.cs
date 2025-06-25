using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-D version of the original PathPlanner.  Only operates in the XY plane and
///  consumes the compact <see cref="ShipKinematics"/> record.  The algorithm is
///  the same seek / arrive / predictive-avoidance strategy, but expressed with
///  Vector2 math so it can be unit-tested and stays detached from Unity's
///  transforms and physics.
/// </summary>
public static class PathPlanner
{
    #region IO structs
    public readonly struct Input
    {
        public readonly ShipKinematics kin;
        public readonly Vector2 goal;            // waypoint in plane space
        public readonly float   arriveRadius;
        public readonly float   maxSpeed;
        public readonly float   avoidradius;
        public readonly float   lookAheadTime;
        public readonly float   safeMargin;
        public readonly IReadOnlyList<Collider> nearbyAsteroids;

        public Input(ShipKinematics k, Vector2 g, float avoid, float arrive, float max, float lookAhead,
                     float margin, IReadOnlyList<Collider> rocks)
        {
            kin   = k;
            goal  = g;
            avoidradius = avoid;
            arriveRadius = arrive;
            maxSpeed     = max;
            lookAheadTime= lookAhead;
            safeMargin   = margin;
            nearbyAsteroids = rocks;
        }
    }

    public readonly struct Output
    {
        public readonly Vector2 desiredVelocity;
        public readonly DebugInfo dbg;

        public Output(Vector2 dv, DebugInfo d)
        {
            desiredVelocity = dv;
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

    // Tunables (kept identical to original PathPlanner for behaviour parity)
    public static float ForwardAcceleration = 8f;
    public static float ReverseAcceleration = 4f;
    public static float StrafeAcceleration  = 6f;
    public static float VelocityDeadZone    = 0.1f;

    public static Output Compute(Input io)
    {
        /* -------- seek / arrive --------------------------------- */
        Vector2 toGoal = io.goal - io.kin.Pos;
        float   dist   = toGoal.magnitude;
        float   tgtSpeed = dist > io.arriveRadius
                           ? io.maxSpeed
                           : Mathf.Lerp(0, io.maxSpeed, dist / io.arriveRadius);
        Vector2 desired = toGoal.normalized * tgtSpeed;

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
            Vector2 rockPos = GamePlane.WorldToPlane(rp3);
            Vector3 rv3 = rock.attachedRigidbody ? rock.attachedRigidbody.linearVelocity : Vector3.zero;
            Vector2 rockVel = GamePlane.WorldToPlane(rv3);
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
        return new Output(desiredVel, dbg);
    }
} 