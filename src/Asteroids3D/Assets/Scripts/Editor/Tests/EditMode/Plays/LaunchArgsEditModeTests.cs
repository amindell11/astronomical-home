#if UNITY_EDITOR
using Game.Dev;
using NUnit.Framework;

namespace Tests.EditMode.Plays
{
    [Category("Bootstrap")]
    public class LaunchArgsEditModeTests
    {
        [Test]
        public void FirstFaction_ReadsTheValueAfterTheFlag()
        {
            Assert.AreEqual("shoal", LaunchArgs.FirstFaction(new[] { "game", "-firstFaction", "shoal", "-batchmode" }));
        }

        [Test]
        public void FirstFaction_IsNullWhenTheFlagIsAbsent()
        {
            Assert.IsNull(LaunchArgs.FirstFaction(new[] { "game", "-batchmode" }));
        }

        [Test]
        public void FirstFaction_IsNullWhenTheFlagIsLast()
        {
            Assert.IsNull(LaunchArgs.FirstFaction(new[] { "game", "-firstFaction" }));
        }
    }
}
#endif
