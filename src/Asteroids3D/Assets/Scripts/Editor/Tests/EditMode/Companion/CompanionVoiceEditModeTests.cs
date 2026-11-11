using Game.Companion;
using Game.Moments;
using NUnit.Framework;
using UI.Comms;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Companion
{
    [Category("UI")]
    public class CompanionVoiceEditModeTests
    {
        private const string FontPath = "Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf";

        private GameObject hud;
        private CompanionFeelProfile feel;
        private CompanionLineSheet sheet;
        private CommLine display;
        private CompanionVoice voice;
        private RunMomentStream moments;

        [SetUp]
        public void SetUp()
        {
            hud = new GameObject("Companion HUD", typeof(RectTransform), typeof(Canvas));
            feel = ScriptableObject.CreateInstance<CompanionFeelProfile>();
            display = CommLine.Create(hud.transform, AssetDatabase.LoadAssetAtPath<Font>(FontPath), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (voice) Object.DestroyImmediate(voice.gameObject);
            if (hud) Object.DestroyImmediate(hud);
            if (feel) Object.DestroyImmediate(feel);
            if (sheet) Object.DestroyImmediate(sheet);
        }

        private void BuildVoice(bool debugLabels, string speaker, string moment, bool mustSay, params string[] lines)
        {
            sheet = ScriptableObject.CreateInstance<CompanionLineSheet>();
            using (var serialized = new SerializedObject(sheet))
            {
                var list = serialized.FindProperty("slots");
                list.arraySize = 1;
                var slot = list.GetArrayElementAtIndex(0);
                slot.FindPropertyRelative("moment").stringValue = moment;
                slot.FindPropertyRelative("speaker").stringValue = speaker;
                slot.FindPropertyRelative("mustSay").boolValue = mustSay;
                var spoken = slot.FindPropertyRelative("lines");
                spoken.arraySize = lines.Length;
                for (var i = 0; i < lines.Length; i++)
                    spoken.GetArrayElementAtIndex(i).stringValue = lines[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            voice = CompanionVoice.Create(hud.transform, sheet, feel, display, () => true, debugLabels);
            moments = new RunMomentStream();
            voice.Initialize(moments);
        }

        [Test]
        public void NamedSpeaker_PrefixesTheOfferedLine()
        {
            BuildVoice(false, "Quartermaster", SectorEntered.SectorSlot, true, "in sector {sector}");

            moments.Raise(new SectorEntered(3));

            Assert.AreEqual("Quartermaster: in sector 3", display.Line);
        }

        [Test]
        public void BlankSpeaker_LeavesTheLineUnchanged()
        {
            BuildVoice(false, "", SectorEntered.SectorSlot, true, "in sector {sector}");

            moments.Raise(new SectorEntered(3));

            Assert.AreEqual("in sector 3", display.Line);
        }

        [Test]
        public void NamedSpeaker_PrefixesTheDebugLabel()
        {
            BuildVoice(true, "Quartermaster", SectorEntered.SectorSlot, true);

            moments.Raise(new SectorEntered(3));

            Assert.AreEqual("Quartermaster: [sector-entered] sector 3", display.Line);
        }
    }
}
