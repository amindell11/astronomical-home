using System.Collections;
using System.Linq;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Ships.Registry;
using Ships.Visuals;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Presentation
{
    [Category("Ships")]
    public sealed class HullShieldFeedbackPlayModeTests : PlayModeWorldFixture
    {
        private Ship ship;
        private HullVisuals hull;
        private Image ring;
        private Renderer[] surfaces;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ship = Factory.CreateShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"),
                null, 0, 0, projectiles: null, Vector3.zero, Quaternion.identity);
            ship.Damage.enabled = false;
            ship.Damage.Health.Configure(100f);
            ship.Damage.Shield.Configure(100f, 20f, 1f);
            hull = ship.GetComponentInChildren<HullVisuals>();
            ring = ship.GetComponentInChildren<ShieldUI>().GetComponent<Image>();
            surfaces = ship.GetComponentInChildren<ShipVisualRig>().Silhouette.ToArray();
        }

        [TearDown]
        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ShieldOnly_Bypass_AndMixedHits_UseTheirAppliedDamageChannels()
        {
            yield return null;
            ship.Damage.TakeDamage(Hit(20f));
            yield return new WaitForSeconds(0.08f);
            Assert.That(ring.enabled, Is.True);
            Assert.That(ring.fillAmount, Is.LessThan(0.3f), "An absorbed hit stays localized.");
            Assert.That(FlashStrength(), Is.Zero);

            ship.ResetShip();
            yield return null;
            ship.Damage.TakeDamage(Hit(20f, true));
            yield return new WaitForSeconds(0.08f);
            Assert.That(ring.enabled, Is.False, "Bypass damage must leave shields quiet.");
            Assert.That(FlashStrength(), Is.GreaterThan(0f));
            Assert.That(ship.Damage.Shield.CurrentValue, Is.EqualTo(100f));

            ship.ResetShip();
            yield return null;
            ship.Damage.TakeDamage(Hit(120f));
            yield return new WaitForSeconds(0.08f);
            Assert.That(ring.enabled, Is.True);
            var brokenFill = ring.fillAmount;
            Assert.That(brokenFill, Is.InRange(0f, 1f));
            yield return new WaitForSeconds(0.12f);
            Assert.That(ring.fillAmount, Is.LessThan(brokenFill), "The tear opens around the original ring.");
            Assert.That(FlashStrength(), Is.GreaterThan(0f));
            yield return new WaitForSeconds(0.8f);
            Assert.That(ring.enabled, Is.False, "Empty shields have no persistent mark.");
            Assert.That(FlashStrength(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RecoveryArc_ClosesOnlyAtFull_ThenDisappears()
        {
            yield return null;
            ship.Damage.TakeDamage(Hit(80f));
            yield return new WaitForSeconds(0.7f);
            Assert.That(ring.enabled, Is.False);
            ship.Damage.Shield.Update(1f);
            yield return null;
            yield return null;
            Assert.That(ring.enabled, Is.True);
            Assert.That(ring.fillAmount, Is.LessThan(0.3f), "Recovery uses a short orbiting arc.");
            var recoveryFill = ring.fillAmount;
            var recoveryRotation = ring.transform.rotation;
            yield return new WaitForSeconds(0.2f);
            Assert.That(Quaternion.Angle(ring.transform.rotation, recoveryRotation), Is.GreaterThan(10f));
            ship.Damage.Shield.Update(0.25f);
            yield return null;
            Assert.That(ring.fillAmount, Is.EqualTo(recoveryFill), "The arc must orbit rather than fill with capacity.");
            var ringSize = ring.rectTransform.sizeDelta;
            ship.Damage.TakeDamage(Hit(10f, true));
            yield return new WaitForSeconds(0.08f);
            Assert.That(ring.enabled, Is.True, "Bypass hits do not interrupt shield recovery.");

            ship.Damage.TakeDamage(Hit(10f));
            yield return new WaitForSeconds(0.7f);
            Assert.That(ring.enabled, Is.False, "Absorbed hits restart the recharge delay.");
            ship.Damage.Shield.Update(1f);
            yield return new WaitForSeconds(0.2f);
            var orbitEnd = ring.transform.eulerAngles.z + ring.fillAmount * 360f;
            ship.Damage.Shield.Update(4f);
            yield return null;
            yield return null;
            Assert.That(ring.fillAmount, Is.InRange(recoveryFill, recoveryFill + 0.1f),
                "Full recharge must extend the moving arc, not replace it with a circle.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(orbitEnd,
                ring.transform.eulerAngles.z + ring.fillAmount * 360f)), Is.LessThan(40f),
                "The closing tip continues from its orbit position.");
            var closingStart = ring.transform.rotation;
            var closingFill = ring.fillAmount;
            yield return new WaitForSeconds(0.25f);
            Assert.That(ring.fillAmount, Is.GreaterThan(closingFill).And.LessThan(1f));
            Assert.That(Quaternion.Angle(ring.transform.rotation, closingStart), Is.LessThan(0.01f),
                "The tail stays anchored while the tip completes the circle.");
            Assert.That(ring.material.GetFloat("_Highlight"), Is.Zero, "The flash waits for the closed circle.");
            yield return new WaitForSeconds(0.4f);
            Assert.That(ring.enabled, Is.True);
            Assert.That(ring.fillAmount, Is.EqualTo(1f), "Full capacity closes the circle.");
            Assert.That(ring.material.GetFloat("_Highlight"), Is.GreaterThan(0.2f));
            Assert.That(ring.rectTransform.sizeDelta, Is.EqualTo(ringSize));
            yield return new WaitForSeconds(0.5f);
            Assert.That(ring.enabled, Is.True, "The full circle holds long enough to read.");
            Assert.That(ring.color.a, Is.EqualTo(1f));
            Assert.That(ring.material.GetFloat("_Highlight"), Is.Zero);
            yield return new WaitForSeconds(0.9f);
            Assert.That(ring.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator Reset_ClearsFeedbackWithoutAnnouncingFullRecharge()
        {
            yield return null;
            ship.Damage.TakeDamage(Hit(120f));
            yield return new WaitForSeconds(0.08f);
            ship.ResetShip();
            yield return null;
            yield return null;
            Assert.That(ring.enabled, Is.False);
            Assert.That(FlashStrength(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RingSize_RemainsRelativeToHull_WhenBindingScaledShips()
        {
            yield return null;
            var originalSize = ring.rectTransform.sizeDelta;
            ship.transform.localScale *= 2f;
            ring.GetComponent<ShieldUI>().Bind(new ShipView(ship.transform, ship.Damage, null, null, true));
            Assert.That(Vector2.Distance(ring.rectTransform.sizeDelta, originalSize), Is.LessThan(0.001f),
                "The parent scales both hull and ring; binding must not apply that scale twice.");
        }

        [UnityTest]
        public IEnumerator HeldKeystoneColor_WinsOverDamage_AndReleaseRestoresEverySurface()
        {
            yield return null;
            var originals = surfaces.Select(ReadColor).ToArray();
            foreach (var color in new[] { Color.white, Color.cyan })
            {
                hull.HoldFlash(color);
                ship.Damage.TakeDamage(Hit(10f, true));
                yield return new WaitForSeconds(0.1f);
                foreach (var surface in surfaces) Assert.That(ReadColor(surface), Is.EqualTo(color));
                hull.ReleaseFlash();
                for (var i = 0; i < surfaces.Length; i++) Assert.That(ReadColor(surfaces[i]), Is.EqualTo(originals[i]));
            }
        }

        [UnityTest]
        public IEnumerator LowHull_PulsesOnlyTheFillColor_AndResetRestoresIt()
        {
            yield return null;
            var bars = ship.GetComponentsInChildren<StatusBarUI>();
            var health = bars.Single(b => b.Tracked == StatusBarUI.TrackedResource.Health).Fill;
            var shield = bars.Single(b => b.Tracked == StatusBarUI.TrackedResource.Shield).Fill;
            var healthColor = health.color;
            var shieldColor = shield.color;
            ship.Damage.TakeDamage(Hit(80f, true));
            yield return new WaitForSeconds(0.1f);
            var first = health.color;
            Assert.That(health.fillAmount, Is.EqualTo(0.2f).Within(0.001f));
            yield return new WaitForSeconds(0.6f);
            Assert.That(health.color.r, Is.GreaterThan(first.r + 0.1f));
            Assert.That(health.fillAmount, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(shield.color, Is.EqualTo(shieldColor));
            ship.ResetShip();
            yield return null;
            yield return null;
            Assert.That(health.color, Is.EqualTo(healthColor));
        }

        private static DamageInfo Hit(float amount, bool bypass = false) =>
            new(amount, DamageKind.Laser, ShipId.Invalid, 1f, Vector3.left, Vector3.right, bypass);

        private float FlashStrength()
        {
            var block = new MaterialPropertyBlock();
            surfaces[0].GetPropertyBlock(block);
            return block.GetFloat("_DamageFlash");
        }

        private static Color ReadColor(Renderer surface)
        {
            var block = new MaterialPropertyBlock();
            surface.GetPropertyBlock(block);
            return block.HasColor("_BaseColor") ? block.GetColor("_BaseColor") : surface.sharedMaterial.GetColor("_BaseColor");
        }
    }
}
