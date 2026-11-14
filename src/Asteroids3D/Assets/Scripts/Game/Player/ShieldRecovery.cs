using Ships.Damage;

namespace Game.Player
{
    /// <summary>
    /// Measures the player's shield regeneration over one open sector for its run-record row.
    /// It only samples: it subscribes to nothing and changes no combat state.
    /// </summary>
    public sealed class ShieldRecovery
    {
        public readonly struct Tally
        {
            public readonly float RegenSeconds;
            public readonly int RegenStarts;
            public readonly int ShieldRefills;
            public readonly float ShieldRestored;
            public readonly float ShieldDownSeconds;
            public readonly float ContestedRegenSeconds;
            public readonly float ContestedShieldRestored;

            public Tally(float regenSeconds, int regenStarts, int shieldRefills, float shieldRestored,
                float shieldDownSeconds, float contestedRegenSeconds, float contestedShieldRestored)
            {
                RegenSeconds = regenSeconds;
                RegenStarts = regenStarts;
                ShieldRefills = shieldRefills;
                ShieldRestored = shieldRestored;
                ShieldDownSeconds = shieldDownSeconds;
                ContestedRegenSeconds = contestedRegenSeconds;
                ContestedShieldRestored = contestedShieldRestored;
            }
        }

        private bool sampling;
        private bool haveSample;
        private bool lastRegenerating;
        private float lastShield;
        private float regenSeconds;
        private int regenStarts;
        private int shieldRefills;
        private float shieldRestored;
        private float shieldDownSeconds;
        private float contestedRegenSeconds;
        private float contestedShieldRestored;

        public void Open()
        {
            Clear();
            sampling = true;
        }

        public Tally Close()
        {
            sampling = false;
            return Snapshot();
        }

        public void Reset()
        {
            sampling = false;
            Clear();
        }

        internal void Sample(bool regenerating, float shield, float maxShield, bool contested, float deltaTime)
        {
            if (!sampling) return;

            var was = haveSample && lastRegenerating;
            var previous = haveSample ? lastShield : shield;

            if (regenerating)
            {
                regenSeconds += deltaTime;
                if (contested)
                    contestedRegenSeconds += deltaTime;
            }

            if (regenerating && !was)
                regenStarts++;

            var gain = shield - previous;
            // IsRegenerating is already false on the sample that reaches full.
            if (gain > 0f && (regenerating || was))
            {
                shieldRestored += gain;
                if (contested)
                    contestedShieldRestored += gain;
            }

            if (was && previous < maxShield && shield >= maxShield)
                shieldRefills++;

            if (shield <= 0f)
                shieldDownSeconds += deltaTime;

            lastRegenerating = regenerating;
            lastShield = shield;
            haveSample = true;
        }

        public void Tick(RegenResource shield, bool contested, float deltaTime) =>
            Sample(shield.IsRegenerating, shield.CurrentValue, shield.MaxValue, contested, deltaTime);

        private void Clear()
        {
            haveSample = false;
            regenSeconds = 0f;
            regenStarts = 0;
            shieldRefills = 0;
            shieldRestored = 0f;
            shieldDownSeconds = 0f;
            contestedRegenSeconds = 0f;
            contestedShieldRestored = 0f;
        }

        private Tally Snapshot() => new(regenSeconds, regenStarts, shieldRefills, shieldRestored,
            shieldDownSeconds, contestedRegenSeconds, contestedShieldRestored);
    }
}
