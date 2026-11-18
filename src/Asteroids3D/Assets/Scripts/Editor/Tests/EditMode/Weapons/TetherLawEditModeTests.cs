using Combat.Tethers;
using NUnit.Framework;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class TetherLawEditModeTests
    {
        [Test]
        public void SlackRope_HasNoTension_EvenWhileSeparating()
        {
            Assert.AreEqual(0f, TetherLaw.Tension(5f, 5f, 10f, 20000f, 2000f));
            Assert.AreEqual(0f, TetherLaw.Tension(4f, 5f, 10f, 20000f, 2000f));
        }

        [Test]
        public void TautRope_IsStiffnessTimesStretch_PlusDampingTimesSeparation()
        {
            Assert.AreEqual(20000f * 2f + 2000f * 3f, TetherLaw.Tension(7f, 5f, 3f, 20000f, 2000f), 1e-4f);
        }

        [Test]
        public void Tension_NeverGoesNegative()
        {
            Assert.AreEqual(0f, TetherLaw.Tension(6f, 5f, -100f, 20000f, 2000f));
        }

        [Test]
        public void Reel_ShrinksByRateTimesDt_StopsAtMinLength_AndNeverLengthens()
        {
            Assert.AreEqual(8f, TetherLaw.Reel(10f, 6f, 4f, 1f / 3f), 1e-4f);
            Assert.AreEqual(4f, TetherLaw.Reel(5f, 6f, 4f, 1f));
            Assert.AreEqual(4f, TetherLaw.Reel(4f, 6f, 4f, 1f));
            Assert.AreEqual(3f, TetherLaw.Reel(3f, 6f, 4f, 1f));
        }
    }
}
