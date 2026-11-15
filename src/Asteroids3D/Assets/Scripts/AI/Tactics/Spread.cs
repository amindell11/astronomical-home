using System;

namespace AI.Tactics
{
    [Serializable]
    public struct Spread
    {
        public float min;
        public float max;

        public Spread(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Draw(System.Random rng)
        {
            var unit = (float)rng.NextDouble();
            return min == max ? min : min + unit * (max - min);
        }
    }
}
