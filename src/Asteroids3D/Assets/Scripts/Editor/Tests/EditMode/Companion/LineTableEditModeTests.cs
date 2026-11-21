using System;
using System.Collections.Generic;
using System.Text;
using Game.Companion;
using Game.Moments;
using NUnit.Framework;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Companion
{
    [Category("UI")]
    public class LineTableEditModeTests
    {
        private const string Header = "| ID | Moment | Faction | Speaker | After | Line | Status |";
        private const string Separator = "| --- | --- | --- | --- | --- | --- | --- |";
        private const string SheetPath = "Assets/Settings/Game/Companion/CompanionLines.asset";

        private CompanionLineSheet sheet;

        [TearDown]
        public void TearDown()
        {
            if (sheet) Object.DestroyImmediate(sheet);
        }

        [Test]
        public void Pick_ReturnsARowForTheMomentAndFaction()
        {
            var table = LineTable.Parse(Table(
                Row("S1", SectorEntered.SectorSlot, "shoal", LineTable.CompanionSpeaker, "", "shoal line", "In game"),
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "veil line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("veil line", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));
        }

        [Test]
        public void Pick_BlankFactionRowMatchesAnyFaction()
        {
            var table = LineTable.Parse(Table(
                Row("B1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "open", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("open", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));
            Assert.AreEqual("open", table.Pick(SectorEntered.SectorSlot, WithFaction("bastion"), n => 0));
        }

        [Test]
        public void Pick_MomentWithoutAFactionFact_MatchesOnlyBlankFactionRows()
        {
            var table = LineTable.Parse(Table(
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "veil line", "In game"),
                Row("B1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "open line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var facts = new Dictionary<string, string> { ["sector"] = "2" };
            Assert.AreEqual("open line", table.Pick(SectorEntered.SectorSlot, facts, n => 0));
        }

        [Test]
        public void Pick_NoMatchingRow_ReturnsNull()
        {
            var table = LineTable.Parse(Table(
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "veil line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var called = false;
            Assert.IsNull(table.Pick(SectorEntered.SectorSlot, WithFaction("shoal"), n =>
            {
                called = true;
                return 0;
            }));
            Assert.IsFalse(called);
        }

        [Test]
        public void Pick_SeveralMatches_UsesTheRangeIndex()
        {
            var table = LineTable.Parse(Table(
                Row("T1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "one", "In game"),
                Row("T2", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "two", "In game"),
                Row("T3", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "three", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var count = 0;
            var text = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                count = n;
                return 2;
            });
            Assert.AreEqual("three", text);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Parse_KeepsOnlyInGameRows()
        {
            var table = LineTable.Parse(Table(
                Row("L1", "no-such-moment", "veil", LineTable.CompanionSpeaker, "L0", "later", "Later"),
                Row("D1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "draft", "Draft"),
                Row("C1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "cut", "Cut"),
                Row("G1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "plays", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("plays", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));
        }

        [Test]
        public void Parse_IgnoresProseAroundTheTableAndExtraColumns()
        {
            var markdown = "The line sheet\n\n"
                + "| ID | Moment | Faction | Speaker | After | Line | Status | Notes |\n"
                + "| --- | --- | --- | --- | --- | --- | --- | --- |\n"
                + $"| N1 | {SectorEntered.SectorSlot} | veil | {LineTable.CompanionSpeaker} |  | noted | In game | a note |\n"
                + "\nEdit here, then export.\n";
            var table = LineTable.Parse(markdown, LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("noted", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));
        }

        [Test]
        public void Parse_UnescapesExportedCells()
        {
            var table = LineTable.Parse(Table(
                Row("U1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "",
                    "\\*shudders\\* a \\| b&#32;", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("*shudders* a | b ", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));
        }

        [Test]
        public void Parse_MissingColumn_NamesTheColumn() =>
            AssertFormat("| ID | Moment | Faction | Speaker | After | Line |\n| --- | --- | --- | --- | --- | --- |",
                LineSlots((SectorEntered.SectorSlot, "")), "Status");

        [Test]
        public void Parse_WrongCellCount_NamesTheRow() =>
            AssertFormat(Table($"| T1 | {SectorEntered.SectorSlot} | Companion | hi | In game |"),
                LineSlots((SectorEntered.SectorSlot, "")), "row 1");

        [Test]
        public void Parse_EmptyId_NamesTheRow() =>
            AssertFormat(Table(Row("", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "row 1");

        [Test]
        public void Parse_DuplicateId_NamesTheId() =>
            AssertFormat(Table(
                Row("Dup", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "a", "In game"),
                Row("Dup", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "b", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Dup");

        [Test]
        public void Parse_UnknownStatus_NamesTheId() =>
            AssertFormat(Table(
                Row("Bad", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "hi", "Soon")),
                LineSlots((SectorEntered.SectorSlot, "")), "Bad");

        [Test]
        public void Parse_UnknownMoment_NamesTheId() =>
            AssertFormat(Table(
                Row("M9", "no-such-moment", "", LineTable.CompanionSpeaker, "", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "M9");

        [Test]
        public void Parse_SpeakerMismatch_NamesTheId() =>
            AssertFormat(Table(
                Row("Spk", SectorEntered.SectorSlot, "", "Enemy", "", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Spk");

        [Test]
        public void Parse_FilledAfter_NamesTheId() =>
            AssertFormat(Table(
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "Spk", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_EmptyLine_NamesTheId() =>
            AssertFormat(Table(
                Row("Txt", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Txt");

        [Test]
        public void Parse_NoTable_Fails() =>
            AssertFormat("The line sheet\n\nEdit here, then export.\n", LineSlots(), "no table");

        [Test]
        public void Parse_SlotSpeakerMatchesItsRows()
        {
            var slots = LineSlots((SectorEntered.SectorSlot, "Repair crew"));
            var kept = LineTable.Parse(Table(
                Row("R1", SectorEntered.SectorSlot, "", "Repair crew", "", "crew line", "In game")), slots);
            Assert.AreEqual("crew line", kept.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0));

            AssertFormat(Table(
                Row("C1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "nope", "In game")),
                slots, "C1");
        }

        [Test]
        public void Factions_ListsTheDistinctFactionsOfKeptRows()
        {
            var table = LineTable.Parse(Table(
                Row("S1", SectorEntered.SectorSlot, "shoal", LineTable.CompanionSpeaker, "", "a", "In game"),
                Row("S2", SectorEntered.SectorSlot, "shoal", LineTable.CompanionSpeaker, "", "b", "In game"),
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "c", "In game"),
                Row("B1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "d", "In game"),
                Row("X1", "no-such-moment", "cutters", LineTable.CompanionSpeaker, "X0", "e", "Cut")),
                LineSlots((SectorEntered.SectorSlot, "")));

            CollectionAssert.AreEquivalent(new[] { "shoal", "veil" }, table.Factions);
        }

        [Test]
        public void ShippedLineFile_Loads()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath);
            Assert.IsTrue(shipped, "the line sheet ships");

            var lines = shipped.LoadLines();
            foreach (var faction in new[] { "shoal", "veil", "bastion" })
                Assert.IsNotNull(lines.Pick(SectorEntered.SectorSlot, WithFaction(faction), n => 0), faction);
        }

        [Test]
        public void ShippedLineFile_NamesOnlyRealFactions()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath);
            var ids = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Faction"))
                ids.Add(AssetDatabase.LoadAssetAtPath<Faction>(AssetDatabase.GUIDToAssetPath(guid)).id);

            foreach (var faction in shipped.LoadLines().Factions)
                Assert.That(ids, Does.Contain(faction));
        }

        private static void AssertFormat(string markdown, IReadOnlyList<LineSlot> slots, string fragment)
        {
            var error = Assert.Throws<FormatException>(() => LineTable.Parse(markdown, slots));
            Assert.That(error.Message, Does.Contain(fragment));
        }

        private static string Table(params string[] rows)
        {
            var text = new StringBuilder(Header).Append('\n').Append(Separator);
            foreach (var row in rows)
                text.Append('\n').Append(row);
            return text.ToString();
        }

        private static string Row(string id, string moment, string faction, string speaker, string after, string line,
            string status) =>
            $"| {id} | {moment} | {faction} | {speaker} | {after} | {line} | {status} |";

        private static IReadOnlyDictionary<string, string> WithFaction(string faction) =>
            new Dictionary<string, string> { ["faction"] = faction };

        private IReadOnlyList<LineSlot> LineSlots(params (string moment, string speaker)[] defined)
        {
            if (sheet) Object.DestroyImmediate(sheet);
            sheet = ScriptableObject.CreateInstance<CompanionLineSheet>();
            using (var serialized = new SerializedObject(sheet))
            {
                var list = serialized.FindProperty("slots");
                list.arraySize = defined.Length;
                for (var i = 0; i < defined.Length; i++)
                {
                    var slot = list.GetArrayElementAtIndex(i);
                    slot.FindPropertyRelative("moment").stringValue = defined[i].moment;
                    slot.FindPropertyRelative("speaker").stringValue = defined[i].speaker;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return sheet.Slots;
        }
    }
}
