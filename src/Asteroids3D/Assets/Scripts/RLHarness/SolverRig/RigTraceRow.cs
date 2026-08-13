using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.RLHarness
{
    /// <summary>One rig tick for offline plotting; solver command, plant state, and the per-term cost breakdown of the applied control at the tick the solve saw — the "which term trapped it" diagnostic.</summary>
    public struct RigTraceRow
    {
        public float t;
        public float posX;
        public float posY;
        public float velX;
        public float velY;
        public float yawDeg;
        public float yawRateDegPerSec;
        public float thrust;
        public float strafe;
        public float yawTorque;
        public float anchorYawDeg;
        public float facingErrorDeg;
        public float range;
        public int underThreat;
        public float solveCost;
        public int incumbentRank;
        public float incumbentCost;
        public float emitYawDeltaFromIncumbent;
        public float costVelocityTrack;
        public float costFacing;
        public float costFacingPrior;
        public float costPos;
        public float costYawRate;
        public float costObstacle;
        public float costCollision;
        public float costMomentum;
        public float costEffort;
        public float costSmoothness;
        public float costTotal;
    }

    public static class RigTraceCsv
    {
        private const string Header =
            "t,posX,posY,velX,velY,yawDeg,yawRateDegPerSec,thrust,strafe,yawTorque,anchorYawDeg,facingErrorDeg," +
            "range,underThreat,solveCost,incumbentRank,incumbentCost,emitYawDeltaFromIncumbent," +
            "costVelocityTrack,costFacing,costFacingPrior,costPos,costYawRate,costObstacle,costCollision," +
            "costMomentum,costEffort,costSmoothness,costTotal";

        public static void Write(string path, IReadOnlyList<RigTraceRow> rows)
        {
            var sb = new StringBuilder(rows.Count * 192);
            sb.AppendLine(Header);
            foreach (var r in rows)
                sb.AppendLine(string.Join(",",
                    F(r.t), F(r.posX), F(r.posY), F(r.velX), F(r.velY),
                    F(r.yawDeg), F(r.yawRateDegPerSec),
                    F(r.thrust), F(r.strafe), F(r.yawTorque),
                    F(r.anchorYawDeg), F(r.facingErrorDeg),
                    F(r.range), r.underThreat.ToString(CultureInfo.InvariantCulture), F(r.solveCost),
                    r.incumbentRank.ToString(CultureInfo.InvariantCulture), F(r.incumbentCost),
                    F(r.emitYawDeltaFromIncumbent),
                    F(r.costVelocityTrack), F(r.costFacing), F(r.costFacingPrior), F(r.costPos),
                    F(r.costYawRate), F(r.costObstacle), F(r.costCollision),
                    F(r.costMomentum), F(r.costEffort), F(r.costSmoothness), F(r.costTotal)));
            File.WriteAllText(path, sb.ToString());
        }

        private static string F(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
