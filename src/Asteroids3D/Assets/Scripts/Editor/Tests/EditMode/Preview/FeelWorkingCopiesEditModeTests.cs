using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Preview;
using Game.Travel;
using NUnit.Framework;
using UI.Recap;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Preview
{
    [Category("Bootstrap")]
    public sealed class FeelWorkingCopiesEditModeTests
    {
        private const string RecapPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string JumpPath = "Assets/Settings/Game/SectorJumpTuning.asset";
        private string folder;
        private FeelWorkingCopies copies;

        [SetUp]
        public void SetUp()
        {
            folder = AssetDatabase.GUIDToAssetPath(
                AssetDatabase.CreateFolder("Assets", "FeelWorkingCopiesTest-" + Guid.NewGuid().ToString("N")));
            copies = new FeelWorkingCopies();
        }

        [TearDown]
        public void TearDown()
        {
            copies.Close();
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void For_GivesOneInMemoryCopyPerAsset_AndCloseDestroysIt()
        {
            var asset = CopyOfShipped<RecapFeelProfile>(RecapPath);
            var copy = copies.For(asset);

            Assert.That(copy, Is.Not.SameAs(asset));
            Assert.That(copies.For(asset), Is.SameAs(copy));
            Assert.That(copies.AssetOf(copy), Is.SameAs(asset));
            Assert.That(copy.name, Is.EqualTo(asset.name));
            Assert.IsFalse(AssetDatabase.Contains(copy));
            Assert.IsFalse(copies.HasUnsavedEdits(copy), "a fresh copy matches its asset");
            copies.Close();
            Assert.IsTrue(copy == null, "the copies end with the session");
        }

        [Test]
        public void EditThenDiscard_LeavesTheAssetFileByteForByteUnchanged_AndNeverDirty()
        {
            var asset = CopyOfShipped<RecapFeelProfile>(RecapPath);
            var path = AssetDatabase.GetAssetPath(asset);
            var before = File.ReadAllBytes(path);
            var saved = asset.BannerInSeconds;
            var copy = copies.For(asset);

            Edit(copy, "bannerInSeconds", saved + 1f);
            Assert.That(copy.BannerInSeconds, Is.EqualTo(saved + 1f), "the copy carries the edit live");
            Assert.IsTrue(copies.HasUnsavedEdits(copy));
            Assert.IsFalse(EditorUtility.IsDirty(asset), "editing the copy leaves the asset clean");

            copies.Resolve(_ => false);
            copies.Close();
            AssetDatabase.SaveAssets();
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            Assert.That(asset.BannerInSeconds, Is.EqualTo(saved));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "a project save writes nothing to the asset");
        }

        [Test]
        public void Save_WritesExactlyTheEditedKnobToTheAssetFile()
        {
            var asset = CopyOfShipped<SectorJumpProfile>(JumpPath);
            var path = AssetDatabase.GetAssetPath(asset);
            var before = File.ReadAllLines(path);
            Assume.That(asset.streakFan, Is.Not.EqualTo(7.25f));
            var copy = copies.For(asset);

            Edit(copy, "streakFan", 7.25f);
            copies.Save(copy);

            var after = File.ReadAllLines(path);
            Assert.IsFalse(EditorUtility.IsDirty(asset), "Save writes the asset to disk");
            Assert.IsFalse(copies.HasUnsavedEdits(copy));
            Assert.That(after.Length, Is.EqualTo(before.Length));
            var changed = Enumerable.Range(0, before.Length).Where(line => before[line] != after[line]);
            Assert.That(changed.Select(line => after[line].Trim()), Is.EqualTo(new[] { "streakFan: 7.25" }));
        }

        [Test]
        public void Revert_ResetsTheCopyToTheAssetsValues()
        {
            var asset = CopyOfShipped<SectorJumpProfile>(JumpPath);
            var copy = copies.For(asset);
            var revision = copy.Revision;

            Edit(copy, "streakFan", asset.streakFan + 3f);
            copies.Revert(copy);

            Assert.That(copy.streakFan, Is.EqualTo(asset.streakFan));
            Assert.IsFalse(copies.HasUnsavedEdits(copy));
            Assert.That(copy.Revision, Is.GreaterThan(revision + 1), "a revert reaches a live jump like an edit");
        }

        [Test]
        public void Resolve_AsksOnlyWhenACopyHoldsUnsavedEdits_AndNamesOnlyThoseCopies()
        {
            var recap = copies.For(CopyOfShipped<RecapFeelProfile>(RecapPath));
            var jump = copies.For(CopyOfShipped<SectorJumpProfile>(JumpPath));
            var asked = new List<IReadOnlyList<string>>();

            copies.Resolve(names =>
            {
                asked.Add(names);
                return false;
            });
            Assert.That(asked, Is.Empty, "untouched copies never ask");

            var saved = recap.BannerInSeconds;
            Edit(recap, "bannerInSeconds", saved + 1f);
            Edit(recap, "bannerInSeconds", saved);
            Assert.IsFalse(copies.HasUnsavedEdits(recap), "an edit set back to the saved value is no unsaved edit");

            Edit(jump, "streakFan", jump.streakFan + 3f);
            copies.Resolve(names =>
            {
                asked.Add(names);
                return true;
            });
            Assert.That(asked.Single(), Is.EqualTo(new[] { jump.name }));
            Assert.IsFalse(copies.HasUnsavedEdits(jump), "Save leaves nothing unsaved");
            Assert.That(((SectorJumpProfile)copies.AssetOf(jump)).streakFan, Is.EqualTo(jump.streakFan));
        }

        private T CopyOfShipped<T>(string shipped) where T : ScriptableObject
        {
            var path = $"{folder}/{Path.GetFileName(shipped)}";
            Assert.IsTrue(AssetDatabase.CopyAsset(shipped, path));
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // The panel's own edit path.
        private static void Edit(Object copy, string knob, float value)
        {
            using var serialized = new SerializedObject(copy);
            serialized.FindProperty(knob).floatValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
