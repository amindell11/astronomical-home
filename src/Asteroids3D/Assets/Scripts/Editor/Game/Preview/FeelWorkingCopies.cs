using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Preview
{
    /// <summary>
    /// The preview's in-memory copies of feel assets for one play session. The host runs on a copy and the
    /// panel edits it, so tuning applies live while the asset changes only on Save. Leaving play mode with
    /// unsaved edits asks Save or Discard, and the copies end with the session.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class FeelWorkingCopies
    {
        public static FeelWorkingCopies Current { get; } = new();

        private readonly Dictionary<ScriptableObject, ScriptableObject> copies = new();
        private readonly Dictionary<ScriptableObject, ScriptableObject> assets = new();

        static FeelWorkingCopies() => EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Current.Resolve(AskToSave);
            else if (change == PlayModeStateChange.EnteredEditMode) Current.Close();
        };

        public T For<T>(T asset) where T : ScriptableObject
        {
            if (copies.TryGetValue(asset, out var existing)) return (T)existing;
            var copy = Object.Instantiate(asset);
            copy.name = asset.name;
            copy.hideFlags = HideFlags.DontSave;
            copies.Add(asset, copy);
            assets.Add(copy, asset);
            return copy;
        }

        public ScriptableObject AssetOf(ScriptableObject copy) => assets.TryGetValue(copy, out var asset)
            ? asset
            : throw new ArgumentException($"{copy.name} is not a preview working copy.", nameof(copy));

        public bool HasUnsavedEdits(ScriptableObject copy) => !KnobsEqual(copy, AssetOf(copy));

        public void Save(ScriptableObject copy)
        {
            var asset = AssetOf(copy);
            CopyKnobs(copy, asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public void Revert(ScriptableObject copy) => CopyKnobs(AssetOf(copy), copy);

        public void Resolve(Func<IReadOnlyList<string>, bool> saveUnsaved) => Resolve(assets.Keys.ToList(), saveUnsaved);

        public void Resolve(ScriptableObject copy, Func<IReadOnlyList<string>, bool> saveUnsaved) =>
            Resolve(new[] { copy }, saveUnsaved);

        public void Close()
        {
            foreach (var copy in assets.Keys) Object.DestroyImmediate(copy);
            copies.Clear();
            assets.Clear();
        }

        public static bool AskToSave(IReadOnlyList<string> names) => EditorUtility.DisplayDialog("Unsaved feel edits",
            $"{string.Join(", ", names)} {(names.Count == 1 ? "has" : "have")} unsaved edits. " +
            "Save writes them to the asset; Discard drops them.", "Save", "Discard");

        private void Resolve(IReadOnlyList<ScriptableObject> candidates, Func<IReadOnlyList<string>, bool> saveUnsaved)
        {
            var unsaved = candidates.Where(HasUnsavedEdits).ToList();
            if (unsaved.Count == 0) return;
            var save = saveUnsaved(unsaved.Select(copy => copy.name).ToList());
            foreach (var copy in unsaved)
                if (save) Save(copy);
                else Revert(copy);
        }

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
