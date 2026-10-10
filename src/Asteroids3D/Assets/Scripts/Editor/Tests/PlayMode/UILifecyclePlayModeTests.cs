using System.Collections;
using Combat.Targeting;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Damage;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UI.PlayerState;
using Ships.Registry;

namespace Tests.PlayMode
{
    [Category("UI")]
    public class UILifecyclePlayModeTests : PlayModeWorldFixture
    {
        private sealed class FakeDamageEvents : IDamageEvents
        {
            public event System.Action<Damage.DamageInfo> OnDamaged { add { } remove { } }
            public event System.Action<ShipId, Damage.DamageInfo> OnDeath { add { } remove { } }
            public Resource Health { get; } = new Resource(100f);
            public RegenResource Shield { get; } = new RegenResource(50f, 0f, 999f);
        }

        private sealed class StubStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable { get; set; }
            public float BoostCooldownRemaining { get; set; }
            public float BoostCooldownPct { get; set; }
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
        }

        private GameObject ownedRoot;

        public override void TearDown()
        {
            DestroyTestObject(ownedRoot);
            base.TearDown();
        }

        private GameObject CreateRoot(string name) => ownedRoot = new GameObject(name);

        /// <summary>
        /// Rig visuals are wired by injection (<see cref="IShipVisual.Bind"/>), not parent discovery:
        /// once a LockChannel is injected, the indicator responds to lock progress, and it re-subscribes
        /// across a disable/enable cycle (death/respawn).
        /// </summary>
        [UnityTest]
        public IEnumerator LockOnIndicator_Injected_RespondsToProgress_AndResubscribesOnReenable()
        {
            var channel = new LockChannel();

            // The indicator lives under a parent in the rig; LateUpdate reads transform.parent.
            var parent = CreateRoot("RigRoot");
            var indicatorGo = new GameObject("LockOnIndicator");
            indicatorGo.transform.SetParent(parent.transform, false);
            indicatorGo.AddComponent<CanvasGroup>();
            indicatorGo.AddComponent<Image>();
            var indicator = indicatorGo.AddComponent<LockOnIndicator>();
            var canvasGroup = indicatorGo.GetComponent<CanvasGroup>();

            yield return null; // Awake/Start (Hide)

            // Inject the lock channel exactly as the presentation installer does.
            indicator.Bind(new ShipView(indicatorGo.transform, null, null, channel, isPlayer: false));
            yield return null;

            channel.RaiseProgress(0.4f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f, "Indicator should show when receiving lock progress");

            channel.RaiseReleased();
            yield return null;
            Assert.AreEqual(0f, canvasGroup.alpha, 0.0001f, "Indicator should hide on release");

            indicatorGo.SetActive(false);
            yield return null;
            indicatorGo.SetActive(true);
            yield return null;

            channel.RaiseProgress(0.6f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f,
                "Indicator should resubscribe and show again after disable/enable");
        }

        private const string LockReticlePrefabPath = "Assets/Prefabs/UI/UILockOnIndicator.prefab";
        private static readonly int LockProgress = Animator.StringToHash("lockProgress");

        private CanvasGroup reticleGroup;
        private Animator reticleAnimator;

        // The authored prefab, so the break runs through the real reticle controller.
        private IEnumerator SpawnLockReticle(LockChannel channel)
        {
            var parent = CreateRoot("RigRoot");
            var reticle = Object.Instantiate(TestAssets.Load<LockOnIndicator>(LockReticlePrefabPath), parent.transform);
            reticleGroup = reticle.GetComponent<CanvasGroup>();
            reticleAnimator = reticle.GetComponent<Animator>();
            reticle.Bind(new ShipView(reticle.transform, null, null, channel, isPlayer: false));
            yield return null;
        }

        private bool ReticleShown => reticleGroup.alpha > 0f;
        private bool ReticleHeld => ReticleShown && Mathf.Approximately(reticleAnimator.GetFloat(LockProgress), 1f);
        private bool ReticleBreaking => reticleAnimator.GetCurrentAnimatorStateInfo(0).IsName("ReticleBreak");

        [UnityTest]
        public IEnumerator LockReticle_StaysUpThroughReleasesWhileTracking()
        {
            var channel = new LockChannel();
            yield return SpawnLockReticle(channel);

            channel.RaiseProgress(1f);
            channel.RaiseAcquired();
            yield return null;

            // Missiles.Fire: consuming the lock raises Released, then the missile adds its track.
            channel.RaiseReleased();
            channel.AddTrack();
            yield return null;
            Assert.IsTrue(ReticleHeld, "Reticle should hold the locked pose after a tracking launch");

            channel.RaiseProgress(0.3f);
            channel.RaiseReleased();
            Assert.IsTrue(ReticleHeld, "A missile lock cancelled mid-build should return the reticle to the held pose");

            channel.RaiseProgress(1f);
            channel.RaiseAcquired();
            channel.RaiseReleased();
            Assert.IsTrue(ReticleShown, "A second launch should never hide the reticle while a missile tracks");
            channel.AddTrack();
            yield return null;
            Assert.IsTrue(ReticleHeld);
        }

        [UnityTest]
        public IEnumerator LockReticle_LostTrack_PlaysBreakThenHides()
        {
            var channel = new LockChannel();
            yield return SpawnLockReticle(channel);
            channel.AddTrack();
            yield return null;

            channel.LoseTrack();
            yield return null;
            yield return null;
            Assert.IsTrue(ReticleBreaking, "A lost track should play the break");
            Assert.IsTrue(ReticleShown, "The reticle should stay up while the break plays");

            yield return AsyncAssert.WaitUntil(() => !ReticleBreaking, 2f, "The break never finished");
            Assert.IsFalse(ReticleShown, "With no missile tracking, the reticle should hide after the break");
        }

        [UnityTest]
        public IEnumerator LockReticle_OtherTrackEnd_ClearsWithoutBreak()
        {
            var channel = new LockChannel();
            yield return SpawnLockReticle(channel);
            channel.AddTrack();
            yield return null;

            channel.EndTrack();
            Assert.IsFalse(ReticleShown, "A track ending any other way should clear the reticle at once");
            yield return null;
            yield return null;
            Assert.IsFalse(ReticleBreaking, "Only a lost track should play the break");
        }

        [UnityTest]
        public IEnumerator LockReticle_TwoTracks_HoldsUntilLastEnds()
        {
            var channel = new LockChannel();
            yield return SpawnLockReticle(channel);
            channel.AddTrack();
            channel.AddTrack();
            yield return null;

            channel.LoseTrack();
            yield return null;
            yield return null;
            Assert.IsTrue(ReticleBreaking, "Precondition: the lost track plays the break");
            yield return AsyncAssert.WaitUntil(() => !ReticleBreaking, 2f, "The break never finished");
            Assert.IsTrue(ReticleHeld, "With a missile still tracking, the break should return to the held pose");

            channel.EndTrack();
            Assert.IsFalse(ReticleShown, "The reticle should clear when the last track ends");
        }

        [UnityTest]
        public IEnumerator LockReticle_HeldLockOutlastsTrackEnd()
        {
            var channel = new LockChannel();
            yield return SpawnLockReticle(channel);
            channel.AddTrack();
            channel.RaiseProgress(1f);
            channel.RaiseAcquired();
            yield return null;

            channel.EndTrack();
            Assert.IsTrue(ReticleShown, "A missile lock held on the ship should keep the reticle up after a track ends");

            channel.RaiseReleased();
            Assert.IsFalse(ReticleShown);
        }

        // Bars live under a parent in the rig; LateUpdate reads transform.parent.
        private static StatusBarUI CreateBar(Transform parent, StatusBarUI.TrackedResource tracked, out Image fill)
        {
            var root = new GameObject(tracked + "Bar");
            root.transform.SetParent(parent, false);
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fill = fillGo.AddComponent<Image>();
            fill.fillAmount = 1f;
            var bar = root.AddComponent<StatusBarUI>();
            bar.Tracked = tracked;
            bar.Fill = fill;
            return bar;
        }

        /// <summary>
        /// An unbound StatusBarUI (never injected) is inert — it neither throws nor logs, and leaves
        /// its authored fill alone until a ShipView is bound.
        /// </summary>
        [UnityTest]
        public IEnumerator StatusBarUI_Unbound_IsInertAndDoesNotThrow()
        {
            var parent = CreateRoot("RigRoot");
            var bar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Shield, out var fill);

            yield return null;

            // OnEnable first ran inside AddComponent, before CreateBar assigned the fill.
            bar.enabled = false;
            bar.enabled = true;
            yield return null;

            Assert.AreEqual(1f, fill.fillAmount, 0.001f, "An unbound bar should leave its authored fill untouched");
        }

        /// <summary>
        /// Bind seeds the bar's fill from the bound resource's current fraction — the bar reads
        /// correctly from frame 0 instead of showing the prefab's fill until the first damage event.
        /// </summary>
        [UnityTest]
        public IEnumerator StatusBarUI_Bind_SeedsFillFromBoundResource()
        {
            var damage = new FakeDamageEvents();
            damage.Health.ApplyDamage(40f); // 60 %
            damage.Shield.ApplyDamage(25f); // 50 %

            var parent = CreateRoot("RigRoot");
            var shieldBar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Shield, out var shieldFill);
            var healthBar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Health, out var healthFill);

            yield return null;

            shieldBar.Bind(new ShipView(parent.transform, damage, null, null, isPlayer: false));
            healthBar.Bind(new ShipView(parent.transform, damage, null, null, isPlayer: false));

            Assert.AreEqual(0.5f, shieldFill.fillAmount, 0.001f, "Shield bar should seed from the bound shield fraction");
            Assert.AreEqual(0.6f, healthFill.fillAmount, 0.001f, "Health bar should seed from the bound health fraction");
        }

        /// <summary>
        /// The boost gauge charges toward full as the cooldown runs down and recolors at the
        /// ready edge; Initialize seeds from current state so a rebind never shows stale fill.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostGaugeUI_TracksCooldown_AndRecolorsAtReadyEdge()
        {
            var status = new StubStatus { BoostAvailable = false, BoostCooldownPct = 0.6f };

            var go = CreateRoot("BoostGauge");
            var image = go.AddComponent<Image>();
            var gauge = go.AddComponent<BoostGaugeUI>();

            gauge.Initialize(status);
            Assert.AreEqual(0.4f, image.fillAmount, 0.001f, "Fill should charge as the cooldown runs down");
            var coolingColor = image.color;

            status.BoostAvailable = true;
            status.BoostCooldownPct = 0f;
            yield return null;

            Assert.AreEqual(1f, image.fillAmount, 0.001f, "Fill should be full once boost is ready");
            Assert.AreNotEqual(coolingColor, image.color, "Gauge should recolor at the ready edge");
        }
    }
}
