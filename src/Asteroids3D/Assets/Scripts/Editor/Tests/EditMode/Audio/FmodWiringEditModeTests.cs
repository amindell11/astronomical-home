using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.EditMode.Audio
{
    /// <summary>
    /// The FMOD wiring rules the hosted suite enforces without the package: every event path the
    /// game references and every parameter name a constant carries exist in the committed GUIDs
    /// export (a Studio rename is otherwise caught only by the local evidence tests), and no prefab
    /// or scene carries a missing script (an FMOD component placed without FMOD's own script GUID
    /// shows up as one against the FMOD stub).
    /// </summary>
    [Category("Audio")]
    public sealed class FmodWiringEditModeTests
    {
        private static readonly string GuidsPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "..", "audio", "fmod", "Build", "GUIDs.txt"));

        // Assembled from pieces so this file's own text never scans as a referenced event or parameter.
        private static readonly string EventPrefix = "event" + ":/";
        private static readonly Regex EventPath = new Regex(Regex.Escape(EventPrefix) + "[^\\s\"'`<>]+");
        private static readonly string ParameterPrefix = "parameter" + ":/";
        private static readonly Regex ParameterConstant = new Regex(@"const\s+string\s+\w+Param" + @"eter\s*=\s*""([^""]+)""");

        [Test]
        public void ReferencedEventPaths_ExistInGuidsExport()
        {
            var known = KnownPaths(EventPrefix);
            Assert.That(known, Is.Not.Empty, GuidsPath);
            var referenced = ReferencedEventPaths();
            Assert.That(referenced, Is.Not.Empty, "nothing references an event path: the scan is broken");
            Assert.That(MissingPaths(referenced, known), Is.Empty);
        }

        [Test]
        public void ParameterNameConstants_ExistInGuidsExport()
        {
            var known = KnownPaths(ParameterPrefix);
            Assert.That(known, Is.Not.Empty, GuidsPath);
            var referenced = ReferencedParameterNames();
            Assert.That(referenced, Is.Not.Empty, "no parameter constant found: the scan is broken");
            Assert.That(MissingPaths(referenced, known), Is.Empty);
        }

        [Test]
        public void MissingPaths_ReportsAPathTheExportLacks()
        {
            var bogus = EventPrefix + "Nowhere/Missing";
            var missing = MissingPaths(new[] { (bogus, "synthetic") }, KnownPaths(EventPrefix));
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
                CollectMissingScripts(path, root, broken);
            }
            Assert.That(broken, Is.Empty, string.Join("\n", broken));
        }

        [Test]
        public void Scenes_CarryNoMissingScripts()
        {
            var broken = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        CollectMissingScripts(path, root, broken);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            Assert.That(broken, Is.Empty, string.Join("\n", broken));
        }

        private static void CollectMissingScripts(string asset, GameObject root, List<string> broken)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
                if (count > 0)
                    broken.Add($"{asset} at '{root.name}/{AnimationUtility.CalculateTransformPath(node, root.transform)}': {count} missing script(s)");
            }
        }

        private static HashSet<string> KnownPaths(string prefix) =>
            File.ReadLines(GuidsPath)
                .Select(line => line.Substring(line.IndexOf(' ') + 1).Trim())
                .Where(path => path.StartsWith(prefix))
                .ToHashSet();

        private static List<(string path, string file)> ReferencedEventPaths()
        {
            var prefabs = Directory.EnumerateFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
            var scenes = Directory.EnumerateFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories);
            var found = new List<(string, string)>();
            foreach (var file in prefabs.Concat(scenes).Concat(ScriptFiles()))
                foreach (Match match in EventPath.Matches(File.ReadAllText(file)))
                    found.Add((match.Value, RelativePath(file)));
            return found;
        }

        private static List<(string path, string file)> ReferencedParameterNames()
        {
            var found = new List<(string, string)>();
            foreach (var file in ScriptFiles())
                foreach (Match match in ParameterConstant.Matches(File.ReadAllText(file)))
                    found.Add((ParameterPrefix + match.Groups[1].Value, RelativePath(file)));
            return found;
        }

        private static IEnumerable<string> ScriptFiles() =>
            Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);

        private static string RelativePath(string file) =>
            file.Substring(Application.dataPath.Length + 1).Replace('\\', '/');

        private static List<string> MissingPaths(IEnumerable<(string path, string file)> referenced, ICollection<string> known) =>
            referenced.Where(r => !known.Contains(r.path)).Select(r => $"{r.path} ({r.file})").ToList();
    }
}
