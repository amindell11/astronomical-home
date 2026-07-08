using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Combat.Conditions;
using Combat.Targeting;
using NUnit.Framework;
using UI;
using UI.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    [Category("UI")]
    public class EventDrivenRefactorEditModeTests
    {
        [Test]
        public void LockController_EmitsStateChangedTransitions()
        {
            var targetGo = new GameObject("Target");
            try
            {
                var target = new TestTargetable(targetGo.transform);
                var lockController = new TargetLock(0.1f, 0.1f, () => true);
                var transitions = new List<(LockState from, LockState to)>();
                lockController.OnStateChanged += (from, to) => transitions.Add((from, to));

                Assert.IsTrue(lockController.TryStartLock(target));
                lockController.Update(0.2f, true);
                lockController.ConsumeLock();
                lockController.Update(0.01f, true);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (LockState.Idle, LockState.Locking),
                        (LockState.Locking, LockState.Locked),
                        (LockState.Locked, LockState.Cooldown),
                        (LockState.Cooldown, LockState.Idle)
                    },
                    transitions);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Heat_ProcessFireAndReset_EmitHeatChanged()
        {
            var go = new GameObject("HeatTest");
            try
            {
                var heat = go.AddComponent<Heat>();
                var events = new List<float>();
                heat.OnHeatChanged += (current, max) => events.Add(current / max);

                heat.ProcessFire();
                heat.Reset();

                Assert.That(events.Count, Is.EqualTo(2));
                Assert.That(events[0], Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(events[1], Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UILockOnAudio_RespondsToLockStateEventsWithoutPolling()
        {
            var go = new GameObject("UILockOnAudioTest");
            try
            {
                var source = go.AddComponent<AudioSource>();
                var uiAudio = go.AddComponent<UILockOnAudio>();
                SetPrivateField(uiAudio, "lockingLoopClip", CreateTestClip("locking"));
                SetPrivateField(uiAudio, "lockedClip", CreateTestClip("locked"));

                var lockSource = new TestLockStateSource();
                uiAudio.Initialize(lockSource);

                lockSource.SetState(LockState.Locking);
                Assert.IsTrue(source.loop, "Locking should start looping clip");
                Assert.IsNotNull(source.clip, "Locking should assign looping clip");

                lockSource.SetState(LockState.Cooldown);
                Assert.IsFalse(source.loop, "Cooldown should stop looping audio");
                Assert.IsNull(source.clip, "Cooldown should clear looping clip");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LockReadoutUI_SpinnerFollowsLockStateEvents()
        {
            var uiGo = new GameObject("LockReadoutUITest");
            var spinnerGo = new GameObject("Spinner");
            try
            {
                var ui = uiGo.AddComponent<LockReadoutUI>();
                var spinner = spinnerGo.AddComponent<Image>();
                SetPrivateField(ui, "spinner", spinner);

                var lockSource = new TestLockStateSource();
                ui.Initialize(lockSource);

                Assert.IsFalse(spinner.enabled);
                lockSource.SetState(LockState.Cooldown);
                Assert.IsTrue(spinner.enabled);
                lockSource.SetState(LockState.Idle);
                Assert.IsFalse(spinner.enabled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiGo);
                UnityEngine.Object.DestroyImmediate(spinnerGo);
            }
        }

        [Test]
        public void AmmoCounterUI_CounterAndReloadFillFollowRoundsEvents()
        {
            var uiGo = new GameObject("AmmoCounterUITest");
            var roundsGo = new GameObject("Rounds");
            var textGo = new GameObject("Count");
            var fillGo = new GameObject("ReloadFill");
            try
            {
                var ui = uiGo.AddComponent<AmmoCounterUI>();
                var text = textGo.AddComponent<Text>();
                var fill = fillGo.AddComponent<Image>();
                SetPrivateField(ui, "countText", text);
                SetPrivateField(ui, "reloadFill", fill);

                var rounds = roundsGo.AddComponent<Rounds>();
                rounds.Configure(maxAmmo: 2, reloadTime: 1f);

                ui.Initialize(rounds);
                Assert.AreEqual("2 / 2", text.text);
                Assert.IsFalse(fill.enabled);

                rounds.ProcessFire();
                Assert.AreEqual("1 / 2", text.text);

                rounds.ProcessFire();
                Assert.AreEqual("0 / 2", text.text);
                Assert.IsTrue(fill.enabled, "Emptying the magazine starts the reload fill.");

                rounds.Tick(1.1f);
                Assert.AreEqual("2 / 2", text.text);
                Assert.IsFalse(fill.enabled, "Reload completion hides the fill.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiGo);
                UnityEngine.Object.DestroyImmediate(roundsGo);
                UnityEngine.Object.DestroyImmediate(textGo);
                UnityEngine.Object.DestroyImmediate(fillGo);
            }
        }

        [Test]
        public void HeatGaugeUI_UpdatesFillFromHeatChangedEvent()
        {
            var heatGo = new GameObject("Heat");
            var uiGo = new GameObject("HeatGaugeUI");
            try
            {
                var heat = heatGo.AddComponent<Heat>();
                var image = uiGo.AddComponent<Image>();
                var ui = uiGo.AddComponent<HeatGaugeUI>();
                SetPrivateField(ui, "fillImage", image);

                ui.Initialize(heat);

                heat.ProcessFire();
                Assert.That(image.fillAmount, Is.EqualTo(0.25f).Within(0.0001f));

                heat.Reset();
                Assert.That(image.fillAmount, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(heatGo);
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        [Test]
        public void RuntimeFiles_AreDecoupledFromEditorOrOverlayConcerns()
        {
            var assetsPath = Application.dataPath;
            var updatingFieldPath = Path.Combine(assetsPath, "Scripts", "Asteroids", "Fields", "UpdatingAsteroidField.cs");
            var managerPath = Path.Combine(assetsPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs");

            var updatingFieldSource = File.ReadAllText(updatingFieldPath);
            var managerSource = File.ReadAllText(managerPath);

            StringAssert.DoesNotContain("using UnityEditor", updatingFieldSource);
            StringAssert.DoesNotContain("OnDrawGizmosSelected", updatingFieldSource);
            StringAssert.DoesNotContain("GameContext.Instance", managerSource);
            StringAssert.DoesNotContain("PresentationReady", managerSource);
        }

        private static AudioClip CreateTestClip(string name)
        {
            return AudioClip.Create(name, 128, 1, 44100, false);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class TestTargetable : ITargetable
        {
            public TestTargetable(Transform targetPoint)
            {
                TargetPoint = targetPoint;
            }

            public Transform TargetPoint { get; }
            public LockChannel Lock { get; } = new();
        }

        private sealed class TestLockStateSource : ILockStateSource
        {
            public LockState State { get; private set; } = LockState.Idle;
            public event Action<LockState, LockState> OnStateChanged;

            public void SetState(LockState next)
            {
                if (State == next) return;
                var previous = State;
                State = next;
                OnStateChanged?.Invoke(previous, next);
            }
        }
    }
}
