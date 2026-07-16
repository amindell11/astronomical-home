#if UNITY_EDITOR
using System;
using System.Linq;
using AI;
using AI.States;
using AI.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the authored pilot data: per-ship profile overrides must resolve through the [SerializeReference] chooser payload, on the base prefab and across every variant-override site.</summary>
    [Category("AI")]
    public class PilotChooserAuthoringEditModeTests
    {
        private static string[] ProfileNames(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(root, $"Missing prefab: {prefabPath}");
            return root.GetComponentsInChildren<Brain>(true)
                .Select(b => b.Chooser)
                .OfType<UtilityChooser>()
                .SelectMany(c => c.StateProfiles ?? Array.Empty<StateProfile>())
                .Select(p => p ? p.name : "<null>")
                .ToArray();
        }

        [TestCase("Assets/Prefabs/Pilots/UtilityPilot.prefab", "AttackAggressive")]
        [TestCase("Assets/Prefabs/Pilots/UtilityPilot Variant.prefab", "Evade")]
        [TestCase("Assets/Prefabs/Ships/Cargo Ships/Junker_1.prefab", "Patrol")]
        public void PilotPrefab_ChooserCarriesAuthoredProfiles(string prefabPath, string expectedProfile)
        {
            CollectionAssert.AreEqual(new[] { expectedProfile }, ProfileNames(prefabPath));
        }

        [Test]
        public void ChaseBenchmarkSector_PilotOverridesResolveDistinctProfiles()
        {
            CollectionAssert.AreEquivalent(new[] { "ChasePursue", "ChaseFlee" },
                ProfileNames("Assets/Prefabs/Sectors/ChaseBenchmarkSector.prefab"));
        }
    }
}
#endif
