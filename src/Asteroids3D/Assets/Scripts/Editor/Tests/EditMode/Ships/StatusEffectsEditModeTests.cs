using NUnit.Framework;
using Ships.Status;

namespace Tests.EditMode.Ships
{
    [Category("Ships")]
    public sealed class StatusEffectsEditModeTests
    {
        [Test]
        public void Stalled_BlocksMovementAndShieldRegen_LeavesGuns_AndWearsOff()
        {
            var status = new StatusEffects();
            Assert.That(status.CanRegenShield, Is.True);

            status.Apply(StatusEffect.Stalled, 1f);
            Assert.That(status.CanThrust, Is.False);
            Assert.That(status.CanTurn, Is.False);
            Assert.That(status.CanKeystone, Is.False);
            Assert.That(status.CanRegenShield, Is.False);
            Assert.That(status.CanFire, Is.True);

            status.Tick(1f);
            Assert.That(status.Has(StatusEffect.Stalled), Is.False);
            Assert.That(status.CanThrust && status.CanTurn && status.CanKeystone && status.CanRegenShield, Is.True);
        }

        [Test]
        public void ApplyDiminishing_ShortensARepeatInsideTheWindow_AndClearForgetsIt()
        {
            var status = new StatusEffects();
            const float seconds = 2f;
            const float window = 6f;
            const float scale = 0.5f;

            status.ApplyDiminishing(StatusEffect.Stalled, seconds, window, scale);
            Assert.That(status.Remaining(StatusEffect.Stalled), Is.EqualTo(seconds));

            status.Tick(seconds);
            status.ApplyDiminishing(StatusEffect.Stalled, seconds, window, scale);
            Assert.That(status.Remaining(StatusEffect.Stalled), Is.EqualTo(seconds * scale));

            status.Tick(seconds * scale + window);
            status.ApplyDiminishing(StatusEffect.Stalled, seconds, window, scale);
            Assert.That(status.Remaining(StatusEffect.Stalled), Is.EqualTo(seconds));

            status.Clear();
            status.ApplyDiminishing(StatusEffect.Stalled, seconds, window, scale);
            Assert.That(status.Remaining(StatusEffect.Stalled), Is.EqualTo(seconds));

            status.Clear();
            status.Apply(StatusEffect.Stalled, seconds);
            status.Tick(seconds);
            status.ApplyDiminishing(StatusEffect.Stalled, seconds, window, scale);
            Assert.That(status.Remaining(StatusEffect.Stalled), Is.EqualTo(seconds));
        }
    }
}
