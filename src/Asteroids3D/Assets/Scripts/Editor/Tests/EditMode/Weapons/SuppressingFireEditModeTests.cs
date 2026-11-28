using Combat.Projectiles;
using NUnit.Framework;
using Ships.Status;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class SuppressingFireEditModeTests
    {
        private const float Hold = 0.4f;

        [Test]
        public void TheLastHitsInsideTheWindow_SuppressTheTarget()
        {
            var fire = new SuppressingFire(hits: 3, window: 0.5f, hold: Hold);
            var target = new StatusEffects();

            fire.Strike(target, 0f);
            fire.Strike(target, 0.2f);
            Assert.IsFalse(target.Has(StatusEffect.Suppressed), "two hits are not yet steady fire");

            fire.Strike(target, 0.4f);
            Assert.AreEqual(Hold, target.Remaining(StatusEffect.Suppressed), 1e-5f, "three inside half a second are");
        }

        [Test]
        public void TheWindowRolls_SoSparseHitsNeverSuppress_UntilTheyTighten()
        {
            var fire = new SuppressingFire(hits: 3, window: 0.5f, hold: Hold);
            var target = new StatusEffects();

            foreach (var at in new[] { 0f, 0.3f, 0.6f })
                fire.Strike(target, at);
            Assert.IsFalse(target.Has(StatusEffect.Suppressed), "0 to 0.6 spans more than the window");

            fire.Strike(target, 0.8f);
            Assert.IsTrue(target.Has(StatusEffect.Suppressed), "0.3 to 0.8 fits it");
        }

        [Test]
        public void HittingAnotherShip_StartsTheCountOver()
        {
            var fire = new SuppressingFire(hits: 3, window: 0.5f, hold: Hold);
            var first = new StatusEffects();
            var second = new StatusEffects();

            fire.Strike(first, 0f);
            fire.Strike(first, 0.1f);
            fire.Strike(second, 0.2f);
            fire.Strike(first, 0.3f);

            Assert.IsFalse(first.Has(StatusEffect.Suppressed));
            Assert.IsFalse(second.Has(StatusEffect.Suppressed));
        }
    }
}
