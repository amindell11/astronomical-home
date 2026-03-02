using AI.Context;
using UnityEngine;

namespace Movement
{
    public class Maneuvers
    {
        private readonly ShipInfo shipInfo;

        public Maneuvers(ShipInfo shipInfo)
        {
            this.shipInfo = shipInfo;
        }

        public Vector2 ComputeOrbitPoint(Vector2 center, bool clockwise, float radius, float leadTime = 0.4f)
        {
            var selfPos = shipInfo.Pos;
            var selfVel = shipInfo.Vel;

            var radiusVector = selfPos - center;
            var currentDistance = radiusVector.magnitude;

            if (currentDistance < 0.1f)
                return center + Vector2.up * radius;

            var radiusDir = radiusVector / currentDistance;
            var tangent = clockwise
                ? new Vector2(radiusDir.y, -radiusDir.x)
                : new Vector2(-radiusDir.y, radiusDir.x);

            var idealPos = center + radiusDir * radius;
            var leadOffset = tangent * (selfVel.magnitude * leadTime);

            var radiusCorrection = Vector2.zero;
            if (!(Mathf.Abs(currentDistance - radius) > 1f)) return idealPos + leadOffset + radiusCorrection;
            var radiusError = radius - currentDistance;
            radiusCorrection = radiusDir * (radiusError * 0.5f);

            return idealPos + leadOffset + radiusCorrection;
        }

        public Vector2 ComputeEvadePoint(Vector2 threatPos, float fleeDistance)
        {
            var selfPos = shipInfo.Pos;
            var fleeDirection = (selfPos - threatPos).normalized;
            if (fleeDirection.sqrMagnitude < 0.01f)
                fleeDirection = Random.insideUnitCircle.normalized;
            return selfPos + fleeDirection * fleeDistance;
        }

        public Vector2 ComputeKitePoint(Vector2 enemyPos, Vector2 enemyVel, float desiredDistance)
        {
            var selfPos = shipInfo.Pos;
            var dirAway = (selfPos - enemyPos).normalized;
            var dirOppVel = enemyVel.sqrMagnitude > 0.01f ? (-enemyVel).normalized : Vector2.zero;
            var retreatDir = (dirAway + dirOppVel).normalized;

            if (retreatDir.sqrMagnitude < 0.01f)
                retreatDir = dirAway;

            return selfPos + retreatDir * desiredDistance;
        }

        public Vector2 ComputeJinkPoint(Vector2 baseFleeDir, float jinkDistance, bool jinkRight)
        {
            var selfPos = shipInfo.Pos;
            var perpendicular = jinkRight
                ? new Vector2(baseFleeDir.y, -baseFleeDir.x)
                : new Vector2(-baseFleeDir.y, baseFleeDir.x);

            var jinkDir = (baseFleeDir + perpendicular * 0.5f).normalized;
            return selfPos + jinkDir * jinkDistance;
        }
    }
}