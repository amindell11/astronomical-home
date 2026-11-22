using Damage;
using NUnit.Framework;

namespace Tests.EditMode.Damage
{
    [Category("Damage")]
    public class AppliedDamageEditModeTests
    {
        [Test]
        public void HitSize_QuarterOfMaxHullIsFull_AndAnEighthIsHalf()
        {
            Assert.AreEqual(1f, new AppliedDamage(default(DamageInfo), 0f, 25f).HitSize(100f), 0.001f);
            Assert.AreEqual(.5f, new AppliedDamage(default(DamageInfo), 0f, 12.5f).HitSize(100f), 0.001f);
        }

        [Test]
        public void HitSize_CountsShieldAtHalf()
        {
            Assert.AreEqual(.5f, new AppliedDamage(default(DamageInfo), 25f, 0f).HitSize(100f), 0.001f);
        }

        [Test]
        public void HitSize_ClampsAboveFull_AndZeroDamageIsZero()
        {
            Assert.AreEqual(1f, new AppliedDamage(default(DamageInfo), 0f, 50f).HitSize(100f), 0.001f);
            Assert.AreEqual(0f, new AppliedDamage(default(DamageInfo), 0f, 0f).HitSize(100f), 0.001f);
        }
    }
}
