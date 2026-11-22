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

            Assert.AreEqual("veil line",
                table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false)[0].Text);
        }

        [Test]
        public void Pick_BlankFactionRowMatchesAnyFaction()
        {
            var table = LineTable.Parse(Table(
                Row("B1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "open", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("open", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false)[0].Text);
            Assert.AreEqual("open",
                table.Pick(SectorEntered.SectorSlot, WithFaction("bastion"), n => 0, false)[0].Text);
        }

        [Test]
        public void Pick_MomentWithoutAFactionFact_MatchesOnlyBlankFactionRows()
        {
            var table = LineTable.Parse(Table(
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "veil line", "In game"),
                Row("B1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "open line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var facts = new Dictionary<string, string> { ["sector"] = "2" };
            Assert.AreEqual("open line", table.Pick(SectorEntered.SectorSlot, facts, n => 0, false)[0].Text);
        }

        [Test]
        public void Pick_NoMatchingRow_ReturnsEmpty()
        {
            var table = LineTable.Parse(Table(
                Row("V1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "", "veil line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var called = false;
            Assert.IsEmpty(table.Pick(SectorEntered.SectorSlot, WithFaction("shoal"), n =>
            {
                called = true;
                return 0;
            }, false));
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
            }, false);
            Assert.AreEqual("three", text[0].Text);
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

            Assert.AreEqual("plays", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false)[0].Text);
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

            Assert.AreEqual("noted", table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false)[0].Text);
        }

        [Test]
        public void Parse_UnescapesExportedCells()
        {
            var table = LineTable.Parse(Table(
                Row("U1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "",
                    "\\*shudders\\* a \\| b&#32;", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            Assert.AreEqual("*shudders* a | b ",
                table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false)[0].Text);
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
        public void Parse_EmptyLine_NamesTheId() =>
            AssertFormat(Table(
                Row("Txt", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Txt");

        [Test]
        public void Parse_NoTable_Fails() =>
            AssertFormat("The line sheet\n\nEdit here, then export.\n", LineSlots(), "no table");

        [Test]
        public void Parse_EmptySpeaker_NamesTheId() =>
            AssertFormat(Table(
                Row("Spk", SectorEntered.SectorSlot, "", "", "", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Spk");

        [Test]
        public void Parse_AfterNamesAMissingId_NamesTheId() =>
            AssertFormat(Table(
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "Nope", "hi", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_AfterNamesAnotherMoment_NamesTheId() =>
            AssertFormat(Table(
                Row("Op", PlayerDied.DeathSlot, "", "Enemy", "", "opened", "In game"),
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "Op", "answered", "In game")),
                LineSlots((SectorEntered.SectorSlot, ""), (PlayerDied.DeathSlot, "")), "Aft");

        [Test]
        public void Parse_AfterNamesTheSameSpeaker_NamesTheId() =>
            AssertFormat(Table(
                Row("Op", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "opened", "In game"),
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "Op", "answered", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_AfterNamesARowThatNeverOpens_NamesTheId() =>
            AssertFormat(Table(
                Row("Op", SectorEntered.SectorSlot, "", "Enemy", "", "opened", "In game"),
                Row("Mid", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "any", "answered", "In game"),
                Row("Aft", SectorEntered.SectorSlot, "", "Enemy", "Mid", "never", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_AfterNamesARowOfAnotherFaction_NamesTheId() =>
            AssertFormat(Table(
                Row("Op", SectorEntered.SectorSlot, "shoal", "Enemy", "", "opened", "In game"),
                Row("Aft", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "Op", "never", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_AnyWithoutAnOpener_NamesTheId() =>
            AssertFormat(Table(
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "any", "answered", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Parse_LaterRowIsNotAValidAfterTarget() =>
            AssertFormat(Table(
                Row("Later", SectorEntered.SectorSlot, "", "Enemy", "", "later", "Later"),
                Row("Aft", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "Later", "answered", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")), "Aft");

        [Test]
        public void Pick_OpenerCarriesItsSpeaker()
        {
            var table = LineTable.Parse(Table(
                Row("E1", SectorEntered.SectorSlot, "veil", "Enemy", "", "enemy line", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var picked = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, false);
            Assert.AreEqual(1, picked.Count);
            Assert.AreEqual("Enemy", picked[0].Speaker);
            Assert.AreEqual("enemy line", picked[0].Text);
        }

        [Test]
        public void Pick_WithReply_ReturnsTheOpenerThenAFollower()
        {
            var picked = Exchange().Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, true);
            Assert.AreEqual(2, picked.Count);
            Assert.AreEqual("Enemy", picked[0].Speaker);
            Assert.AreEqual("opened", picked[0].Text);
            Assert.AreEqual(LineTable.CompanionSpeaker, picked[1].Speaker);
            Assert.AreEqual("answered", picked[1].Text);
        }

        [Test]
        public void Pick_WithoutReply_ReturnsOnlyTheOpener()
        {
            var calls = 0;
            var picked = Exchange().Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                calls++;
                return 0;
            }, false);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, picked.Count);
            Assert.AreEqual("Enemy", picked[0].Speaker);
            Assert.AreEqual("opened", picked[0].Text);
        }

        [Test]
        public void Pick_FollowerNeverOpens()
        {
            var table = LineTable.Parse(Table(
                Row("E1", SectorEntered.SectorSlot, "shoal", "Enemy", "", "shoal open", "In game"),
                Row("C1", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "any", "any reply", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var called = false;
            var picked = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                called = true;
                return 0;
            }, true);
            Assert.IsEmpty(picked);
            Assert.IsFalse(called);
        }

        [Test]
        public void Pick_IdFollower_FollowsOnlyItsOpener()
        {
            var table = LineTable.Parse(Table(
                Row("A", SectorEntered.SectorSlot, "", "Enemy", "", "a", "In game"),
                Row("B", SectorEntered.SectorSlot, "", "Enemy", "", "b", "In game"),
                Row("C", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "A", "after a", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var calls = 0;
            var pickedB = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                calls++;
                return 1;
            }, true);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, pickedB.Count);
            Assert.AreEqual("b", pickedB[0].Text);
            Assert.AreEqual("Enemy", pickedB[0].Speaker);

            calls = 0;
            var pickedA = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                calls++;
                return 0;
            }, true);
            Assert.AreEqual(2, calls);
            Assert.AreEqual("a", pickedA[0].Text);
            Assert.AreEqual("Enemy", pickedA[0].Speaker);
            Assert.AreEqual("after a", pickedA[1].Text);
            Assert.AreEqual(LineTable.CompanionSpeaker, pickedA[1].Speaker);
        }

        [Test]
        public void Pick_EitherRow_OpensOrFollows()
        {
            var table = LineTable.Parse(Table(
                Row("C", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "companion", "In game"),
                Row("E", SectorEntered.SectorSlot, "", "Elite", "either", "elite", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var calls = 0;
            var withReply = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                calls++;
                return 0;
            }, true);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(LineTable.CompanionSpeaker, withReply[0].Speaker);
            Assert.AreEqual("companion", withReply[0].Text);
            Assert.AreEqual("Elite", withReply[1].Speaker);
            Assert.AreEqual("elite", withReply[1].Text);

            calls = 0;
            var eliteOpens = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n =>
            {
                calls++;
                return calls == 1 ? 1 : 0;
            }, true);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, eliteOpens.Count);
            Assert.AreEqual("Elite", eliteOpens[0].Speaker);
            Assert.AreEqual("elite", eliteOpens[0].Text);
        }

        [Test]
        public void Pick_FollowerNeedsADifferentSpeaker()
        {
            var table = LineTable.Parse(Table(
                Row("C", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "", "open", "In game"),
                Row("S", SectorEntered.SectorSlot, "", LineTable.CompanionSpeaker, "either", "same", "In game"),
                Row("E", SectorEntered.SectorSlot, "", "Enemy", "any", "other", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var picked = table.Pick(SectorEntered.SectorSlot, WithFaction("veil"), n => 0, true);
            Assert.AreEqual(2, picked.Count);
            Assert.AreEqual("open", picked[0].Text);
            Assert.AreEqual(LineTable.CompanionSpeaker, picked[0].Speaker);
            Assert.AreEqual("other", picked[1].Text);
            Assert.AreEqual("Enemy", picked[1].Speaker);
        }

        [Test]
        public void Pick_FollowerMatchesTheFaction()
        {
            var table = LineTable.Parse(Table(
                Row("E", SectorEntered.SectorSlot, "", "Enemy", "", "open", "In game"),
                Row("V", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "any", "veil reply", "In game"),
                Row("S", SectorEntered.SectorSlot, "shoal", LineTable.CompanionSpeaker, "any", "shoal reply", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

            var picked = table.Pick(SectorEntered.SectorSlot, WithFaction("shoal"), n => 0, true);
            Assert.AreEqual(2, picked.Count);
            Assert.AreEqual("open", picked[0].Text);
            Assert.AreEqual("shoal reply", picked[1].Text);
            Assert.AreEqual(LineTable.CompanionSpeaker, picked[1].Speaker);
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
                Assert.IsNotEmpty(lines.Pick(SectorEntered.SectorSlot, WithFaction(faction), n => 0, false), faction);
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

        private LineTable Exchange() =>
            LineTable.Parse(Table(
                Row("E1", SectorEntered.SectorSlot, "veil", "Enemy", "", "opened", "In game"),
                Row("C1", SectorEntered.SectorSlot, "veil", LineTable.CompanionSpeaker, "any", "answered", "In game")),
                LineSlots((SectorEntered.SectorSlot, "")));

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
