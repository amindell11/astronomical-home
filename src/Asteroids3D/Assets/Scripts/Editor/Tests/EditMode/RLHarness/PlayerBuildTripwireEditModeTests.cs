#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.RLHarness;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tests.EditMode
{
    // The merge gate never builds a player, so an asmdef edit can drop an assembly from
    // player builds while every editor gate stays green (#185, #251 — 8 days invisible).
    [Category("Smoke")]
    public class PlayerBuildTripwireEditModeTests
    {
        private const string PlayerPlatform = "WindowsStandalone64";
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";

        // None of these is defined in a player build.
        private static readonly string[] KnownConstraintSymbols = { "UNITY_EDITOR", "UNITY_INCLUDE_TESTS" };

        [Serializable]
        private class AsmdefJson
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public string[] defineConstraints;
        }

        private sealed class ProjectAsmdef
        {
            public string Path;
            public string Guid;
            public AsmdefJson Json;
            public string ExclusionReason;
            public bool PlayerIncluded => ExclusionReason == null;
        }

        private static List<ProjectAsmdef> LoadProjectAsmdefs()
        {
            var result = new List<ProjectAsmdef>();
            foreach (var guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var json = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(FullPath(path)));
                result.Add(new ProjectAsmdef
                {
                    Path = path,
                    Guid = guid,
                    Json = json,
                    ExclusionReason = PlayerExclusionReason(json),
                });
            }
            Assert.IsNotEmpty(result, "no asmdefs found under Assets/ — the tripwire has lost its subject");
            return result;
        }

        private static string PlayerExclusionReason(AsmdefJson a)
        {
            var include = a.includePlatforms ?? Array.Empty<string>();
            if (include.Length > 0 && !include.Contains(PlayerPlatform))
                return $"includePlatforms [{string.Join(", ", include)}] omits {PlayerPlatform}";
            if ((a.excludePlatforms ?? Array.Empty<string>()).Contains(PlayerPlatform))
                return $"excludePlatforms lists {PlayerPlatform}";
            foreach (var constraint in a.defineConstraints ?? Array.Empty<string>())
            {
                var negated = constraint.StartsWith("!");
                var symbol = negated ? constraint.Substring(1) : constraint;
                Assert.Contains(symbol, KnownConstraintSymbols,
                    $"'{a.name}' has defineConstraint '{constraint}', which this lint cannot evaluate "
                    + "for a player build; extend KnownConstraintSymbols knowingly.");
                if (!negated)
                    return $"defineConstraint '{constraint}' is undefined in player builds";
            }
            return null;
        }

        // #251's shape: Game.Capture.Editor silently dropped, Game.RLHarness.Editor still
        // compiled against its types — CS0246 only a player build can see.
        [Test]
        public void PlayerIncludedAsmdefs_ReferenceOnlyPlayerIncludedAsmdefs()
        {
            var all = LoadProjectAsmdefs();
            var byName = all.ToDictionary(a => a.Json.name);
            var byGuid = all.ToDictionary(a => a.Guid);

            var violations = new List<string>();
            foreach (var asmdef in all.Where(a => a.PlayerIncluded))
            foreach (var reference in asmdef.Json.references ?? Array.Empty<string>())
            {
                var target = Resolve(reference, byName, byGuid);
                if (target != null && !target.PlayerIncluded)
                    violations.Add($"{asmdef.Json.name} -> {target.Json.name} ({target.ExclusionReason})");
            }

            Assert.IsEmpty(violations,
                "player-included assemblies reference assemblies a player build drops; "
                + "the next RLTraining player build fails with CS0246 while every editor gate stays green:\n"
                + string.Join("\n", violations));
        }

        private static ProjectAsmdef Resolve(string reference,
            Dictionary<string, ProjectAsmdef> byName, Dictionary<string, ProjectAsmdef> byGuid)
        {
            if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                return byGuid.TryGetValue(reference.Substring(5).ToLowerInvariant(), out var g) ? g : null;
            return byName.TryGetValue(reference, out var n) ? n : null;
        }

        // #185's shape: the scene's own component assembly dropped from the player. Asmdef
        // references never see it — the scene-to-script edge lives in the asset database.
        [Test]
        public void TrainingSceneScriptDependencies_SurvivePlayerBuild()
        {
            var byPath = LoadProjectAsmdefs().ToDictionary(a => a.Path);
            var scripts = AssetDatabase.GetDependencies(RLTrainingPlayerBuild.Scene, true)
                .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.IsNotEmpty(scripts,
                $"{RLTrainingPlayerBuild.Scene} resolved zero script dependencies — the tripwire has lost its subject");

            var violations = new List<string>();
            foreach (var script in scripts)
            {
                var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(script);
                if (asmdefPath == null)
                    continue;
                if (!byPath.TryGetValue(asmdefPath.Replace('\\', '/'), out var owner))
                    continue;
                if (!owner.PlayerIncluded)
                    violations.Add($"{script} ({owner.Json.name}: {owner.ExclusionReason})");
            }

            Assert.IsEmpty(violations,
                "the RLTraining scene depends on scripts a player build drops; "
                + "the built player loses these components while every editor gate stays green:\n"
                + string.Join("\n", violations));
        }

        // A Succeeded build receipt does not prove hydrated meshes: pointer-shaped FBXs import
        // as null and every worker dies at spawn (throughput plan, Stage 0C).
        [Test]
        public void SpawnSettingsFbxDependencies_AreHydratedLfsPayloads()
        {
            var fbxPaths = AssetDatabase.GetDependencies(SpawnSettingsPath, true)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.IsNotEmpty(fbxPaths,
                $"{SpawnSettingsPath} resolved zero FBX dependencies — the tripwire has lost its subject");

            var pointers = fbxPaths.Where(IsLfsPointer).ToArray();
            Assert.IsEmpty(pointers,
                "git-lfs pointer text where mesh payloads should be; hydrate with `git lfs pull` before building:\n"
                + string.Join("\n", pointers));
        }

        private static bool IsLfsPointer(string assetPath)
        {
            using (var stream = File.OpenRead(FullPath(assetPath)))
            {
                var head = new byte[64];
                var read = stream.Read(head, 0, head.Length);
                return Encoding.ASCII.GetString(head, 0, read).StartsWith("version https://git-lfs");
            }
        }

        private static string FullPath(string assetPath) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }
}
#endif
