using Game.Bootstrap;
using NUnit.Framework;
using UI;

namespace Tests.EditMode
{
    /// <summary>
    /// The splash must cover exactly the non-interactive states: visible while the game boots,
    /// composes the session, or cycles a sector — hidden in the hangar and during live play.
    /// </summary>
    public class LoadingSplashEditModeTests
    {
        [TestCase(GameState.Loading, true)]
        [TestCase(GameState.Start, true)]
        [TestCase(GameState.LoadSector, true)]
        [TestCase(GameState.Restart, true)]
        [TestCase(GameState.Hangar, false)]
        [TestCase(GameState.InSector, false)]
        [TestCase(GameState.Exit, false)]
        public void Covers_MatchesNonInteractiveStates(GameState state, bool expected) =>
            Assert.AreEqual(expected, LoadingSplash.Covers(state));
    }
}
