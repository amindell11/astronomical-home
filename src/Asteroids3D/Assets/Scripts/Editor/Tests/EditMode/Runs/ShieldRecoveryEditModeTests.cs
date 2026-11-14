using Game.Player;
using NUnit.Framework;

namespace Tests.EditMode.Runs
{
    [Category("Bootstrap")]
    public class ShieldRecoveryEditModeTests
    {
        private ShieldRecovery recovery;

        [SetUp]
        public void SetUp() => recovery = new ShieldRecovery();

        [Test]
        public void InterruptedRegen_CountsSecondsAndEachStart()
        {
            recovery.Open();
            recovery.Sample(true, 10f, 100f, false, 1f);
            recovery.Sample(true, 20f, 100f, false, 0.5f);
            recovery.Sample(false, 20f, 100f, false, 3f);
            recovery.Sample(true, 24f, 100f, false, 0.25f);

            var tally = recovery.Close();
            Assert.AreEqual(1.75f, tally.RegenSeconds, 1e-4f);
            Assert.AreEqual(2, tally.RegenStarts);
            Assert.AreEqual(14f, tally.ShieldRestored, 1e-4f, "the first sample's previous shield is itself");
        }

        [Test]
        public void ReachingFull_CountsOneRefill_AndTheGainOnThatSample()
        {
            recovery.Open();
            recovery.Sample(true, 40f, 100f, false, 1f);
            recovery.Sample(true, 80f, 100f, false, 1f);
            recovery.Sample(false, 100f, 100f, false, 1f);
            recovery.Sample(false, 100f, 100f, false, 1f);

            var tally = recovery.Close();
            Assert.AreEqual(1, tally.ShieldRefills);
            Assert.AreEqual(60f, tally.ShieldRestored, 1e-4f, "the sample that reaches full still restores");
            Assert.AreEqual(2f, tally.RegenSeconds, 1e-4f, "regeneration has already stopped on that sample");
        }

        [Test]
        public void Restored_CountsOnlyRegenGains()
        {
            recovery.Open();
            recovery.Sample(false, 10f, 100f, false, 1f);
            recovery.Sample(false, 40f, 100f, false, 1f);
            recovery.Sample(true, 40f, 100f, false, 1f);
            recovery.Sample(true, 55f, 100f, false, 1f);
            recovery.Sample(false, 55f, 100f, false, 1f);
            recovery.Sample(false, 80f, 100f, false, 1f);

            Assert.AreEqual(15f, recovery.Close().ShieldRestored, 1e-4f);
        }

        [Test]
        public void ShieldAtZero_CountsDownSeconds()
        {
            recovery.Open();
            recovery.Sample(false, 5f, 100f, false, 1f);
            recovery.Sample(false, 0f, 100f, false, 1.5f);
            recovery.Sample(true, 0f, 100f, false, 0.5f);
            recovery.Sample(true, 2f, 100f, false, 1f);

            Assert.AreEqual(2f, recovery.Close().ShieldDownSeconds, 1e-4f);
        }

        [Test]
        public void Contested_SplitsRegenSecondsAndRestored()
        {
            recovery.Open();
            recovery.Sample(true, 10f, 100f, true, 1f);
            recovery.Sample(true, 30f, 100f, true, 1f);
            recovery.Sample(true, 45f, 100f, false, 2f);
            recovery.Sample(false, 45f, 100f, true, 1f);
            recovery.Sample(false, 70f, 100f, true, 1f);

            var tally = recovery.Close();
            Assert.AreEqual(4f, tally.RegenSeconds, 1e-4f);
            Assert.AreEqual(2f, tally.ContestedRegenSeconds, 1e-4f);
            Assert.AreEqual(35f, tally.ShieldRestored, 1e-4f);
            Assert.AreEqual(20f, tally.ContestedShieldRestored, 1e-4f);
        }

        [Test]
        public void SamplesOutsideOpenAndClose_AreIgnored()
        {
            recovery.Sample(true, 0f, 100f, true, 5f);
            Assert.AreEqual(0f, recovery.Close().RegenSeconds, 1e-4f);

            recovery.Open();
            recovery.Sample(true, 10f, 100f, false, 1f);
            recovery.Close();
            recovery.Sample(true, 40f, 100f, true, 4f);

            var tally = recovery.Close();
            Assert.AreEqual(1f, tally.RegenSeconds, 1e-4f);
            Assert.AreEqual(0f, tally.ContestedRegenSeconds, 1e-4f);
            Assert.AreEqual(1, tally.RegenStarts);
            Assert.AreEqual(0f, tally.ShieldRestored, 1e-4f);
        }
    }
}
