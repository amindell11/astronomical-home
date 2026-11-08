using System;

namespace Substrate.Services.Objectives
{
    /// <summary>A boost in force until the sector ends, shown on the HUD, held by the module that granted it until Close().</summary>
    public sealed class SectorBoost
    {
        private readonly Action<SectorBoost> close;

        public string Label { get; }

        internal SectorBoost(string label, Action<SectorBoost> close)
        {
            Label = label;
            this.close = close;
        }

        public void Close() => close(this);
    }
}
