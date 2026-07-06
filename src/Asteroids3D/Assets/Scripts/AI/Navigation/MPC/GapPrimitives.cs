using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC
{
    public struct GapCandidate
    {
        public float2 axis;
        public float width;
        public float score;
        public int bankOnly;
    }

    public static class GapDetector
    {
        private const float SafetyMargin = 0.1f;

        public static bool TryFindBestGap(float2 shipPos, float2 goalPos,
            NativeArray<ObstacleData> obstacles, int obstacleCount,
            float shipRadius, float maxBankAngleRad, out GapCandidate best)
        {
            best = default;
            if (!obstacles.IsCreated || obstacleCount < 2 || shipRadius <= 0f)
                return false;

            var toGoal = goalPos - shipPos;
            var goalLenSq = math.lengthsq(toGoal);
            var goalDir = goalLenSq > 1e-6f ? toGoal * math.rsqrt(goalLenSq) : new float2(0f, 1f);
            var bankedDiameter = 2f * shipRadius * (maxBankAngleRad > 0f ? math.cos(maxBankAngleRad) : 1f);
            var unbankedDiameter = 2f * shipRadius;
            var found = false;

            for (var i = 0; i < obstacleCount; i++)
            {
                var a = obstacles[i];
                for (var j = i + 1; j < obstacleCount; j++)
                {
                    var b = obstacles[j];
                    var center = (a.position + b.position) * 0.5f;
                    var toCenter = center - shipPos;
                    var centerDistSq = math.lengthsq(toCenter);
                    if (centerDistSq < 1e-4f) continue;

                    var axis = toCenter * math.rsqrt(centerDistSq);
                    var alignment = math.dot(axis, goalDir);
                    if (alignment <= 0f) continue;

                    var width = math.distance(a.position, b.position) - a.radius - b.radius;
                    if (width < bankedDiameter + SafetyMargin) continue;

                    var bankOnly = width < unbankedDiameter + SafetyMargin ? 1 : 0;
                    var clearanceScore = math.saturate(width / math.max(unbankedDiameter, 0.01f));
                    var distScore = 1f / (1f + math.sqrt(centerDistSq) * 0.02f);
                    var score = alignment * 2f + clearanceScore + distScore + bankOnly * 0.25f;

                    if (found && score <= best.score) continue;
                    best = new GapCandidate
                    {
                        axis = axis,
                        width = width,
                        score = score,
                        bankOnly = bankOnly
                    };
                    found = true;
                }
            }

            return found;
        }
    }
}
