using UnityEngine;

namespace AI.Plays
{
    public readonly struct PlayOrder
    {
        public readonly Vector2 velocity;
        public readonly bool engages;

        public PlayOrder(Vector2 velocity, bool engages)
        {
            this.velocity = velocity;
            this.engages = engages;
        }
    }
}
