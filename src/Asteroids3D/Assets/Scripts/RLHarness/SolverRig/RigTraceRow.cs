using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.RLHarness
{
    /// <summary>One rig tick for offline plotting; solver command and plant state at the tick the solve saw.</summary>
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
        public float solveCost;
        public int incumbentRank;
        public float incumbentCost;
        public float emitYawDeltaFromIncumbent;
    }

    public static class RigTraceCsv
    {
        private const string Header =
            "t,posX,posY,velX,velY,yawDeg,yawRateDegPerSec,thrust,strafe,yawTorque,anchorYawDeg,facingErrorDeg,solveCost," +
            "incumbentRank,incumbentCost,emitYawDeltaFromIncumbent";

        public static void Write(string path, IReadOnlyList<RigTraceRow> rows)
        {
            var sb = new StringBuilder(rows.Count * 96);
            sb.AppendLine(Header);
            foreach (var r in rows)
                sb.AppendLine(string.Join(",",
                    F(r.t), F(r.posX), F(r.posY), F(r.velX), F(r.velY),
                    F(r.yawDeg), F(r.yawRateDegPerSec),
                    F(r.thrust), F(r.strafe), F(r.yawTorque),
                    F(r.anchorYawDeg), F(r.facingErrorDeg), F(r.solveCost),
                    r.incumbentRank.ToString(CultureInfo.InvariantCulture), F(r.incumbentCost),
                    F(r.emitYawDeltaFromIncumbent)));
            File.WriteAllText(path, sb.ToString());
        }

        private static string F(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
