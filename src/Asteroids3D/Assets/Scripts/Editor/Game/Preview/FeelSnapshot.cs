using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Preview
{
    /// <summary>
    /// The preview's snapshot of the feel assets the panel edits in place. Taking it keeps a hidden copy
    /// of each asset. Save writes the asset and moves its copy forward; Discard restores the copy and
    /// leaves the asset as dirty or clean as it was. Leaving play mode asks Save or Discard for edited
    /// assets and discards the rest silently. As a singleton it survives a mid-play domain reload, so
    /// that prompt still fires; it ends with the play session.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class FeelSnapshot : ScriptableSingleton<FeelSnapshot>
    {
        [Serializable]
        private sealed class Entry
        {
            public ScriptableObject asset;
            public ScriptableObject saved;
            public bool wasDirty;
        }

        [SerializeField] private List<Entry> entries = new();

        static FeelSnapshot() => EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingPlayMode) instance.Resolve(AskToSave);
            else if (change == PlayModeStateChange.EnteredEditMode) instance.Close();
        };

        public void Take(params ScriptableObject[] assets)
        {
            foreach (var asset in assets)
            {
                if (entries.Any(entry => entry.asset == asset))
                    throw new InvalidOperationException($"{asset.name} is already under the preview snapshot.");
                var saved = Instantiate(asset);
                saved.hideFlags = HideFlags.HideAndDontSave;
                entries.Add(new Entry { asset = asset, saved = saved, wasDirty = EditorUtility.IsDirty(asset) });
            }
        }

        public bool HasUnsavedEdits(ScriptableObject asset) => !KnobsEqual(asset, EntryOf(asset).saved);

        public void Save(ScriptableObject asset)
        {
            var entry = EntryOf(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            CopyKnobs(asset, entry.saved);
            entry.wasDirty = false;
        }

        public void Discard(ScriptableObject asset)
        {
            var entry = EntryOf(asset);
            CopyKnobs(entry.saved, asset);
            if (!entry.wasDirty) EditorUtility.ClearDirty(asset);
        }

        public void Resolve(Func<IReadOnlyList<string>, bool> saveUnsaved)
        {
            var unsaved = entries.Where(entry => HasUnsavedEdits(entry.asset)).ToList();
            var save = unsaved.Count > 0 && saveUnsaved(unsaved.Select(entry => entry.asset.name).ToList());
            foreach (var entry in entries)
                if (save && unsaved.Contains(entry)) Save(entry.asset);
                else Discard(entry.asset);
        }

        public void Close()
        {
            foreach (var entry in entries) DestroyImmediate(entry.saved);
            entries.Clear();
        }

        public static bool AskToSave(IReadOnlyList<string> names) => EditorUtility.DisplayDialog("Unsaved feel edits",
            $"{string.Join(", ", names)} {(names.Count == 1 ? "has" : "have")} unsaved edits. " +
            "Save writes them to the asset; Discard restores the last saved values.", "Save", "Discard");

        private Entry EntryOf(ScriptableObject asset) => entries.FirstOrDefault(entry => entry.asset == asset) ??
            throw new ArgumentException($"{asset.name} is not under the preview snapshot.", nameof(asset));

        // The visible properties are exactly the knobs the panel draws.
        private static bool KnobsEqual(Object a, Object b)
        {
            using var left = new SerializedObject(a);
            using var right = new SerializedObject(b);
            var property = left.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                if (property.name != "m_Script" &&
                    !SerializedProperty.DataEquals(property, right.FindProperty(property.propertyPath)))
                    return false;
            return true;
        }

        private static void CopyKnobs(Object from, Object to)
        {
            using var source = new SerializedObject(from);
            using var target = new SerializedObject(to);
            var property = source.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                if (property.name != "m_Script") target.CopyFromSerializedPropertyIfDifferent(property);
            target.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
