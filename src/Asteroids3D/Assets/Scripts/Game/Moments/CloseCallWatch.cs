using System;
using Ships.Damage;
using UI.PlayerState;

namespace Game.Moments
{
    /// <summary>
    /// Raises a <see cref="CloseCall"/> when the hull drops to the HUD's low threshold while the ship lives, then
    /// stays quiet until the hull climbs back above it, so one dip is one moment. It arms from the hull it is
    /// handed: a sector entered already low says nothing until the hull recovers and dips again.
    /// </summary>
    public sealed class CloseCallWatch : IDisposable
    {
        private readonly Resource hull;
        private readonly RunMomentStream moments;
        private readonly int sector;
        private bool armed;

        public CloseCallWatch(Resource hull, RunMomentStream moments, int sector)
        {
            this.hull = hull ?? throw new ArgumentNullException(nameof(hull));
            this.moments = moments ?? throw new ArgumentNullException(nameof(moments));
            this.sector = sector;
            armed = Above(hull.Pct);
            hull.OnValueChanged += OnHullChanged;
        }

        public void Dispose() => hull.OnValueChanged -= OnHullChanged;

        private static bool Above(float pct) => pct > StatusBarUI.LowThreshold;

        private void OnHullChanged(float current, float previous, float max)
        {
            var above = Above(hull.Pct);
            if (armed && !above && current > 0f)
                moments.Raise(new CloseCall(sector, hull.Pct));
            armed = above;
        }
    }
}
