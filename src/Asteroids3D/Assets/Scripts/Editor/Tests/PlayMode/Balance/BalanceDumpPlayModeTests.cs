#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Balance;
using Game;
using Game.Runs;
using NUnit.Framework;
using Ships.Loadout;
using Substrate.Results;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Tests.PlayMode.Balance
{
    /// <summary>
    /// <see cref="DumpCatalog"/> is explicit, so only a run naming it measures: the Astronomical/Balance
    /// menu item, or <c>unity_test_agent.ps1 -Mode PlayMode -TestFilter BalanceDumpPlayModeTests.DumpCatalog -ExcludeCategory ''</c>.
    /// The empty exclusion matters: NUnit never counts a run that excludes a category as naming an
    /// explicit test. No category on purpose: a category-only selection (routed, or the Test Runner's
    /// category pick) names every explicit test in that category's fixtures. The prebuild setup is not
    /// explicit: any PlayMode run whose filter passes the test runs it.
    /// Why a test and not an editor command: https://github.com/amindell11/astronomical-home/issues/916#issuecomment-5978022472
    /// </summary>
    public class BalanceDumpPlayModeTests : PlayModeWorldFixture
    {
        private const string InitScenePath = "Assets/Scenes/InitScene.unity";
        private const string FingerprintKey = "BalanceDump.StatFingerprint";
        private const string SettingLinesKey = "BalanceDump.SettingLines";
        private const string DumpSuffix = "-balance-dump.json";

        [MenuItem("Astronomical/Balance/Write balance dump")]
        private static void DumpFromMenu() =>
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { $"{typeof(BalanceDumpPlayModeTests).FullName}.{nameof(DumpCatalog)}" },
            }));

        // Runs in edit mode before play: a scene cannot be opened in play mode.
        private sealed class ReadStatFingerprintInputs : IPrebuildSetup
        {
            public void Setup()
            {
                var scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Additive);
                try
                {
                    var host = scene.GetRootGameObjects()
                        .Select(root => root.GetComponentInChildren<GameHost>(true))
                        .FirstOrDefault(found => found);
                    if (!host)
                        throw new InvalidOperationException($"{InitScenePath} holds no GameHost to read the stat fingerprint from.");

                    var (offer, sector, refill) = host.StatFingerprintInputs;
                    SessionState.SetString(FingerprintKey, StatHash.OfSetting(offer, sector, refill));
                    SessionState.SetString(SettingLinesKey, string.Join("\n", StatHash.SettingLines(offer, sector, refill)));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        // Minutes long: each charge weapon fires to the probe's simulated cap under every pattern.
        [UnityTest, Explicit("Measures the shipped catalog for minutes and writes a balance dump; runs only by name.")]
        [Timeout(30 * 60 * 1000)]
        [PrebuildSetup(typeof(ReadStatFingerprintInputs))]
        public IEnumerator DumpCatalog()
        {
            var fingerprint = SessionState.GetString(FingerprintKey, "");
            var settingLines = SessionState.GetString(SettingLinesKey, "").Split('\n');
            SessionState.EraseString(FingerprintKey);
            SessionState.EraseString(SettingLinesKey);
            Assert.IsNotEmpty(fingerprint, "the prebuild setup handed no stat fingerprint into play mode");
            Assert.IsTrue(BuildIdentity.TryReadGit(out var identity, out var failure), $"build identity unreadable: {failure}");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            Assert.IsNotNull(catalog, $"ItemCatalog missing at {ItemCatalog.AssetPath}");

            var takenUtc = DateTime.UtcNow;
            var dump = default(BalanceDump);
            yield return BalanceDump.Measure(catalog, Projectiles, takenUtc, identity, fingerprint, settingLines,
                measured => dump = measured);

            var folder = ResultsRoot.Folder(RunRecordStore.Folder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder,
                takenUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + DumpSuffix);
            File.WriteAllText(path, dump.ToJson());
            var previous = Directory.GetFiles(folder, "*" + DumpSuffix)
                .Where(other => string.CompareOrdinal(Path.GetFileName(other), Path.GetFileName(path)) < 0)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .LastOrDefault();
            var table = Path.ChangeExtension(path, ".md");
            File.WriteAllText(table, Render(path, previous));
            Debug.Log($"Balance dump written to '{path}', its table to '{table}'" +
                      (previous == null ? ", no previous dump to take a delta against." : $", delta against '{previous}'."));

            CollectionAssert.AreEquivalent(catalog.Weapons.Select(weapon => weapon.name),
                dump.derived.Select(row => row.weapon).Distinct(), "every catalog weapon has a row");
        }

        private static string Render(string dump, string previous)
        {
            var script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "scripts", "balance", "balance_table.py"));
            var arguments = $"\"{script}\" \"{dump}\"" + (previous == null ? "" : $" \"{previous}\"");
            var command = $"python {arguments}";
            var start = new ProcessStartInfo("python", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using var python = Process.Start(start);
                var table = python.StandardOutput.ReadToEnd();
                var error = python.StandardError.ReadToEnd();
                python.WaitForExit();
                Assert.AreEqual(0, python.ExitCode, $"`{command}` failed: {error.Trim()}");
                return table;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new AssertionException($"`{command}` could not start: {e.Message}");
            }
        }
    }
}
#endif
