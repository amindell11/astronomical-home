using System;

namespace Substrate.Services.Objectives
{
    /// <summary>A timed boost shown on the HUD, held by the module that granted it until Close().</summary>
    public sealed class SectorBoost
    {
        private readonly Action<SectorBoost> close;

        public string Label { get; }

        /// <summary>The <c>Time.time</c> the boost runs out at.</summary>
        public float EndsAt { get; }

        internal SectorBoost(string label, float endsAt, Action<SectorBoost> close)
        {
            Label = label;
            EndsAt = endsAt;
            this.close = close;
        }

        public void Close() => close(this);
    }
}
