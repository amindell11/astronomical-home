using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Preview;
using Game.Travel;
using NUnit.Framework;
using UI.Recap;
using UI.Projection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Preview
{
    [Category("Bootstrap")]
    public sealed class FeelSnapshotEditModeTests
    {
        private const string RecapPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string JumpPath = "Assets/Settings/Game/SectorJumpTuning.asset";
        private const string CanopyPath = "Assets/Settings/UI/CanopyProjection.asset";
        private string folder;
        private FeelSnapshot snapshot;

        [SetUp]
        public void SetUp()
        {
            folder = AssetDatabase.GUIDToAssetPath(
                AssetDatabase.CreateFolder("Assets", "FeelSnapshotTest-" + Guid.NewGuid().ToString("N")));
            snapshot = FeelSnapshot.instance;
        }

        [TearDown]
        public void TearDown()
        {
            snapshot.Close();
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void Take_RefusesAnAssetTwice_AndCloseReleasesIt()
        {
            var asset = CopyOfShipped<RecapFeelProfile>(RecapPath);
            snapshot.Take(asset);

            Assert.IsFalse(snapshot.HasUnsavedEdits(asset), "a fresh snapshot matches its asset");
            Assert.Throws<InvalidOperationException>(() => snapshot.Take(asset));
            snapshot.Close();
            Assert.Throws<ArgumentException>(() => snapshot.HasUnsavedEdits(asset), "the snapshot ends with Close");
        }

        [Test]
        public void EditThenDiscard_LeavesTheAssetFileByteForByteUnchanged_AndClean()
        {
            // RecapFeel.asset is not in the serializer's own form, so any write of it changes its bytes.
            var asset = CopyOfShipped<RecapFeelProfile>(RecapPath);
            var path = AssetDatabase.GetAssetPath(asset);
            var before = File.ReadAllBytes(path);
            var saved = asset.BannerInSeconds;
            snapshot.Take(asset);

            Edit(asset, "bannerInSeconds", saved + 1f);
            Assert.That(asset.BannerInSeconds, Is.EqualTo(saved + 1f), "the edit lands on the asset live");
            Assert.IsTrue(snapshot.HasUnsavedEdits(asset));

            snapshot.Resolve(_ => false);
            snapshot.Close();
            AssetDatabase.SaveAssets();
            Assert.That(asset.BannerInSeconds, Is.EqualTo(saved));
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "a project save after Discard writes nothing");
        }

        [Test]
        public void Save_WritesExactlyTheEditedKnob_AndALaterDiscardRestoresTheSavedValue()
        {
            var asset = CopyOfShipped<SectorJumpProfile>(JumpPath);
            var path = AssetDatabase.GetAssetPath(asset);
            var before = File.ReadAllLines(path);
            Assume.That(asset.streakFan, Is.Not.EqualTo(7.25f));
            snapshot.Take(asset);

            Edit(asset, "streakFan", 7.25f);
            snapshot.Save(asset);

            var after = File.ReadAllLines(path);
            Assert.IsFalse(EditorUtility.IsDirty(asset), "Save writes the asset to disk");
            Assert.IsFalse(snapshot.HasUnsavedEdits(asset));
            Assert.That(after.Length, Is.EqualTo(before.Length));
            var changed = Enumerable.Range(0, before.Length).Where(line => before[line] != after[line]);
            Assert.That(changed.Select(line => after[line].Trim()), Is.EqualTo(new[] { "streakFan: 7.25" }));

            Edit(asset, "streakFan", 9f);
            snapshot.Discard(asset);
            Assert.That(asset.streakFan, Is.EqualTo(7.25f), "Discard restores the last save");
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            Assert.That(File.ReadAllLines(path), Is.EqualTo(after));
        }

        [Test]
        public void Discard_RestoresTheSnapshot_AndReachesALiveJumpLikeAnEdit()
        {
            var asset = CopyOfShipped<SectorJumpProfile>(JumpPath);
            var saved = asset.streakFan;
            snapshot.Take(asset);
            var revision = asset.Revision;

            Edit(asset, "streakFan", saved + 3f);
            snapshot.Discard(asset);

            Assert.That(asset.streakFan, Is.EqualTo(saved));
            Assert.IsFalse(snapshot.HasUnsavedEdits(asset));
            Assert.That(asset.Revision, Is.GreaterThan(revision + 1), "a Discard reaches a live jump like an edit");
        }

        [Test]
        public void Discard_LeavesAnAssetDirtyThatWasDirtyBeforeThePreview()
        {
            var asset = CopyOfShipped<SectorJumpProfile>(JumpPath);
            EditorUtility.SetDirty(asset);
            snapshot.Take(asset);

            Edit(asset, "streakFan", asset.streakFan + 3f);
            snapshot.Discard(asset);

            Assert.IsTrue(EditorUtility.IsDirty(asset), "edits from before the preview still reach the next save");
        }

        [Test]
        public void CanopyNestedEditsAndImport_ApplyInPlace_AndDiscardRestoresTheSavedBlock()
        {
            var asset = CopyOfShipped<CanopyProjectionProfile>(CanopyPath);
            var path = AssetDatabase.GetAssetPath(asset);
            var before = File.ReadAllBytes(path);
            snapshot.Take(asset);
            Edit(asset, "lowHull.settings.glow", 1.7f);
            using (var serialized = new SerializedObject(asset))
            {
                serialized.FindProperty("lowHull.fields").intValue = (int)CanopyFields.Glow;
                serialized.ApplyModifiedProperties();
            }
            Assert.That(asset.Resolve(CanopyState.LowHull).glow, Is.EqualTo(1.7f), "nested edits apply live");
            Assert.That(snapshot.HasUnsavedEdits(asset), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "nothing reaches the file before Save");

            snapshot.Save(asset);
            var export = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Editor/Tests/EditMode/Rendering/Projection/OwnerSettings.json").text;
            CanopyTunerSettings.Parse(export).Apply(asset, CanopyState.LowHull);
            Assert.That(asset.lowHull.fields, Is.EqualTo(CanopyFields.All));
            Assert.That(snapshot.HasUnsavedEdits(asset), Is.True, "an import is an unsaved edit");
            snapshot.Discard(asset);
            Assert.That(asset.Resolve(CanopyState.LowHull).glow, Is.EqualTo(1.7f));
            Assert.That(asset.lowHull.fields, Is.EqualTo(CanopyFields.Glow));
            Assert.That(snapshot.HasUnsavedEdits(asset), Is.False);
        }

        [Test]
        public void Resolve_AsksOnlyForEditedAssets_AndAnEditSetBackEndsClean()
        {
            var recap = CopyOfShipped<RecapFeelProfile>(RecapPath);
            var jump = CopyOfShipped<SectorJumpProfile>(JumpPath);
            snapshot.Take(recap, jump);
            var asked = new List<IReadOnlyList<string>>();

            snapshot.Resolve(names =>
            {
                asked.Add(names);
                return false;
            });
            Assert.That(asked, Is.Empty, "untouched assets never ask");

            var saved = recap.BannerInSeconds;
            Edit(recap, "bannerInSeconds", saved + 1f);
            Edit(recap, "bannerInSeconds", saved);
            Assert.IsFalse(snapshot.HasUnsavedEdits(recap), "an edit set back to the saved value is no unsaved edit");
            Assert.IsTrue(EditorUtility.IsDirty(recap));

            Edit(jump, "streakFan", jump.streakFan + 3f);
            snapshot.Resolve(names =>
            {
                asked.Add(names);
                return true;
            });
            Assert.That(asked.Single(), Is.EqualTo(new[] { jump.name }));
            Assert.IsFalse(snapshot.HasUnsavedEdits(jump), "Save leaves nothing unsaved");
            Assert.IsFalse(EditorUtility.IsDirty(jump), "Save wrote the edited asset");
            Assert.IsFalse(EditorUtility.IsDirty(recap), "the set-back asset ends as clean as it started");
        }

        private T CopyOfShipped<T>(string shipped) where T : ScriptableObject
        {
            var path = $"{folder}/{Path.GetFileName(shipped)}";
            Assert.IsTrue(AssetDatabase.CopyAsset(shipped, path));
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // The panel's own edit path.
        private static void Edit(Object asset, string knob, float value)
        {
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty(knob).floatValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
