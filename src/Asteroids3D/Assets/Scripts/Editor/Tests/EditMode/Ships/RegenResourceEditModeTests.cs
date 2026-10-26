using NUnit.Framework;
using Ships.Damage;

namespace Tests.EditMode.Ships
{
    [Category("Ships")]
    public sealed class RegenResourceEditModeTests
    {
        [Test]
        public void FullSignal_RequiresActualRegeneration_AndFiresOnce()
        {
            var resource = new RegenResource(100f, 10f, 1f);
            var completions = 0;
            resource.OnFullyRegenerated += () => completions++;
            resource.ApplyDamage(40f);
            resource.Update(0.5f);
            Assert.That(resource.IsRegenerating, Is.False);
            Assert.That(resource.CurrentValue, Is.EqualTo(60f));
            resource.Update(0.6f);
            Assert.That(resource.IsRegenerating, Is.True);
            Assert.That(completions, Is.Zero);
            resource.Update(4f);
            Assert.That(resource.CurrentValue, Is.EqualTo(100f));
            Assert.That(resource.IsRegenerating, Is.False);
            resource.Update(10f);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void ResetRestoreAndConfigure_DoNotAnnounceRegenerationCompletion()
        {
            var resource = new RegenResource(100f, 10f, 1f);
            var completions = 0;
            resource.OnFullyRegenerated += () => completions++;
            resource.ApplyDamage(40f);
            resource.Reset();
            resource.ApplyDamage(40f);
            resource.RestoreFraction(1f);
            resource.ApplyDamage(40f);
            resource.Configure(150f, 10f, 1f);
            Assert.That(completions, Is.Zero);
            Assert.That(resource.IsRegenerating, Is.False);
        }

        [Test]
        public void AbsorbedHit_InterruptsRegenerationUntilTheDelayElapses()
        {
            var resource = new RegenResource(100f, 10f, 1f);
            resource.ApplyDamage(40f);
            resource.Update(1f);
            Assert.That(resource.IsRegenerating, Is.True);
            resource.ApplyDamage(10f);
            Assert.That(resource.IsRegenerating, Is.False);
            resource.Update(0.5f);
            Assert.That(resource.IsRegenerating, Is.False);
            resource.Update(0.5f);
            Assert.That(resource.IsRegenerating, Is.True);
        }
    }
}

