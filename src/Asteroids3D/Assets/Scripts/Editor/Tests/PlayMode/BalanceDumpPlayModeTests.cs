#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Balance;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Game;
using Game.Runs;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate.Results;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    /// The balance dump. <see cref="DumpCatalog"/> measures the shipped catalog and writes a dump
    /// under the results root, then has scripts/balance/balance_table.py render it beside itself,
    /// with the delta against the dump before it by filename. It is explicit, so only a run naming it
    /// measures: the Astronomical/Balance menu item, or
    /// <c>unity_test_agent.ps1 -Mode PlayMode -TestFilter BalanceDumpPlayModeTests.DumpCatalog -ExcludeCategory ''</c>.
    /// The empty exclusion matters: NUnit never counts a run that excludes a category as naming an
    /// explicit test. Why a test and not an editor command: https://github.com/amindell11/astronomical-home/issues/916#issuecomment-5978022472
    /// </summary>
    [Category("Weapons")]
    public class BalanceDumpPlayModeTests : PlayModeWorldFixture
    {
        private const string InitScenePath = "Assets/Scenes/InitScene.unity";
        private const string FingerprintKey = "BalanceDump.StatFingerprint";
        private const string SettingLinesKey = "BalanceDump.SettingLines";
        private const string DumpSuffix = "-balance-dump.json";

        private readonly List<Object> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var item in spawned)
                if (item) Object.DestroyImmediate(item is Component component ? component.gameObject : item);
            spawned.Clear();
            base.TearDown();
        }

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

        // Inactive, so nothing on it wakes: the probe's instance is the one that runs.
        private T Template<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var component = go.AddComponent<T>();
            spawned.Add(component);
            return component;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Laser Bolt(string name, float damage)
        {
            var bolt = Template<Laser>(name);
            bolt.gameObject.AddComponent<Rigidbody>().useGravity = false;
            SetFloat(bolt, "damage", damage);
            return bolt;
        }

        private Ship Hull(string name, float maxHealth)
        {
            var hull = Template<Ship>(name);
            hull.maxHealth = maxHealth;
            return hull;
        }

        private ShieldModule Shield(string name, float maxShield)
        {
            var shield = ScriptableObject.CreateInstance<ShieldModule>();
            shield.name = name;
            shield.maxShield = maxShield;
            spawned.Add(shield);
            return shield;
        }

        private ItemCatalog Catalog(Ship[] chassis, ShieldModule[] shields, Component[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            spawned.Add(catalog);
            var serialized = new SerializedObject(catalog);
            void Fill(string field, Object[] items)
            {
                var list = serialized.FindProperty(field);
                list.arraySize = items.Length;
                for (var i = 0; i < items.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("chassis", chassis);
            Fill("engines", Array.Empty<Object>());
            Fill("shields", shields);
            Fill("weapons", weapons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [UnityTest]
        public IEnumerator Measure_ComposesRowsPoolsAndInputs()
        {
            var lasers = Template<Lasers>("MadeUpLasers");
            lasers.projectilePrefab = Bolt("MadeUpBolt", damage: 5f);
            SetFloat(lasers.gameObject.AddComponent<Cooldown>(), "fireRate", 0.1f);
            lasers.gameObject.AddComponent<Heat>().Configure(maxHeat: 100f, heatPerShot: 30f, coolingRate: 100f,
                coolDownDelay: 0.1f, overheatPenaltyTime: 0.5f);
            var rippers = Template<Rippers>("MadeUpRippers");
            rippers.projectilePrefab = Bolt("MadeUpSlug", damage: 4f);
            SetFloat(rippers.gameObject.AddComponent<Cooldown>(), "fireRate", 0.2f);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 4, reloadTime: 1f);
            var catalog = Catalog(
                new[] { Hull("LightHull", maxHealth: 100f), Hull("HeavyHull", maxHealth: 150f) },
                new[] { Shield("ThinShield", maxShield: 50f), Shield("ThickShield", maxShield: 100f) },
                new Component[] { lasers, rippers });

            var measured = default(BalanceDump);
            yield return BalanceDump.Measure(catalog, Projectiles, new DateTime(2026, 10, 4, 12, 30, 5, DateTimeKind.Utc),
                new BuildIdentity { commit = "0123abcd", dirty = true }, "f0e1d2c3b4a59687",
                new[] { "setting/killHullRestore=0.25" }, dump => measured = dump);
            // Assert on what the file carries.
            var dump = JsonUtility.FromJson<BalanceDump>(measured.ToJson());

            Assert.AreEqual("balance-dump-v1", dump.schema);
            Assert.AreEqual("2026-10-04T12:30:05Z", dump.takenUtc);
            Assert.AreEqual("0123abcd", dump.buildIdentity.commit);
            Assert.AreEqual("f0e1d2c3b4a59687", dump.statFingerprint);
            CollectionAssert.AreEqual(new[] { 150f, 200f, 250f }, dump.pools,
                "one pool per distinct hull plus shield: 100 + 100 and 150 + 50 are one");

            CollectionAssert.AreEqual(new[] { "MadeUpLasers/hold", "MadeUpLasers/AI", "MadeUpRippers/hold" },
                dump.derived.Select(row => $"{row.weapon}/{row.mode}").ToArray(),
                "catalog order, then the probe's mode order");
            var magazine = dump.derived[2];
            Assert.AreEqual(16f, magazine.openingDamage, 0.001f);
            Assert.AreEqual(10f, magazine.sustainedDps, 0.2f, "16 every 0.6 s dump plus 1 s reload");
            Assert.That(magazine.stakes, Is.EqualTo(new[] { 16f / 150f, 16f / 200f, 16f / 250f }).Within(0.001f),
                "the opening burst over each pool, in pool order");
            Assert.IsTrue(dump.derived.All(row => row.stakes.Count == 3), "every row has a stake per pool");

            CollectionAssert.IsOrdered(dump.inputs, StringComparer.Ordinal);
            CollectionAssert.AllItemsAreUnique(dump.inputs);
            CollectionAssert.IsSubsetOf(new[]
            {
                "setting/killHullRestore=0.25",
                "HeavyHull/Ship.maxHealth=150",
                "ThickShield/ShieldModule.maxShield=100",
                "MadeUpLasers/Lasers.projectilePrefab=MadeUpBolt",
                "MadeUpBolt/Laser.damage=5",
                "MadeUpSlug/Laser.damage=4",
            }, dump.inputs, "the setting lines plus every catalog item's stat lines");
        }
    }
}
#endif
