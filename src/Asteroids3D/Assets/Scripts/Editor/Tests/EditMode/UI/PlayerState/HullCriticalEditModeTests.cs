using NUnit.Framework;
using UI.PlayerState.Alerts;

namespace Tests.EditMode.UI.PlayerState
{
    [Category("UI")]
    public sealed class HullCriticalEditModeTests
    {
        [TestCase(.25f, .1f, true)]
        [TestCase(.01f, 0f, true)]
        [TestCase(.2501f, 0f, false)]
        [TestCase(.2f, .1001f, false)]
        [TestCase(0f, 0f, false)]
        [TestCase(1f, 1f, false)]
        public void Holds_OnlyWithHullAtOrUnderAQuarterAndShieldAtOrUnderATenth(float hull, float shield, bool holds) =>
            Assert.AreEqual(holds, HullCritical.Holds(hull, shield));
    }
}
