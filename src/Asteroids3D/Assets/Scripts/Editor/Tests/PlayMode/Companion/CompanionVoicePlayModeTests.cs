#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Damage;
using Game.Companion;
using Game.Moments;
using Game.Runs;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UI.Comms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Companion
{
    [Category("UI")]
    public class CompanionVoicePlayModeTests : PlayModeWorldFixture
    {
        private const string FontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";

        private GameObject hud;
        private CompanionFeelProfile feel;
        private CompanionLineSheet sheet;
        private CommLine display;
        private CompanionVoice voice;
        private RunMomentStream moments;
        private string dir;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            hud = new GameObject("Companion HUD", typeof(RectTransform), typeof(Canvas));
            feel = ScriptableObject.CreateInstance<CompanionFeelProfile>();
            display = CommLine.Create(hud.transform, TestAssets.Load<Font>(FontPath), null);
            dir = Path.Combine(Path.GetTempPath(), "companion-voice-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(voice);
            DestroyTestObject(hud);
            Object.Destroy(feel);
            if (sheet) Object.Destroy(sheet);
            base.TearDown();
        }

        private RunStarted FirstRun() => new(PastRuns.Read(Path.Combine(dir, "run-records.jsonl")));

        private void BuildVoice(params (string moment, bool mustSay, string[] lines)[] slots)
        {
            sheet = ScriptableObject.CreateInstance<CompanionLineSheet>();
            using (var serialized = new SerializedObject(sheet))
            {
                var list = serialized.FindProperty("slots");
                list.arraySize = slots.Length;
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = list.GetArrayElementAtIndex(i);
                    slot.FindPropertyRelative("moment").stringValue = slots[i].moment;
                    slot.FindPropertyRelative("mustSay").boolValue = slots[i].mustSay;
                    var lines = slot.FindPropertyRelative("lines");
                    lines.arraySize = slots[i].lines.Length;
                    for (var j = 0; j < slots[i].lines.Length; j++)
                        lines.GetArrayElementAtIndex(j).stringValue = slots[i].lines[j];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            voice = CompanionVoice.Create(hud.transform, sheet, feel, display, () => true);
            moments = new RunMomentStream();
            voice.Initialize(moments);
        }

        [UnityTest]
        public IEnumerator EmptyLineSlots_ShowNothing()
        {
            BuildVoice(RunStarted.AllSlots.Append(PlayerDied.DeathSlot)
                .Select(slot => (slot, true, Array.Empty<string>())).ToArray());

            moments.Raise(FirstRun());
            moments.Raise(new PlayerDied(1, DamageKind.Laser));
            yield return null;

            Assert.IsFalse(display.Speaking, "an empty line slot stays silent");
            Assert.AreEqual(string.Empty, display.Line);
        }

        [UnityTest]
        public IEnumerator RaisedMoment_SpeaksItsLineWithItsFactsAtOnce()
        {
            BuildVoice((RunStarted.FirstLaunchSlot, true, new[] { "clone {clone}" }));

            moments.Raise(FirstRun());

            Assert.IsTrue(display.Speaking, "the line shows in the frame the moment is raised");
            Assert.AreEqual("clone 1", display.Line);
            yield return null;
            Assert.IsTrue(display.Speaking, "the line holds");
        }

        [UnityTest]
        public IEnumerator PlainLineRightAfterASpokenLine_IsDropped()
        {
            BuildVoice((RunStarted.FirstLaunchSlot, true, new[] { "clone {clone}" }),
                (PlayerDied.DeathSlot, false, new[] { "down in {sector}" }));

            moments.Raise(FirstRun());
            moments.Raise(new PlayerDied(1, DamageKind.Laser));
            Assert.AreEqual("clone 1", display.Line, "the plain line does not replace the spoken one");

            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.AreEqual("clone 1", display.Line, "the dropped line never shows");
        }

        [UnityTest]
        public IEnumerator MomentWithoutALineSlot_Throws()
        {
            BuildVoice((RunStarted.FirstLaunchSlot, true, new[] { "clone {clone}" }));

            Assert.Throws<InvalidOperationException>(() => moments.Raise(new PlayerDied(1, DamageKind.Laser)));
            yield return null;
        }
    }
}
#endif
