#if UNITY_EDITOR && ASTRO_FMOD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.Targeting;
using Combat.Weapons.Conditions;
using Damage;
using FMOD.Studio;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Damage;
using Ships.Registry;
using UI.Audio;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class HudAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/UIOverlay.prefab";
        private const string LowHealthEvent = "event:/HUD/LowHealth";
        private const string ShieldDepletedEvent = "event:/HUD/ShieldDepleted";
        private const string LockingEvent = "event:/HUD/LockOn/Locking";
        private const string LockedEvent = "event:/HUD/LockOn/Locked";
        private const string OverheatEvent = "event:/HUD/Overheat";
        private const string BoostReadyEvent = "event:/HUD/BoostReady";
        private const string IncomingMissileEvent = "event:/HUD/IncomingMissile";

        private sealed class FakeDamage : IDamageEvents
        {
            public event Action<DamageInfo> OnDamaged { add { } remove { } }
            public event Action<ShipId, DamageInfo> OnDeath;
            public Resource Health { get; } = new Resource(100f);
            public RegenResource Shield { get; } = new RegenResource(100f, 0f, 999f);
            public void Die() => OnDeath?.Invoke(default, default);
        }

        private sealed class FakeLock : ILockStateSource
        {
            public LockState State { get; private set; } = LockState.Idle;
            public event Action<LockState, LockState> OnStateChanged;

            public void Set(LockState next)
            {
                var previous = State;
                State = next;
                OnStateChanged?.Invoke(previous, next);
            }
        }

        private sealed class FakeHeat : IHeatReadout
        {
            public float HeatPct => 0f;
            public bool Overheated => false;
            public event Action<float, float> OnHeatChanged { add { } remove { } }
            public event Action OnOverheat;
            public event Action OnCooldownStart { add { } remove { } }
            public void Overheat() => OnOverheat?.Invoke();
        }

        private sealed class FakeMissile : ITrackingMissile
        {
            public Kinematics Kinematics => default;
        }

        private sealed class FakeStatus : IShipStatus
        {
            public readonly List<ITrackingMissile> Trackers = new();
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable { get; set; }
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 0f;
            public float MaxYawRate => 0f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => Trackers;
        }

        private GameObject host;

        [TearDown]
        public override void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            base.TearDown();
        }

        // The overlay prefab's authored values on a lone component, so each test binds one cue's fake.
        private T NewAuthored<T>() where T : MonoBehaviour
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath).GetComponentInChildren<T>(true);
            Assert.IsNotNull(authored, $"the overlay prefab carries {typeof(T).Name}");
            host = new GameObject(typeof(T).Name);
            var component = host.AddComponent<T>();
            ComponentUtility.CopyComponent(authored);
            ComponentUtility.PasteComponentValues(component);
            return component;
        }

        private static int Sounding(EventDescription description) =>
            Instances(description).Count(i => State(i) is PLAYBACK_STATE.PLAYING or PLAYBACK_STATE.STARTING);

        [UnityTest]
        public IEnumerator LowHealth_SoundsExactlyWhileTheRuleHolds_AndStopsOnRecovery()
        {
            var alarm = Fresh(LowHealthEvent);
            var damage = new FakeDamage();
            NewAuthored<UIHealthAudio>().Initialize(damage);

            damage.Shield.ApplyDamage(95f);
            yield return Flush();
            Assert.AreEqual(0, Sounding(alarm), "a critical shield alone leaves the alarm off");

            damage.Health.ApplyDamage(80f);
            yield return Flush();
            Debug.Log($"[FMOD] {LowHealthEvent} sounding at health 20% shield 5% = {Sounding(alarm)}");
            Assert.AreEqual(1, Sounding(alarm), "health and shield both critical sound one alarm");

            damage.Health.ApplyDamage(5f);
            yield return Flush();
            Assert.AreEqual(1, Instances(alarm).Length, "more damage inside the rule keeps the one alarm");

            damage.Health.RestoreFraction(0.5f);
            yield return Flush();
            Debug.Log($"[FMOD] {LowHealthEvent} sounding after recovery to 65% = {Sounding(alarm)}");
            Assert.AreEqual(0, Sounding(alarm), "recovering past the threshold stops the alarm");
        }

        [UnityTest]
        public IEnumerator LowHealth_StopsOnDeath()
        {
            var alarm = Fresh(LowHealthEvent);
            var damage = new FakeDamage();
            NewAuthored<UIHealthAudio>().Initialize(damage);

            damage.Shield.ApplyDamage(100f);
            damage.Health.ApplyDamage(90f);
            yield return Flush();
            Assert.AreEqual(1, Sounding(alarm), "the alarm sounds before the death");

            damage.Die();
            yield return Flush();
            Debug.Log($"[FMOD] {LowHealthEvent} sounding after death = {Sounding(alarm)}");
            Assert.AreEqual(0, Sounding(alarm), "death stops the alarm");
        }

        [UnityTest]
        public IEnumerator ShieldDepleted_PlaysOncePerDepletion()
        {
            var depleted = Fresh(ShieldDepletedEvent);
            var damage = new FakeDamage();
            NewAuthored<UIHealthAudio>().Initialize(damage);

            damage.Shield.ApplyDamage(60f);
            yield return Flush();
            Assert.AreEqual(0, Instances(depleted).Length, "a shield hit short of zero plays nothing");

            damage.Shield.ApplyDamage(60f);
            yield return Flush();
            var first = Instances(depleted).Length;
            Debug.Log($"[FMOD] {ShieldDepletedEvent} instances after the first depletion = {first}");
            Assert.AreEqual(1, first, "one cue when the shield reaches zero");

            depleted = Fresh(ShieldDepletedEvent);
            damage.Shield.ApplyDamage(10f);
            damage.Shield.RestoreFraction(0.3f);
            yield return Flush();
            Assert.AreEqual(0, Instances(depleted).Length, "damage at zero and a recharge play nothing");

            damage.Shield.ApplyDamage(100f);
            yield return Flush();
            var second = Instances(depleted).Length;
            Debug.Log($"[FMOD] {ShieldDepletedEvent} instances after the second depletion = {second}");
            Assert.AreEqual(1, second, "one cue for the second depletion");
        }

        [UnityTest]
        public IEnumerator LockOn_LockingHoldsTheLoop_LockedStopsItAndPlaysOneLocked()
        {
            var locking = Fresh(LockingEvent);
            var locked = Fresh(LockedEvent);
            var source = new FakeLock();
            NewAuthored<UILockOnAudio>().Initialize(source);
            yield return Flush();
            Assert.AreEqual(0, Instances(locking).Length, "an idle lock plays nothing");

            source.Set(LockState.Locking);
            yield return Flush();
            yield return Flush();
            yield return Flush();
            Debug.Log($"[FMOD] {LockingEvent} sounding through Locking = {Sounding(locking)} of {Instances(locking).Length}");
            Assert.AreEqual(1, Instances(locking).Length, "one locking loop");
            Assert.AreEqual(1, Sounding(locking), "the locking loop sounds through Locking");

            source.Set(LockState.Locked);
            yield return Flush();
            var lockedCount = Instances(locked).Length;
            Debug.Log($"[FMOD] at Locked: {LockingEvent} sounding = {Sounding(locking)}, {LockedEvent} instances = {lockedCount}");
            Assert.AreEqual(0, Sounding(locking), "Locked stops the locking loop");
            Assert.AreEqual(1, lockedCount, "Locked plays one cue");

            source.Set(LockState.Cooldown);
            source.Set(LockState.Idle);
            source.Set(LockState.Locking);
            yield return Flush();
            Assert.AreEqual(1, Sounding(locking), "a new lock sounds the loop again");

            source.Set(LockState.Idle);
            yield return Flush();
            Assert.AreEqual(0, Sounding(locking), "a lock dropped to Idle stops the loop");
        }

        [UnityTest]
        public IEnumerator Overheat_PlaysOncePerOverheat()
        {
            var overheat = Fresh(OverheatEvent);
            var heat = new FakeHeat();
            NewAuthored<UILaserAudio>().Initialize(heat);
            yield return Flush();
            Assert.AreEqual(0, Instances(overheat).Length, "binding plays nothing");

            heat.Overheat();
            yield return Flush();
            var first = Instances(overheat).Length;
            Debug.Log($"[FMOD] {OverheatEvent} instances after one overheat = {first}");
            Assert.AreEqual(1, first, "one cue per overheat");

            overheat = Fresh(OverheatEvent);
            heat.Overheat();
            yield return Flush();
            Assert.AreEqual(1, Instances(overheat).Length, "one cue for the next overheat");
        }

        [UnityTest]
        public IEnumerator BoostReady_PlaysOncePerReadyEdge_AndNoneAtSpawn()
        {
            var ready = Fresh(BoostReadyEvent);
            var status = new FakeStatus { BoostAvailable = true };
            NewAuthored<UIBoostAudio>().Initialize(status);
            yield return null;
            yield return Flush();
            Debug.Log($"[FMOD] {BoostReadyEvent} instances at a spawn with boost ready = {Instances(ready).Length}");
            Assert.AreEqual(0, Instances(ready).Length, "a spawn with boost ready stays silent");

            status.BoostAvailable = false;
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(ready).Length, "spending the boost plays nothing");

            status.BoostAvailable = true;
            yield return null;
            yield return Flush();
            var edge = Instances(ready).Length;
            Debug.Log($"[FMOD] {BoostReadyEvent} instances after the ready edge = {edge}");
            Assert.AreEqual(1, edge, "one cue at the ready edge");

            ready = Fresh(BoostReadyEvent);
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(ready).Length, "staying ready plays no second cue");
        }

        [UnityTest]
        public IEnumerator IncomingMissile_HeldWhileTracked_StoppedWhenTheLastLeaves()
        {
            var warning = Fresh(IncomingMissileEvent);
            var status = new FakeStatus();
            NewAuthored<UIMissileWarningAudio>().Initialize(status);
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(warning).Length, "no tracker, no warning");

            var first = new FakeMissile();
            var second = new FakeMissile();
            status.Trackers.Add(first);
            yield return null;
            yield return Flush();
            Debug.Log($"[FMOD] {IncomingMissileEvent} sounding with one tracker = {Sounding(warning)}");
            Assert.AreEqual(1, Sounding(warning), "a tracker sounds the warning");

            status.Trackers.Add(second);
            status.Trackers.Remove(first);
            yield return null;
            yield return Flush();
            Assert.AreEqual(1, Instances(warning).Length, "a second tracker keeps the one warning");
            Assert.AreEqual(1, Sounding(warning), "the warning holds while a tracker remains");

            status.Trackers.Remove(second);
            yield return null;
            yield return Flush();
            Debug.Log($"[FMOD] {IncomingMissileEvent} sounding after the last tracker left = {Sounding(warning)}");
            Assert.AreEqual(0, Sounding(warning), "the last tracker leaving stops the warning");
        }
    }
}
#endif
