using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Audio
{
    /// <summary>
    /// The two FMOD wiring rules the hosted suite enforces without the package: every event path
    /// the game references exists in the committed GUIDs export (a Studio rename is otherwise
    /// caught only by the local evidence test), and no prefab carries a missing script (an FMOD
    /// component placed without FMOD's own script GUID shows up as one against the FMOD stub).
    /// </summary>
    [Category("Audio")]
    public sealed class FmodWiringEditModeTests
    {
        private static readonly string GuidsPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "..", "audio", "fmod", "Build", "GUIDs.txt"));

        // Assembled from pieces so this file's own text never scans as a referenced event.
        private static readonly string EventPrefix = "event" + ":/";
        private static readonly Regex EventPath = new Regex(Regex.Escape(EventPrefix) + "[^\\s\"'`<>]+");

        [Test]
        public void ReferencedEventPaths_ExistInGuidsExport()
        {
            var known = KnownEventPaths();
            Assert.That(known, Is.Not.Empty, GuidsPath);
            var referenced = ReferencedEventPaths();
            Assert.That(referenced, Is.Not.Empty, "nothing references an event path: the scan is broken");
            Assert.That(MissingPaths(referenced, known), Is.Empty);
        }

        [Test]
        public void MissingPaths_ReportsAPathTheExportLacks()
        {
            var bogus = EventPrefix + "Nowhere/Missing";
            var missing = MissingPaths(new[] { (bogus, "synthetic") }, KnownEventPaths());
            Assert.That(missing, Is.EqualTo(new[] { $"{bogus} (synthetic)" }));
        }

        [Test]
        public void Prefabs_CarryNoMissingScripts()
        {
            var broken = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab")) continue;
                var root = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                Assert.That(root, Is.Not.Null, path);
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                {
                    var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
                    if (count > 0)
                        broken.Add($"{path} at '{AnimationUtility.CalculateTransformPath(node, root.transform)}': {count} missing script(s)");
                }
            }
            Assert.That(broken, Is.Empty, string.Join("\n", broken));
        }

        private static HashSet<string> KnownEventPaths() =>
            File.ReadLines(GuidsPath)
                .Select(line => line.Substring(line.IndexOf(' ') + 1).Trim())
                .Where(path => path.StartsWith(EventPrefix))
                .ToHashSet();

        private static List<(string path, string file)> ReferencedEventPaths()
        {
            var prefabs = Directory.EnumerateFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            var scripts = Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);
            var found = new List<(string, string)>();
            foreach (var file in prefabs.Concat(scripts))
                foreach (Match match in EventPath.Matches(File.ReadAllText(file)))
                    found.Add((match.Value, file.Substring(Application.dataPath.Length + 1).Replace('\\', '/')));
            return found;
        }

        private static List<string> MissingPaths(IEnumerable<(string path, string file)> referenced, ICollection<string> known) =>
            referenced.Where(r => !known.Contains(r.path)).Select(r => $"{r.path} ({r.file})").ToList();
    }
}
