using Utils;

namespace AI.Tactics
{
    /// <summary>One ship's own hurt numbers, drawn once from its temper.</summary>
    public readonly struct WoundedDraw
    {
        public readonly float below;
        public readonly float shieldBack;
        public readonly float seconds;
        public readonly int retreats;

        public WoundedDraw(float below, float shieldBack, float seconds, int retreats)
        {
            this.below = below;
            this.shieldBack = shieldBack;
            this.seconds = seconds;
            this.retreats = retreats;
        }

        public static WoundedDraw From(TacticsSettings settings, SeedScope seed)
        {
            var rng = new System.Random(seed.ToSeed());
            return new WoundedDraw(
                settings.woundedBelow.Draw(rng),
                settings.shieldBack.Draw(rng),
                settings.woundedSeconds.Draw(rng),
                rng.Next(settings.retreatsMin, settings.retreatsMax + 1));
        }
    }
}
